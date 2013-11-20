//     Copyright (c) Microsoft Corporation.  All rights reserved.
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// 
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Security;
using System.Diagnostics;
using Diagnostics.Tracing.Parsers;

// code:System.Diagnostics.ETWTraceEventSource defintion.
namespace Diagnostics.Tracing
{
    /// <summary>
    /// A code:ETWTraceEventSource represents the stream of events that was collected from a
    /// code:TraceEventSession (eg the ETL moduleFile, or the live session event stream). Like all
    /// code:TraceEventSource, it logically represents a stream of code:TraceEvent s. Like all
    /// code:TraceEventDispather s it supports a callback model where Parsers attach themselves to this
    /// soures, and user callbacks defined on the parsers are called when the 'Process' methodIndex is called.
    /// 
    /// * See also code:TraceEventDispatcher
    /// * See also code:TraceEvent
    /// * See also code:#ETWTraceEventSourceInternals
    /// * See also code:#ETWTraceEventSourceFields
    /// </summary>    

    public unsafe sealed class ETWTraceEventSource : TraceEventDispatcher, IDisposable
    {
        /// <summary>
        /// Open a ETW event trace moduleFile (ETL moduleFile) for processing.  
        /// </summary>
        /// <param name="fileName">The ETL data moduleFile to open</param>
        public ETWTraceEventSource(string fileName)
            : this(fileName, TraceEventSourceType.MergeAll)
        {
        }
        /// <summary>
        /// Open a ETW event source for processing.  This can either be a moduleFile or a real time ETW session
        /// </summary>
        /// <param name="fileOrSessionName">
        /// If type == ModuleFile this is the name of the moduleFile to open.
        /// If type == Session this is the name of real time sessing to open.</param>
        /// <param name="type"></param>
        // [SecuritySafeCritical]
        public ETWTraceEventSource(string fileOrSessionName, TraceEventSourceType type)
        {
            long now = DateTime.Now.ToFileTime() - 100000;     // used as the start time for real time sessions (sub 10msec to avoid negative times)

            // Allocate the LOGFILE and structures and arrays that hold them  
            // Figure out how many log files we have
            if (type == TraceEventSourceType.MergeAll)
            {
                string fileBaseName = Path.GetFileNameWithoutExtension(fileOrSessionName);
                string dir = Path.GetDirectoryName(fileOrSessionName);
                if (dir.Length == 0)
                    dir = ".";
                List<string> allLogFiles = new List<string>();
                allLogFiles.AddRange(Directory.GetFiles(dir, fileBaseName + ".etl"));
                allLogFiles.AddRange(Directory.GetFiles(dir, fileBaseName + ".kernel.etl"));
                allLogFiles.AddRange(Directory.GetFiles(dir, fileBaseName + ".clr*.etl"));
                allLogFiles.AddRange(Directory.GetFiles(dir, fileBaseName + ".user*.etl"));

                if (allLogFiles.Count == 0)
                    throw new FileNotFoundException("Could not find file     " + fileOrSessionName);

                logFiles = new TraceEventNativeMethods.EVENT_TRACE_LOGFILEW[allLogFiles.Count];
                for (int i = 0; i < allLogFiles.Count; i++)
                    logFiles[i].LogFileName = allLogFiles[i];
            }
            else
            {
                logFiles = new TraceEventNativeMethods.EVENT_TRACE_LOGFILEW[1];
                if (type == TraceEventSourceType.FileOnly)
                    logFiles[0].LogFileName = fileOrSessionName;
                else
                {
                    Debug.Assert(type == TraceEventSourceType.Session);
                    logFiles[0].LoggerName = fileOrSessionName;
                    logFiles[0].LogFileMode |= TraceEventNativeMethods.EVENT_TRACE_REAL_TIME_MODE;
                }
            }
            handles = new ulong[logFiles.Length];

            // Fill  out the first log file information (we will clone it later if we have mulitple files). 
            logFiles[0].BufferCallback = this.TraceEventBufferCallback;
            handles[0] = TraceEventNativeMethods.INVALID_HANDLE_VALUE;
            useClassicETW = Environment.OSVersion.Version.Major < 6;
            if (useClassicETW)
            {
                IntPtr mem = Marshal.AllocHGlobal(sizeof(TraceEventNativeMethods.EVENT_RECORD));
                TraceEventNativeMethods.ZeroMemory(mem, (uint)sizeof(TraceEventNativeMethods.EVENT_RECORD));
                convertedHeader = (TraceEventNativeMethods.EVENT_RECORD*)mem;
                logFiles[0].EventCallback = RawDispatchClassic;
            }
            else
            {
                logFiles[0].LogFileMode |= TraceEventNativeMethods.PROCESS_TRACE_MODE_EVENT_RECORD;
                logFiles[0].EventCallback = RawDispatch;
            }
            // We want the raw timestamp because it is needed to match up stacks with the event they go with.  
            logFiles[0].LogFileMode |= TraceEventNativeMethods.PROCESS_TRACE_MODE_RAW_TIMESTAMP;

            // Copy the information to any additional log files 
            for (int i = 1; i < logFiles.Length; i++)
            {
                logFiles[i].BufferCallback = logFiles[0].BufferCallback;
                logFiles[i].EventCallback = logFiles[0].EventCallback;
                logFiles[i].LogFileMode = logFiles[0].LogFileMode;
                handles[i] = handles[0];
            }

            sessionStartTime100ns = long.MaxValue;
            sessionEndTime100ns = long.MinValue;
            eventsLost = 0;

            // Open all the traces
            for (int i = 0; i < handles.Length; i++)
            {
                handles[i] = TraceEventNativeMethods.OpenTrace(ref logFiles[i]);
                if (handles[i] == TraceEventNativeMethods.INVALID_HANDLE_VALUE)
                    Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRForLastWin32Error());

                // Start time is minimum of all start times 
                if (logFiles[i].LogfileHeader.StartTime < sessionStartTime100ns)
                    sessionStartTime100ns = logFiles[i].LogfileHeader.StartTime;
                // End time is maximum of all start times
                if (logFiles[i].LogfileHeader.EndTime > sessionEndTime100ns)
                    sessionEndTime100ns = logFiles[i].LogfileHeader.EndTime;

                // TODO do we even need log pointer size anymore?   
                // We take the max pointer size.  
                if ((int)logFiles[i].LogfileHeader.PointerSize > pointerSize)
                    pointerSize = (int)logFiles[i].LogfileHeader.PointerSize;

                eventsLost += (int)logFiles[i].LogfileHeader.EventsLost;
            }

            // Real time providers don't set this to something useful
            if (sessionStartTime100ns == 0)
                sessionStartTime100ns = now;
            if (sessionEndTime100ns == 0)
                sessionEndTime100ns = long.MaxValue;

            if (pointerSize == 0)       // Real time does not set this (grrr). 
            {
                pointerSize = sizeof(IntPtr);
                Debug.Assert((logFiles[0].LogFileMode & TraceEventNativeMethods.EVENT_TRACE_REAL_TIME_MODE) != 0);
            }
            Debug.Assert(pointerSize == 4 || pointerSize == 8);

            cpuSpeedMHz = (int)logFiles[0].LogfileHeader.CpuSpeedInMHz;
            numberOfProcessors = (int)logFiles[0].LogfileHeader.NumberOfProcessors;
            _QPCFreq = logFiles[0].LogfileHeader.PerfFreq;
            if (_QPCFreq == 0)          // Real time does not set this all the time 
            {
                _QPCFreq = Stopwatch.Frequency;
                Debug.Assert((logFiles[0].LogFileMode & TraceEventNativeMethods.EVENT_TRACE_REAL_TIME_MODE) != 0);
            }
            Debug.Assert(_QPCFreq != 0);
            int ver = (int) logFiles[0].LogfileHeader.Version;
            osVersion = new Version((byte) ver, (byte) (ver>> 8));

            // Logic for looking up process names
            processNameForID = new Dictionary<int, string>();
            Kernel.ProcessStartGroup += delegate(ProcessTraceData data)
            {
                // Get just the file name without the extension.  Can't use the 'Path' class because
                // it tests to make certain it does not have illegal chars etc.  Since KernelImageFileName
                // is not a true user mode path, we can get failures. 
                string path = data.KernelImageFileName;
                int startIdx = path.LastIndexOf('\\');
                if (0 <= startIdx)
                    startIdx++;
                else
                    startIdx = 0;
                int endIdx = path.LastIndexOf('.');
                if (endIdx <= startIdx) 
                    endIdx = path.Length;
                processNameForID[data.ProcessID] = path.Substring(startIdx, endIdx - startIdx);
            };
        }

        // Process is called after all desired subscriptions have been registered.  
        /// <summary>
        /// Processes all the events in the data soruce, issuing callbacks that were subscribed to.  See
        /// code:#Introduction for more
        /// </summary>
        /// <returns>false If StopProcesing was called</returns>
        // [SecuritySafeCritical]
        public override bool Process()
        {
            if (processTraceCalled)
                Reset();
            processTraceCalled = true;
            stopProcessing = false;
            int dwErr = TraceEventNativeMethods.ProcessTrace(handles, (uint)handles.Length, (IntPtr)0, (IntPtr)0);

            if (dwErr == 6)
                throw new ApplicationException("Error opening ETL file.  Most likely caused by opening a Win8 Trace on a Pre Win8 OS.");

            // ETW returns 1223 when you stop processing explicitly 
            if (!(dwErr == 1223 && stopProcessing))
                Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRFromWin32(dwErr));

            return !stopProcessing;
        }

        /// <summary>
        /// Closes the ETL moduleFile or detaches from the session.  
        /// </summary>  
        public void Close()
        {
            Dispose(true);
        }
        /// <summary>
        /// The log moduleFile that is being processed (if present)
        /// TODO: what does this do for Real time sessions?
        /// </summary>
        public string LogFileName { get { return logFiles[0].LogFileName; } }
        /// <summary>
        /// The name of the session that generated the data. 
        /// </summary>
        public string SessionName { get { return logFiles[0].LoggerName; } }
        /// <summary>
        /// The size of the log, will return 0 if it does not know. 
        /// </summary>
        public override long Size
        {
            get
            {
                long ret = 0;
                for (int i = 0; i < logFiles.Length; i++)
                {
                    var fileName = logFiles[0].LogFileName;
                    if (File.Exists(fileName))
                        ret += new FileInfo(fileName).Length;
                }
                return ret;
            }
        }
        /// <summary>
        /// Returns true if the code:Process can be called mulitple times (if the Data source is from a
        /// moduleFile, not a real time stream.
        /// </summary>
        public bool CanReset { get { return (logFiles[0].LogFileMode & TraceEventNativeMethods.EVENT_TRACE_REAL_TIME_MODE) == 0; } }

        // [SecuritySafeCritical]
        public override void Dispose()
        {
            Dispose(true);
        }

        #region Private

        // #ETWTraceEventSourceInternals
        // 
        // ETWTraceEventSource is a wrapper around the Windows API code:TraceEventNativeMethods.OpenTrace
        // methodIndex (see http://msdn2.microsoft.com/en-us/library/aa364089.aspx) We set it up so that we call
        // back to code:ETWTraceEventSource.Dispatch which is the heart of the event callback logic.
        // [SecuritySafeCritical]
        [AllowReversePInvokeCalls]
        private void RawDispatchClassic(TraceEventNativeMethods.EVENT_RECORD* eventData)
        {
            // TODO not really a EVENT_RECORD on input, but it is a pain to be type-correct.  
            TraceEventNativeMethods.EVENT_TRACE* oldStyleHeader = (TraceEventNativeMethods.EVENT_TRACE*)eventData;
            eventData = convertedHeader;

            eventData->EventHeader.Size = (ushort)sizeof(TraceEventNativeMethods.EVENT_TRACE_HEADER);
            // HeaderType
            eventData->EventHeader.Flags = TraceEventNativeMethods.EVENT_HEADER_FLAG_CLASSIC_HEADER;

            // TODO Figure out if there is a marker that is used in the WOW for the classic providers 
            // right now I assume they are all the same as the machine.  
            if (pointerSize == 8)
                eventData->EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_64_BIT_HEADER;
            else
                eventData->EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_32_BIT_HEADER;

            // EventProperty
            eventData->EventHeader.ThreadId = oldStyleHeader->Header.ThreadId;
            eventData->EventHeader.ProcessId = oldStyleHeader->Header.ProcessId;
            eventData->EventHeader.TimeStamp = oldStyleHeader->Header.TimeStamp;
            eventData->EventHeader.ProviderId = oldStyleHeader->Header.Guid;            // ProviderId = TaskId
            // ID left 0
            eventData->EventHeader.Version = (byte)oldStyleHeader->Header.Version;
            // Channel
            eventData->EventHeader.Level = oldStyleHeader->Header.Level;
            eventData->EventHeader.Opcode = oldStyleHeader->Header.Type;
            // Task
            // Keyword
            eventData->EventHeader.KernelTime = oldStyleHeader->Header.KernelTime;
            eventData->EventHeader.UserTime = oldStyleHeader->Header.UserTime;
            // ActivityID

            eventData->BufferContext = oldStyleHeader->BufferContext;
            // ExtendedDataCount
            eventData->UserDataLength = (ushort)oldStyleHeader->MofLength;
            // ExtendedData
            eventData->UserData = oldStyleHeader->MofData;
            // UserContext 

            RawDispatch(eventData);
        }

        // [SecuritySafeCritical]
        [AllowReversePInvokeCalls]
        private void RawDispatch(TraceEventNativeMethods.EVENT_RECORD* rawData)
        {
            Debug.Assert(rawData->EventHeader.HeaderType == 0);     // if non-zero probably old-style ETW header
            TraceEvent anEvent = Lookup(rawData);

            // Keep in mind that for UnhandledTraceEvent 'PrepForCallback' has NOT been called, which means the
            // opcode, guid and eventIds are not correct at this point.  The ToString() routine WILL call
            // this so if that is in your debug window, it will have this side effect (which is good and bad)
            // Looking at rawData will give you the truth however. 
            anEvent.DebugValidate();

            // TODO FIX NOW, can we be more efficient?
            if (sessionStartTimeQPC == 0)
                sessionStartTimeQPC = rawData->EventHeader.TimeStamp;

            if (anEvent.FixupETLData != null)
                anEvent.FixupETLData();
            Dispatch(anEvent);
        }

        // [SecuritySafeCritical]
        protected override void Dispose(bool disposing)
        {
            stopProcessing = true;      
            if (handles != null)
            {
                foreach (ulong handle in handles)
                    if (handle != TraceEventNativeMethods.INVALID_HANDLE_VALUE)
                        TraceEventNativeMethods.CloseTrace(handle);
                handles = null;
            }
            logFiles = null;       
            base.Dispose(disposing);
            GC.SuppressFinalize(this);
        }

        ~ETWTraceEventSource()
        {
            Dispose(false);
        }

        private void Reset()
        {
            if (!CanReset)
                throw new InvalidOperationException("Event stream is not resetable (e.g. real time).");

            for (int i = 0; i < handles.Length; i++)
            {
                if (handles[i] != TraceEventNativeMethods.INVALID_HANDLE_VALUE)
                {
                    TraceEventNativeMethods.CloseTrace(handles[i]);
                    handles[i] = TraceEventNativeMethods.INVALID_HANDLE_VALUE;
                }
                // Annoying.  The OS resets the LogFileMode field, so I have to set it up again.   
                if (!useClassicETW)
                {
                    logFiles[i].LogFileMode = TraceEventNativeMethods.PROCESS_TRACE_MODE_EVENT_RECORD;
                    logFiles[i].LogFileMode |= TraceEventNativeMethods.PROCESS_TRACE_MODE_RAW_TIMESTAMP;
                }

                handles[i] = TraceEventNativeMethods.OpenTrace(ref logFiles[i]);

                if (handles[i] == TraceEventNativeMethods.INVALID_HANDLE_VALUE)
                    Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRForLastWin32Error());
            }
        }

        // Private data / methods 
        // [SecuritySafeCritical]
        [AllowReversePInvokeCalls]
        private bool TraceEventBufferCallback(IntPtr rawLogFile)
        {
            return !stopProcessing;
        }

        // #ETWTraceEventSourceFields
        private bool processTraceCalled;
        private TraceEventNativeMethods.EVENT_RECORD* convertedHeader;

        // Returned from OpenTrace
        private TraceEventNativeMethods.EVENT_TRACE_LOGFILEW[] logFiles;
        private UInt64[] handles;

        // We do minimal processing to keep track of process names (since they are REALLY handy). 
        private Dictionary<int, string> processNameForID;

        protected internal override string ProcessName(int processID, long time100ns)
        {
            string ret;
            if (!processNameForID.TryGetValue(processID, out ret))
                ret = "";
            return ret;
        }
        #endregion
    }

    /// <summary>
    /// The kinds of data sources that can be opened (see code:ETWTraceEventSource)
    /// </summary>
    public enum TraceEventSourceType
    {
        /// <summary>
        /// Look for any files like *.etl or *.*.etl (the later holds things like *.kernel.etl or *.clrRundown.etl ...)
        /// </summary>
        MergeAll,
        /// <summary>
        /// Look for a ETL moduleFile *.etl as the event data source 
        /// </summary>
        FileOnly,
        /// <summary>
        /// Use a real time session as the event data source.
        /// </summary>
        Session,
    };
}
