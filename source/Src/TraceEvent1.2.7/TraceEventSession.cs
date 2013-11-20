using Microsoft.Win32;
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Utilities;
using Diagnostics.Tracing.Parsers;

// TraceEventSession defintions See code:#Introduction to get started.
namespace Diagnostics.Tracing
{
    /// <summary>
    /// #Introduction 
    /// 
    /// A TraceEventSession represents a single ETW Tracing Session (something that logs a
    /// single output moduleFile). Every ETL output moduleFile has exactly one session assoicated with it,
    /// although you can have 'real time' sessions that have no output file and you can connect to
    /// 'directly' to get events without ever creating a file. You signify this simply by passing
    /// 'null' as the name of the file. You extract data from these 'real time' sources by specifying
    /// the session name to the constructor of code:ETWTraceEventSource). Sessions are MACHINE WIDE and can
    /// OUTLIVE the process that creates them. This it takes some care to insure that sessions are cleaned up
    /// in all cases.
    /// 
    /// Code that generated ETW events are called Providers. The Kernel has a provider (and it is often the
    /// most intersting) but other components are free to use public OS APIs (eg WriteEvent), to create
    /// user-mode providers. Each Provider is given a GUID that is used to identify it. You can get a list of
    /// all providers on the system as well as their GUIDs by typing the command
    /// 
    ///             logman query providers
    ///             
    /// The basic model is that you start a session (which creates a ETL moduleFile), and then you call
    /// code:TraceEventSession.EnableProvider on it to add all the providers (event sources), that you are
    /// interested in. A session is given a name (which is MACHINE WIDE), so that you can connect back up to
    /// it from another process (since it might outlive the process that created it), so you can modify it or
    /// (more commonly) close the session down later from another process.
    /// 
    /// For implementation reasons, this is only one Kernel provider and it can only be specified in a
    /// special 'Kernel Mode' session. There can be only one kernel mode session (MACHINE WIDE) and it is
    /// distinguished by a special name 'NT Kernel Logger'. The framework allows you to pass flags to the
    /// provider to control it and the Kernel provider uses these bits to indicate which particular events
    /// are of interest. Because of these restrictions, you often need two sessions, one for the kernel
    /// events and one for all user-mode events.
    /// 
    /// Sample use. Enabling the Kernel's DLL image logging to the moduleFile output.etl
    /// 
    ///  * TraceEventSession session = new TraceEventSession(, KernelTraceEventParser.Keywords.ImageLoad); 
    ///  * Run you scenario 
    ///  * session.Close(); 
    /// 
    /// Once the scenario is complete, you use the code:TraceEventSession.Close methodIndex to shut down a
    /// session. You can also use the code:TraceEventSession.GetActiveSessionNames to get a list of all
    /// currently running session on the machine (in case you forgot to close them!).
    /// 
    /// When the sesion is closed, you can use the code:ETWTraceEventSource to parse the events in the ETL
    /// moduleFile.  Alternatively, you can use code:TraceLog.CreateFromETL to convert the ETL file into an ETLX file. 
    /// Once it is an ETLX file you have a much richer set of processing options availabe from code:TraceLog. 
    /// </summary>
    // [SecuritySafeCritical]
    unsafe public sealed class TraceEventSession : IDisposable
    {
        /// <summary>
        /// Create a new logging session.
        /// </summary>
        /// <param name="sessionName">
        /// The name of the session. Since session can exist beyond the lifetime of the process this name is
        /// used to refer to the session from other threads.
        /// </param>
        /// <param name="fileName">
        /// The output moduleFile (by convention .ETL) to put the event data. If this parameter is null, it means
        /// that the data is 'real time' (stored in the session memory itself)
        /// </param>
        public TraceEventSession(string sessionName, string fileName)
        {
            this.m_BufferSizeMB = Math.Max(64, System.Environment.ProcessorCount * 2);       // The default size.  
            this.m_SessionHandle = TraceEventNativeMethods.INVALID_HANDLE_VALUE;
            this.m_FileName = fileName;               // filename = null means real time session
            this.m_SessionName = sessionName;
            this.m_Create = true;
            this.CpuSampleIntervalMSec = 1.0F;
        }
        /// <summary>
        /// Open an existing Windows Event Tracing Session, with name 'sessionName'. To create a new session,
        /// use TraceEventSession(string, string)
        /// </summary>
        /// <param name="sessionName"> The name of the session to open (see GetActiveSessionNames)</param>
        public TraceEventSession(string sessionName)
        {
            this.m_SessionHandle = TraceEventNativeMethods.INVALID_HANDLE_VALUE;
            this.m_SessionName = sessionName;
            this.CpuSampleIntervalMSec = 1.0F;

            // Get the filename
            var propertiesBuff = stackalloc byte[PropertiesSize];
            var properties = GetProperties(propertiesBuff);
            int hr = TraceEventNativeMethods.ControlTrace(0UL, sessionName, properties, TraceEventNativeMethods.EVENT_TRACE_CONTROL_QUERY);
            if (hr == 4201)     // Instance name not found.  This means we did not start
                throw new FileNotFoundException("The session " + sessionName + " is not active.");  // Not really a file, but not bad. 
            Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRFromWin32(hr));
            this.m_FileName = new string((char*)(((byte*)properties) + properties->LogFileNameOffset));
            this.m_BufferSizeMB = (int)properties->MinimumBuffers;
            if ((properties->LogFileMode & TraceEventNativeMethods.EVENT_TRACE_FILE_MODE_CIRCULAR) != 0)
                m_CircularBufferMB = (int)properties->MaximumFileSize;
        }

        public bool EnableKernelProvider(KernelTraceEventParser.Keywords flags)
        {
            return EnableKernelProvider(flags, KernelTraceEventParser.Keywords.None);
        }
        /// <summary>
        /// #EnableKernelProvider
        /// Enable the kernel provider for the session. If the session must be called 'NT Kernel Session'.   
        /// <param name="flags">
        /// Specifies the particular kernel events of interest</param>
        /// <param name="stackCapture">
        /// Specifies which events should have their eventToStack traces captured too (VISTA+ only)</param>
        /// <returns>Returns true if the session had existed before and is now restarted</returns>
        /// </summary>
        public unsafe bool EnableKernelProvider(KernelTraceEventParser.Keywords flags, KernelTraceEventParser.Keywords stackCapture)
        {
            bool systemTraceProvider = false;
            var version = Environment.OSVersion.Version.Major * 10 + Environment.OSVersion.Version.Minor;
            if (m_SessionName != KernelTraceEventParser.KernelSessionName)
            {
                systemTraceProvider = true;
                if (version < 62)
                    throw new NotSupportedException("System Tracing is only supported on Windows 8 and above.");
            }
            else
            {
                if (m_SessionHandle != TraceEventNativeMethods.INVALID_HANDLE_VALUE)
                    throw new Exception("The kernel provider must be enabled as the only provider.");
                if (version < 60)
                    throw new NotSupportedException("Kernel Event Tracing is only supported on Windows 6.0 (Vista) and above.");
            }

            // The Profile event requires the SeSystemProfilePrivilege to succeed, so set it.  
            if ((flags & (KernelTraceEventParser.Keywords.Profile | KernelTraceEventParser.Keywords.PMCProfile)) != 0)
            {
                TraceEventNativeMethods.SetSystemProfilePrivilege();
                // TODO FIX NOW never fails.  
                if (CpuSampleIntervalMSec != 1)
                {
                    if (!TraceEventNativeMethods.CanSetCpuSamplingRate())
                        throw new ApplicationException("Changing the CPU sampling rate is currently not supported on this OS.");
                }
                var cpu100ns = (CpuSampleIntervalMSec * 10000.0 + .5);
                // The API seems to have an upper bound of 1 second.  
                if (cpu100ns >= int.MaxValue || ((int)cpu100ns) > 10000000)
                    throw new ApplicationException("CPU Sampling rate is too high.");
                var succeeded = TraceEventNativeMethods.SetCpuSamplingRate((int)cpu100ns);       // Always try to set, since it may not be the default
                if (!succeeded && CpuSampleIntervalMSec != 1.0F)
                    throw new InvalidOperationException("Can't set CPU sampling to " + CpuSampleIntervalMSec.ToString("f3") + "Msec.");
            }

            var propertiesBuff = stackalloc byte[PropertiesSize];
            var properties = GetProperties(propertiesBuff);

            // Initialize the stack collecting information
            const int stackTracingIdsMax = 96;
            int numIDs = 0;
            var stackTracingIds = stackalloc TraceEventNativeMethods.STACK_TRACING_EVENT_ID[stackTracingIdsMax];
            if (stackCapture != KernelTraceEventParser.Keywords.None)
                numIDs = SetStackTraceIds(stackCapture, stackTracingIds, stackTracingIdsMax);

            bool ret = false;
            int dwErr;
            try
            {
                if (systemTraceProvider)
                {
                    properties->LogFileMode = properties->LogFileMode | TraceEventNativeMethods.EVENT_TRACE_SYSTEM_LOGGER_MODE;
                    InsureStarted(properties);

                    dwErr = TraceEventNativeMethods.TraceSetInformation(m_SessionHandle,
                                                                        TraceEventNativeMethods.TRACE_INFO_CLASS.TraceStackTracingInfo,
                                                                        stackTracingIds,
                                                                        (numIDs * sizeof(TraceEventNativeMethods.STACK_TRACING_EVENT_ID)));
                    Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRFromWin32(dwErr));

                    ulong* systemTraceFlags = stackalloc ulong[1];
                    systemTraceFlags[0] = (ulong)(flags & ~KernelTraceEventParser.Keywords.NonOSKeywords);
                    dwErr = TraceEventNativeMethods.TraceSetInformation(m_SessionHandle,
                                                                        TraceEventNativeMethods.TRACE_INFO_CLASS.TraceSystemTraceEnableFlagsInfo,
                                                                        systemTraceFlags,
                                                                        sizeof(ulong));
                    Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRFromWin32(dwErr));
                    ret = true;
                }
                else
                {
                    properties->Wnode.Guid = KernelTraceEventParser.ProviderGuid;
                    properties->EnableFlags = (uint)flags;

                    dwErr = StartKernelTrace(out m_SessionHandle, properties, stackTracingIds, numIDs);
                    if (dwErr == 0xB7) // STIERR_HANDLEEXISTS
                    {
                        ret = true;
                        Stop();
                        m_Stopped = false;
                        Thread.Sleep(100);  // Give it some time to stop. 
                        dwErr = StartKernelTrace(out m_SessionHandle, properties, stackTracingIds, numIDs);
                    }
                }
            }
            catch (BadImageFormatException)
            {
                // We use a small native DLL called KernelTraceControl that needs to be 
                // in the same directory as the EXE that used TraceEvent.dll.  Unlike IL
                // Native DLLs are specific to a processor type (32 or 64 bit) so the easiestC:\Users\vancem\Documents\etw\traceEvent\TraceEventSession.cs
                // way to insure this is that the EXE that uses TraceEvent is built for 32 bit
                // and that you use the 32 bit version of KernelTraceControl.dll
                throw new BadImageFormatException("Could not load KernelTraceControl.dll (likely 32-64 bit process mismatch)");
            }
            catch (DllNotFoundException)
            {
                // In order to start kernel session, we need a support DLL called KernelTraceControl.dll
                // This DLL is available by downloading the XPERF.exe tool (see 
                // http://msdn.microsoft.com/en-us/performance/cc825801.aspx for instructions)
                // It is recommended that you get the 32 bit version of this (it works on 64 bit machines)
                // and build your EXE that uses TraceEvent to launch as a 32 bit application (This is
                // the default for VS 2010 projects).  
                throw new DllNotFoundException("KernelTraceControl.dll missing from distribution.");
            }
            if (dwErr == 5 && Environment.OSVersion.Version.Major > 5)      // On Vista and we get a 'Accessed Denied' message
                throw new UnauthorizedAccessException("Error Starting ETW:  Access Denied (Administrator rights required to start ETW)");
            Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRFromWin32(dwErr));
            m_IsActive = true;

            if (version >= 62 && StackCompression)
                TraceEventNativeMethods.EnableStackCaching(m_SessionHandle);
            return ret;
        }

        /// <summary>
        /// EventSources have a convention for converting its name to a GUID.  Use this convention to 
        /// convert 'name' to a GUID.  
        /// </summary>
        public static Guid GetEventSourceGuidFromName(string name)
        {
            name = name.ToUpperInvariant();     // names are case insenstive.  

            // The algorithm below is following the guidance of http://www.ietf.org/rfc/rfc4122.txt
            // Create a blob containing a 16 byte number representing the namespace
            // followed by the unicode bytes in the name.  
            var bytes = new byte[name.Length * 2 + 16];
            uint namespace1 = 0x482C2DB2;
            uint namespace2 = 0xC39047c8;
            uint namespace3 = 0x87F81A15;
            uint namespace4 = 0xBFC130FB;
            // Write the bytes most-significant byte first.  
            for (int i = 3; 0 <= i; --i)
            {
                bytes[i] = (byte)namespace1;
                namespace1 >>= 8;
                bytes[i + 4] = (byte)namespace2;
                namespace2 >>= 8;
                bytes[i + 8] = (byte)namespace3;
                namespace3 >>= 8;
                bytes[i + 12] = (byte)namespace4;
                namespace4 >>= 8;
            }
            // Write out  the name, most significant byte first
            for (int i = 0; i < name.Length; i++)
            {
                bytes[2 * i + 16 + 1] = (byte)name[i];
                bytes[2 * i + 16] = (byte)(name[i] >> 8);
            }

            // Compute the Sha1 hash 
            var sha1 = System.Security.Cryptography.SHA1.Create();
            byte[] hash = sha1.ComputeHash(bytes);

            // Create a GUID out of the first 16 bytes of the hash (SHA-1 create a 20 byte hash)
            int a = (((((hash[3] << 8) + hash[2]) << 8) + hash[1]) << 8) + hash[0];
            short b = (short)((hash[5] << 8) + hash[4]);
            short c = (short)((hash[7] << 8) + hash[6]);

            c = (short)((c & 0x0FFF) | 0x5000);   // Set high 4 bits of octet 7 to 5, as per RFC 4122
            Guid guid = new Guid(a, b, c, hash[8], hash[9], hash[10], hash[11], hash[12], hash[13], hash[14], hash[15]);
            return guid;
        }

        /// <summary>
        /// Add an additional USER MODE provider prepresented by 'providerGuid' (a list of
        /// providers is available by using 'logman query providers').
        /// </summary>
        /// <param name="providerGuid">
        /// The GUID that represents the event provider to turn on. Use 'logman query providers' or
        /// for a list of possible providers. Note that additional user mode (but not kernel mode)
        /// providers can be added to the session by using EnableProvider.</param>
        /// <param name="providerLevel">The verbosity to turn on</param>
        /// <param name="matchAnyKeywords">A bitvector representing the areas to turn on. Only the
        /// low 32 bits are used by classic providers and passed as the 'flags' value.  Zero
        /// is a special value which is a provider defined default, which is usuall 'everything'</param>
        /// <param name="matchAllKeywords">A bitvector representing keywords of an event that must
        /// be on for a particular event for the event to be logged.  A value of zero means
        /// that no keyword must be on, which effectively ignores this value.  </param>
        /// <param name="options">Additional options for the provider (e.g. taking a stack trace)</param>
        /// <param name="values">This is set of key-value strings that are passed to the provider
        /// for provider-specific interpretation. Can be null if no additional args are needed.  
        /// If the special key-value pair 'Command'='SendManifest' is provided, then the 'SendManifest'
        /// command will be sent (which causes EventSources to redump their manifest to the ETW log.  </param>
        /// <returns>true if the session already existed and needed to be restarted.</returns>
        public bool EnableProvider(Guid providerGuid, TraceEventLevel providerLevel = TraceEventLevel.Verbose, ulong matchAnyKeywords = ulong.MaxValue, ulong matchAllKeywords = 0, TraceEventOptions options = 0, IEnumerable<KeyValuePair<string, string>> values = null)
        {
            byte[] valueData = null;
            int valueDataSize = 0;
            int valueDataType = 0;
            if (values != null)
            {
                valueDataType = 0; // ControllerCommand.Update  // TODO use enumeration
                valueData = new byte[1024];
                foreach (KeyValuePair<string, string> keyValue in values)
                {
                    if (keyValue.Key == "Command")
                    {
                        if (keyValue.Value == "SendManifest")
                            valueDataType = -1; // ControllerCommand.SendManifest
                        else
                        {
                            int val;
                            if (int.TryParse(keyValue.Value, out val))
                                valueDataType = val;
                        }
                    }
                    valueDataSize += Encoding.UTF8.GetBytes(keyValue.Key, 0, keyValue.Key.Length, valueData, valueDataSize);
                    if (valueDataSize >= 1023)
                        throw new Exception("Too much provider data");  // TODO better message. 
                    valueData[valueDataSize++] = 0;
                    valueDataSize += Encoding.UTF8.GetBytes(keyValue.Value, 0, keyValue.Value.Length, valueData, valueDataSize);
                    if (valueDataSize >= 1023)
                        throw new Exception("Too much provider data");  // TODO better message. 
                    valueData[valueDataSize++] = 0;
                }
            }
            return EnableProvider(providerGuid, providerLevel, matchAnyKeywords, matchAllKeywords, options, valueDataType, valueData, valueDataSize);
        }

        /// <summary>
        /// Disables a provider completely
        /// </summary>
        public void DisableProvider(Guid providerGuid)
        {
            int hr;
            try
            {
                try
                {
                    // Try the Win7 API
                    var parameters = new TraceEventNativeMethods.ENABLE_TRACE_PARAMETERS { Version = TraceEventNativeMethods.ENABLE_TRACE_PARAMETERS_VERSION };
                    hr = TraceEventNativeMethods.EnableTraceEx2(
                        m_SessionHandle, ref providerGuid, TraceEventNativeMethods.EVENT_CONTROL_CODE_DISABLE_PROVIDER,
                        0, 0, 0, 0, ref parameters);
                }
                catch (EntryPointNotFoundException)
                {
                    // OK that did not work, try the VISTA API
                    hr = TraceEventNativeMethods.EnableTraceEx(ref providerGuid, null, m_SessionHandle, 0, 0, 0, 0, 0, null);
                }
            }
            catch (EntryPointNotFoundException)
            {
                // Try with the old pre-vista API
                hr = TraceEventNativeMethods.EnableTrace(0, 0, 0, ref providerGuid, m_SessionHandle);
            }
            Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRFromWin32(hr));
        }
        /// <summary>
        /// Once started, event sessions will persist even after the process that created them dies. They are
        /// only stoped by this explicit Stop() API. 
        /// </summary>
        public bool Stop(bool noThrow = false)
        {
            if (m_Stopped)
                return true;
            m_Stopped = true;
            TraceEventNativeMethods.SetCpuSamplingRate(10000);      // Set sample rate back to default 1 Msec 
            var propertiesBuff = stackalloc byte[PropertiesSize];
            var properties = GetProperties(propertiesBuff);
            int hr = TraceEventNativeMethods.ControlTrace(0UL, m_SessionName, properties, TraceEventNativeMethods.EVENT_TRACE_CONTROL_STOP);

            if (hr != 4201)     // Instance name not found.  This means we did not start
            {
                if (!noThrow)
                    Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRFromWin32(hr));
                return false;   // Stop failed
            }

            return true;
        }
        /// <summary>
        /// Sends the CAPUTURE_STATE command to the provider 
        /// 
        /// This routine only works Win7 and above, since previous versions don't have this concept.   The providers also has 
        /// to support it.  
        /// 
        /// You can use KernelTraceEventParser.ProviderGuid here to cause rundown for the system.  
        /// </summary>
        /// <param name="providerGuid">The GUID that identifies the provider to send the CaptureState command to</param>
        /// <param name="matchAnyKeywords">The Keywords to send as part of the command (can influnced what is sent back)</param>
        /// <param name="filterType">if non-zero, this is passed along to the provider as type of the filter data.</param>
        /// <param name="data">If non-null this is either an int, or a byte array and is passed along as filter data.</param>
        public void CaptureState(Guid providerGuid, ulong matchAnyKeywords = ulong.MaxValue, int filterType = 0, object data = null)
        {
            var parameters = new TraceEventNativeMethods.ENABLE_TRACE_PARAMETERS();
            var filter = new TraceEventNativeMethods.EVENT_FILTER_DESCRIPTOR();
            parameters.Version = TraceEventNativeMethods.ENABLE_TRACE_PARAMETERS_VERSION;

            byte[] asArray = data as byte[];
            if (data is int)
            {
                int intVal = (int)data;
                asArray = new byte[4];
                asArray[0] = (byte)intVal;
                asArray[1] = (byte)(intVal >> 8);
                asArray[2] = (byte)(intVal >> 16);
                asArray[3] = (byte)(intVal >> 24);
            }
            fixed (byte* filterDataPtr = asArray)
            {
                if (asArray != null)
                {
                    parameters.EnableFilterDesc = &filter;
                    filter.Type = filterType;
                    filter.Size = asArray.Length;
                    filter.Ptr = filterDataPtr;
                }
                int hr = TraceEventNativeMethods.EnableTraceEx2(
                    m_SessionHandle, ref providerGuid, TraceEventNativeMethods.EVENT_CONTROL_CODE_CAPTURE_STATE,
                    (byte)TraceEventLevel.Verbose, matchAnyKeywords, 0, 0, ref parameters);

                Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRFromWin32(hr));
            }
        }

        /// <summary>
        /// Cause the log to be a circular buffer.  The buffer size (in MegaBytes) is the value of this property.
        /// Setting this to 0 will cause it to revert to non-circular mode.  This routine can only be called BEFORE
        /// a provider is enabled.  
        /// </summary>
        public int CircularBufferMB
        {
            get { return m_CircularBufferMB; }
            set
            {
                if (IsActive)
                    throw new InvalidOperationException("Property can't be changed after A provider has started.");
                if (m_FileName == null)
                    throw new InvalidOperationException("Circular buffers only allowed on sessions with files.");
                m_CircularBufferMB = value;
            }

        }
        /// <summary>
        /// Sets the size of the buffer the operating system should reserve to avoid lost packets.   Starts out 
        /// as a very generous 32MB for files.  If events are lost, this can be increased.  
        /// </summary>
        public int BufferSizeMB
        {
            get { return m_BufferSizeMB; }
            set
            {
                if (IsActive)
                    throw new InvalidOperationException("Property can't be changed after A provider has started.");
                m_BufferSizeMB = value;
            }
        }
        /// <summary>
        /// If set then Stop() will be called automatically when this object is Disposed or GCed (which
        /// will happen on program exit unless a unhandled exception occurs.  
        /// </summary>
        public bool StopOnDispose { get { return m_StopOnDispose; } set { m_StopOnDispose = value; } }
        /// <summary>
        /// The name of the session that can be used by other threads to attach to the session. 
        /// </summary>
        public string SessionName
        {
            get { return m_SessionName; }
        }
        /// <summary>
        /// The name of the moduleFile that events are logged to.  Null means the session is real time. 
        /// </summary>
        public string FileName
        {
            get
            {
                return m_FileName;
            }
        }
        /// <summary>
        /// Creating a TraceEventSession does not actually interact with the operating system until a
        /// provider is enabled. At that point the session is considered active (OS state that survives a
        /// process exit has been modified). IsActive returns true if the session is active.
        /// </summary>
        public bool IsActive
        {
            get
            {
                return m_IsActive;
            }
        }
        /// <summary>
        /// The rate at which CPU samples are collected.  By default this is 1 (once a millisecond per CPU).
        /// There is alower bound on this (typically .125 Msec)
        /// </summary>
        public float CpuSampleIntervalMSec { get; set; }
        /// <summary>
        /// Indicate that this session should use compress the stacks to save space.  
        /// Must be set before any providers are enabled.  Currently only works for kernel events.  
        /// TODO FIX NOW untested.  
        /// </summary>
        public bool StackCompression { get; set; }

        // OS Heap Provider support.  
        /// <summary>
        /// Turn on windows heap logging (stack for allocation) for a particular existing process.
        /// </summary>
        public void EnableWindowsHeapProvider(int pid)
        {
            throw new NotSupportedException("This version of PerfView does not support collection of OS Heap events.");
        }
        /// <summary>
        /// Turn on windows heap logging for a particular EXE file name (just the file name, no directory, but it DOES include the .exe extension)
        /// </summary>
        /// <param name="exeFileName"></param>
        public void EnableWindowsHeapProvider(string exeFileName)
        {
            throw new NotSupportedException("This version of PerfView does not support collection of OS Heap events.");
        }

        // CPU counter support 
        /// <summary>
        /// Returned by GetProfileSourceInfo, describing the CPU counter (ProfileSource) available on the machine. 
        /// </summary>
        public class ProfileSourceInfo
        {
            public string Name;             // Human readable name of the CPU performance counter (eg BranchInstructions, TotalIssues ...)
            public int ID;                  // The ID that can be passed to SetProfileSources
            public int Interval;            // This many events are skipped for each sample that is actually recorded
            public int MinInterval;         // The smallest Interval can be (typically 4K)
            public int MaxInterval;         // The largest Interval can be (typically maxInt).  
        }
        /// <summary>
        /// Returns a ditionary of keyed by name of ProfileSourceInfo structures for all the CPU counters available on the machine. 
        /// TODO FIX NOW remove log parameter. 
        /// </summary>
        public static unsafe Dictionary<string, ProfileSourceInfo> GetProfileSourceInfo()
        {
            var version = Environment.OSVersion.Version.Major * 10 + Environment.OSVersion.Version.Minor;
            if (version < 62)
                throw new ApplicationException("Profile source only availabe on Win8 and beyond.");

            var ret = new Dictionary<string, ProfileSourceInfo>(StringComparer.OrdinalIgnoreCase);

            // Figure out how much space we need.  
            int retLen = 0;
            var result = TraceEventNativeMethods.TraceQueryInformation(0,
                TraceEventNativeMethods.TRACE_INFO_CLASS.TraceProfileSourceListInfo,
                null, 0, ref retLen);
            Debug.Assert(result == 24);     // Not enough space.  
            if (retLen != 0)
            {
                // Do it for real.  
                byte* buffer = stackalloc byte[retLen];
                result = TraceEventNativeMethods.TraceQueryInformation(0,
                    TraceEventNativeMethods.TRACE_INFO_CLASS.TraceProfileSourceListInfo,
                    buffer, retLen, ref retLen);

                if (result == 0)
                {
                    var interval = new TraceEventNativeMethods.TRACE_PROFILE_INTERVAL();
                    var profileSource = (TraceEventNativeMethods.PROFILE_SOURCE_INFO*)buffer;
                    for (int i = 0; i < 10; i++)
                    {
                        char* namePtr = (char*)&profileSource[1];       // points off the end of the array;

                        interval.Source = profileSource->Source;
                        interval.Interval = 0;
                        result = TraceEventNativeMethods.TraceQueryInformation(0,
                            TraceEventNativeMethods.TRACE_INFO_CLASS.TraceSampledProfileIntervalInfo,
                            &interval, sizeof(TraceEventNativeMethods.TRACE_PROFILE_INTERVAL), ref retLen);
                        if (result != 0)
                            Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRFromWin32(result));

                        var name = new string(namePtr);
                        ret.Add(name, new ProfileSourceInfo()
                        {
                            Name = name,
                            ID = profileSource->Source,
                            Interval = interval.Interval,
                            MinInterval = profileSource->MinInterval,
                            MaxInterval = profileSource->MaxInterval,
                        });
                        if (profileSource->NextEntryOffset == 0)
                            break;
                        profileSource = (TraceEventNativeMethods.PROFILE_SOURCE_INFO*)(profileSource->NextEntryOffset + (byte*)profileSource);
                    }
                }
                else
                    Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRFromWin32(result));
            }
            return ret;
        }
        /// <summary>
        /// Sets the Profile Sources (CPU machine counters) that will be used if PMC (Precise Machine Counters)
        /// are turned on.   Each CPU counter is given a id (the profileSourceID) and has an interval 
        /// (the number of counts you skip for each event you log).   You can get the human name for 
        /// all the supported CPU counters by calling GetProfileSourceInfo.  Then choose the ones you want
        /// and configure them here (the first array indicating the CPU counters to enable, and the second
        /// array indicating the interval.  The second array can be shorter then the first, in which case
        /// the existing interval is used (it persists and has a default on boot).  
        /// </summary>
        public static unsafe void SetProfileSources(int[] profileSourceIDs, int[] profileSourceIntervals)
        {
            var version = Environment.OSVersion.Version.Major * 10 + Environment.OSVersion.Version.Minor;
            if (version < 62)
                throw new ApplicationException("Profile source only availabe on Win8 and beyond.");

            TraceEventNativeMethods.SetSystemProfilePrivilege();
            var interval = new TraceEventNativeMethods.TRACE_PROFILE_INTERVAL();
            for (int i = 0; i < profileSourceIntervals.Length; i++)
            {
                interval.Source = profileSourceIDs[i];
                interval.Interval = profileSourceIntervals[i];
                var result = TraceEventNativeMethods.TraceSetInformation(0,
                    TraceEventNativeMethods.TRACE_INFO_CLASS.TraceSampledProfileIntervalInfo,
                    &interval, sizeof(TraceEventNativeMethods.TRACE_PROFILE_INTERVAL));
                if (result != 0)
                    Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRFromWin32(result));
            }

            fixed (int* sourcesPtr = profileSourceIDs)
            {
                var result = TraceEventNativeMethods.TraceSetInformation(0,
                    TraceEventNativeMethods.TRACE_INFO_CLASS.TraceProfileSourceConfigInfo,
                    sourcesPtr, profileSourceIDs.Length * sizeof(int));
                if (result != 0)
                    Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRFromWin32(result));
            }
        }

        // Post processing (static methods)
        /// <summary>
        /// It is sometimes useful to merge the contents of several ETL files into a single 
        /// output ETL file.   This routine does that.  It also will attach additional 
        /// information that will allow correct file name and symbolic lookup if the 
        /// ETL file is used on a machine other than the one that the data was collected on.
        /// If you wish to transport the file to another machine you need to merge them.
        /// </summary>
        /// <param name="inputETLFileNames"></param>
        /// <param name="outputETLFileName"></param>
        public static void Merge(string[] inputETLFileNames, string outputETLFileName)
        {
            IntPtr state = IntPtr.Zero;

            // If we happen to be in the WOW, disable file system redirection as you don't get the System32 dlls otherwise. 
            bool disableRedirection = TraceEventNativeMethods.Wow64DisableWow64FsRedirection(ref state);
            try
            {
                Debug.Assert(disableRedirection || System.Runtime.InteropServices.Marshal.SizeOf(typeof(IntPtr)) == 8);

                int retValue = TraceEventNativeMethods.CreateMergedTraceFile(
                    outputETLFileName, inputETLFileNames, inputETLFileNames.Length,
                        TraceEventNativeMethods.EVENT_TRACE_MERGE_EXTENDED_DATA.IMAGEID |
                        TraceEventNativeMethods.EVENT_TRACE_MERGE_EXTENDED_DATA.BUILDINFO |
                        TraceEventNativeMethods.EVENT_TRACE_MERGE_EXTENDED_DATA.WINSAT |
                        TraceEventNativeMethods.EVENT_TRACE_MERGE_EXTENDED_DATA.EVENT_METADATA |
                        TraceEventNativeMethods.EVENT_TRACE_MERGE_EXTENDED_DATA.VOLUME_MAPPING);
                if (retValue != 0)
                    throw new ApplicationException("Merge operation failed.");
            }
            finally
            {
                if (disableRedirection)
                    TraceEventNativeMethods.Wow64RevertWow64FsRedirection(state);
            }
        }
        /// <summary>
        /// This variation of the Merge command takes the 'primary' etl file name (X.etl)
        /// and will merge in any files that match .clr*.etl .user*.etl. and .kernel.etl.  
        /// </summary>
        public static void MergeInPlace(string etlFileName, TextWriter log)
        {
            var dir = Path.GetDirectoryName(etlFileName);
            if (dir.Length == 0)
                dir = ".";
            var baseName = Path.GetFileNameWithoutExtension(etlFileName);
            List<string> mergeInputs = new List<string>();
            mergeInputs.Add(etlFileName);
            mergeInputs.AddRange(Directory.GetFiles(dir, baseName + ".kernel.etl"));
            mergeInputs.AddRange(Directory.GetFiles(dir, baseName + ".clr*.etl"));
            mergeInputs.AddRange(Directory.GetFiles(dir, baseName + ".user*.etl"));

            string tempName = Path.ChangeExtension(etlFileName, ".etl.new");
            try
            {
                // Do the merge;
                Merge(mergeInputs.ToArray(), tempName);

                // Delete the originals.  
                foreach (var mergeInput in mergeInputs)
                    FileUtilities.ForceDelete(mergeInput);

                // Place the output in its final resting place.  
                FileUtilities.ForceMove(tempName, etlFileName);
            }
            finally
            {
                // Insure we clean up.  
                if (File.Exists(tempName))
                    File.Delete(tempName);
            }
        }

        // Session Discovery 
        /// <summary>
        /// ETW trace sessions survive process shutdown. Thus you can attach to existing active sessions.
        /// GetActiveSessionNames() returns a list of currently existing session names.  These can be passed
        /// to the code:TraceEventSession constructor to control it.   
        /// </summary>
        /// <returns>A enumeration of strings, each of which is a name of a session</returns>
        public unsafe static IEnumerable<string> GetActiveSessionNames()
        {
            const int MAX_SESSIONS = 64;
            int sizeOfProperties = sizeof(TraceEventNativeMethods.EVENT_TRACE_PROPERTIES) +
                                   sizeof(char) * MaxNameSize +     // For log moduleFile name 
                                   sizeof(char) * MaxNameSize;      // For session name

            byte* sessionsArray = stackalloc byte[MAX_SESSIONS * sizeOfProperties];
            TraceEventNativeMethods.EVENT_TRACE_PROPERTIES** propetiesArray = stackalloc TraceEventNativeMethods.EVENT_TRACE_PROPERTIES*[MAX_SESSIONS];

            for (int i = 0; i < MAX_SESSIONS; i++)
            {
                TraceEventNativeMethods.EVENT_TRACE_PROPERTIES* properties = (TraceEventNativeMethods.EVENT_TRACE_PROPERTIES*)&sessionsArray[sizeOfProperties * i];
                properties->Wnode.BufferSize = (uint)sizeOfProperties;
                properties->LoggerNameOffset = (uint)sizeof(TraceEventNativeMethods.EVENT_TRACE_PROPERTIES);
                properties->LogFileNameOffset = (uint)sizeof(TraceEventNativeMethods.EVENT_TRACE_PROPERTIES) + sizeof(char) * MaxNameSize;
                propetiesArray[i] = properties;
            }
            int sessionCount = 0;
            int hr = TraceEventNativeMethods.QueryAllTraces((IntPtr)propetiesArray, MAX_SESSIONS, ref sessionCount);
            Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRFromWin32(hr));

            List<string> activeTraceNames = new List<string>();
            for (int i = 0; i < sessionCount; i++)
            {
                byte* propertiesBlob = (byte*)propetiesArray[i];
                string sessionName = new string((char*)(&propertiesBlob[propetiesArray[i]->LoggerNameOffset]));
                activeTraceNames.Add(sessionName);
            }
            return activeTraceNames;
        }

        // Dicovering providers and their Keywords
        /// <summary>
        /// Returns the names of every registered provider on the system.   This is a long list (1000s of entries.  
        /// You can get its Guid with GetProviderByName.  
        /// </summary>
        public static IEnumerable<string> RegisteredProviders
        {
            get
            {
                return ProviderNameToGuid.Keys;
            }
        }
        /// <summary>
        /// Returns a list of provider GUIDs that are registered in a process with 'processID'.   
        /// This is a nice way to filter down the providers you might care about. 
        /// </summary>
        public static List<Guid> ProvidersInProcess(int processID)
        {
            var ret = new List<Guid>();
            // For every provider 
            foreach (var guid in ProviderNameToGuid.Values)
            {
                // See what process it is in.  
                int buffSize = 0;
                Guid localGuid = guid;
                var hr = TraceEventNativeMethods.EnumerateTraceGuidsEx(TraceEventNativeMethods.TRACE_QUERY_INFO_CLASS.TraceGuidQueryInfo,
                    &localGuid, sizeof(Guid), null, 0, ref buffSize);
                if (hr != 122)
                    continue;           // TODO should we be ignoring errors?

                Debug.Assert(hr == 122);     // ERROR_INSUFFICIENT_BUFFER
                var buffer = stackalloc byte[buffSize];
                hr = TraceEventNativeMethods.EnumerateTraceGuidsEx(TraceEventNativeMethods.TRACE_QUERY_INFO_CLASS.TraceGuidQueryInfo,
                    &localGuid, sizeof(Guid), buffer, buffSize, ref buffSize);
                if (hr != 0)
                    throw new InvalidOperationException("TraceGuidQueryInfo failed.");       // TODO better error message

                var providerInfos = (TraceEventNativeMethods.TRACE_GUID_INFO*)buffer;
                var provider = (TraceEventNativeMethods.TRACE_PROVIDER_INSTANCE_INFO*)&providerInfos[1];
                for (int i = 0; i < providerInfos->InstanceCount; i++)
                {
                    if (provider->Pid == processID)
                    {
                        ret.Add(guid);
                        break;      // We can go on since we found what we were looking for. 
                    }
                    if (provider->NextOffset == 0)
                        break;
                    Debug.Assert(0 <= provider->NextOffset && provider->NextOffset < buffSize);
                    var structBase = (byte*)provider;
                    provider = (TraceEventNativeMethods.TRACE_PROVIDER_INSTANCE_INFO*)&structBase[provider->NextOffset];
                }
            }
            return ret;
        }

        /// <summary>
        /// Given the friendly name of a provider (e.g. Microsoft-Windows-DotNETRuntimeStress) return the
        /// GUID for the provider.  Returns Guid.Empty on failure.   
        /// </summary>
        public static Guid GetProviderByName(string name)
        {
            Guid ret;
            ProviderNameToGuid.TryGetValue(name, out ret);
            return ret;
        }
        /// <summary>
        /// Finds the friendly name for 'providerGuid'  Returns the Guid as a string if can't be found
        /// </summary>
        public static string GetProviderName(Guid providerGuid)
        {
            string ret;
            ProviderGuidToName.TryGetValue(providerGuid, out ret);
            if (ret == null)
                ret = providerGuid.ToString();
            return ret;
        }
        /// <summary>
        /// Returns the keywords the provider represented by 'providerGuid' supports. 
        /// </summary>
        public static List<ProviderDataItem> GetProviderKeywords(Guid providerGuid)
        {
            return GetProviderFields(providerGuid, TraceEventNativeMethods.EVENT_FIELD_TYPE.EventKeywordInformation);
        }

        // Misc
        /// <summary>
        /// Is the current process Elevated (allowed to turn on a ETW provider).   Does not really belong here
        /// but it useful since ETW does need to be elevated.  
        /// </summary>
        /// <returns></returns>
        public static bool? IsElevated() { return TraceEventNativeMethods.IsElevated(); }

        #region Private
        /// <summary>
        /// Returns a sorted dictionary of  names and Guids for every provider registered on the system.   
        /// </summary>
        private static SortedDictionary<string, Guid> ProviderNameToGuid
        {
            get
            {
                if (s_providersByName == null)
                {
                    s_providersByName = new SortedDictionary<string, Guid>();
                    int buffSize = 0;
                    var hr = TraceEventNativeMethods.TdhEnumerateProviders(null, ref buffSize);
                    Debug.Assert(hr == 122);     // ERROR_INSUFFICIENT_BUFFER
                    var buffer = stackalloc byte[buffSize];
                    var providersDesc = (TraceEventNativeMethods.PROVIDER_ENUMERATION_INFO*)buffer;

                    hr = TraceEventNativeMethods.TdhEnumerateProviders(providersDesc, ref buffSize);
                    if (hr != 0)
                        throw new InvalidOperationException("TdhEnumerateProviders failed.");       // TODO better error message

                    var providers = (TraceEventNativeMethods.TRACE_PROVIDER_INFO*)&providersDesc[1];
                    for (int i = 0; i < providersDesc->NumberOfProviders; i++)
                    {
                        var name = new string((char*)&buffer[providers[i].ProviderNameOffset]);
                        s_providersByName[name] = providers[i].ProviderGuid;
                    }
                }
                return s_providersByName;
            }
        }

        private static Dictionary<Guid, string> ProviderGuidToName
        {
            get
            {
                if (s_providerNames == null)
                {
                    foreach (var keyValue in ProviderNameToGuid)
                        s_providerNames[keyValue.Value] = keyValue.Key;
                }
                return s_providerNames;
            }
        }

        static SortedDictionary<string, Guid> s_providersByName;
        static Dictionary<Guid, string> s_providerNames;

        private static List<ProviderDataItem> GetProviderFields(Guid providerGuid, TraceEventNativeMethods.EVENT_FIELD_TYPE fieldType)
        {
            var ret = new List<ProviderDataItem>();

            int buffSize = 0;
            var hr = TraceEventNativeMethods.TdhEnumerateProviderFieldInformation(ref providerGuid, fieldType, null, ref buffSize);
            if (hr != 122)
                return ret;     // TODO FIX NOW Do I want to simply return nothing or give a more explicit error? 
            Debug.Assert(hr == 122);     // ERROR_INSUFFICIENT_BUFFER 

            var buffer = stackalloc byte[buffSize];
            var fieldsDesc = (TraceEventNativeMethods.PROVIDER_FIELD_INFOARRAY*)buffer;
            hr = TraceEventNativeMethods.TdhEnumerateProviderFieldInformation(ref providerGuid, fieldType, fieldsDesc, ref buffSize);
            if (hr != 0)
                throw new InvalidOperationException("TdhEnumerateProviderFieldInformation failed.");       // TODO better error message

            var fields = (TraceEventNativeMethods.PROVIDER_FIELD_INFO*)&fieldsDesc[1];
            for (int i = 0; i < fieldsDesc->NumberOfElements; i++)
            {
                var field = new ProviderDataItem();
                field.Name = new string((char*)&buffer[fields[i].NameOffset]);
                field.Description = new string((char*)&buffer[fields[i].DescriptionOffset]);
                field.Value = fields[i].Value;
                ret.Add(field);
            }

            return ret;
        }

        /// <summary>
        /// We wrap this because sadly the PMC suppport is private, so we have to do it a different way if that is present.
        /// </summary>
        int StartKernelTrace(
            out UInt64 TraceHandle,
            TraceEventNativeMethods.EVENT_TRACE_PROPERTIES* properties,
            TraceEventNativeMethods.STACK_TRACING_EVENT_ID* stackTracingEventIds,
            int cStackTracingEventIds)
        {
            //bool needExtensions = false;  // SLAB fix
            if ((((KernelTraceEventParser.Keywords)properties->EnableFlags) & KernelTraceEventParser.Keywords.PMCProfile) != 0)
                throw new ApplicationException("CPU Counter profiling not supported.");

            properties->EnableFlags = properties->EnableFlags & (uint)~KernelTraceEventParser.Keywords.NonOSKeywords;
            return TraceEventNativeMethods.StartKernelTrace(out TraceHandle, properties, stackTracingEventIds, cStackTracingEventIds);
        }

        private const int maxStackTraceProviders = 256;
        /// <summary>
        /// The 'properties' field is only the header information.  There is 'tail' that is 
        /// required.  'ToUnmangedBuffer' fills in this tail properly. 
        /// </summary>
        ~TraceEventSession()
        {
            Dispose();
        }
        public void Dispose()
        {
            if (m_StopOnDispose)
                Stop(true);

            // TODO FIX NOW need safe handles
            if (m_SessionHandle != TraceEventNativeMethods.INVALID_HANDLE_VALUE)
                TraceEventNativeMethods.CloseTrace(m_SessionHandle);
            m_SessionHandle = TraceEventNativeMethods.INVALID_HANDLE_VALUE;

            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Do intialization common to the contructors.  
        /// </summary>
        private bool EnableProvider(Guid providerGuid, TraceEventLevel providerLevel, ulong matchAnyKeywords, ulong matchAllKeywords, TraceEventOptions options, int providerDataType, byte[] providerData, int providerDataSize)
        {
            if (m_SessionName == KernelTraceEventParser.KernelSessionName)
                throw new NotSupportedException("Can only enable kernel events on a kernel session.");

            bool ret = InsureStarted();
            TraceEventNativeMethods.EVENT_FILTER_DESCRIPTOR* dataDescrPtr = null;
            fixed (byte* providerDataPtr = providerData)
            {
                string regKeyName = @"Software\Microsoft\Windows\CurrentVersion\Winevt\Publishers\{" + providerGuid + "}";
                byte[] registryData = null;
                // If this is an update operation, remember the data in registry so that even providers
                // that have not yet started will get the data.   We don't do this for any other kind of command (providerDataType)
                // since we don't know that they are desired 'on startup'.  
                if (providerData != null && providerDataType == 0)
                {
                    TraceEventNativeMethods.EVENT_FILTER_DESCRIPTOR dataDescr = new TraceEventNativeMethods.EVENT_FILTER_DESCRIPTOR();
                    dataDescr.Ptr = null;
                    dataDescr.Size = providerDataSize;
                    dataDescr.Type = providerDataType;
                    dataDescrPtr = &dataDescr;

                    if (providerData == null)
                        providerData = new byte[0];
                    else
                        dataDescr.Ptr = providerDataPtr;

                    // Set the registry key so providers get the information even if they are not active now
                    registryData = new byte[providerDataSize + 4];
                    // providerDataType is always zero, but older versions assume it is here, so we put the redundant value here for compatibility. 
                    registryData[0] = (byte)(providerDataType);
                    registryData[1] = (byte)(providerDataType >> 8);
                    registryData[2] = (byte)(providerDataType >> 16);
                    registryData[3] = (byte)(providerDataType >> 24);
                    Array.Copy(providerData, 0, registryData, 4, providerDataSize);
                }
                SetOrDelete(regKeyName, "ControllerData", registryData);
                int hr;

                try
                {
                    try
                    {
                        // Try the Win7 API
                        TraceEventNativeMethods.ENABLE_TRACE_PARAMETERS parameters = new TraceEventNativeMethods.ENABLE_TRACE_PARAMETERS();
                        parameters.Version = TraceEventNativeMethods.ENABLE_TRACE_PARAMETERS_VERSION;
                        if ((options & TraceEventOptions.Stacks) != 0)
                            parameters.EnableProperty = TraceEventNativeMethods.EVENT_ENABLE_PROPERTY_STACK_TRACE;
                        parameters.EnableFilterDesc = dataDescrPtr;

                        hr = TraceEventNativeMethods.EnableTraceEx2(m_SessionHandle, ref providerGuid,
                            TraceEventNativeMethods.EVENT_CONTROL_CODE_ENABLE_PROVIDER, (byte)providerLevel,
                            matchAnyKeywords, matchAllKeywords, 0, ref parameters);
                    }
                    catch (EntryPointNotFoundException)
                    {
                        // OK that did not work, try the VISTA API
                        hr = TraceEventNativeMethods.EnableTraceEx(ref providerGuid, null, m_SessionHandle, 1,
                            (byte)providerLevel, matchAnyKeywords, matchAllKeywords, 0, dataDescrPtr);
                    }
                }
                catch (EntryPointNotFoundException)
                {
                    // Try with the old pre-vista API
                    hr = TraceEventNativeMethods.EnableTrace(1, (int)matchAnyKeywords, (int)providerLevel, ref providerGuid, m_SessionHandle);
                }
                Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRFromWin32(hr));
            }
            m_IsActive = true;
            return ret;
        }
        private static void SetOrDelete(string regKeyName, string valueName, byte[] data)
        {
            if (System.Runtime.InteropServices.Marshal.SizeOf(typeof(IntPtr)) == 8 &&
                regKeyName.StartsWith(@"Software\", StringComparison.OrdinalIgnoreCase))
                regKeyName = @"Software\Wow6432Node" + regKeyName.Substring(8);

            if (data == null)
            {
                Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regKeyName, true);
                if (regKey != null)
                {
                    regKey.DeleteValue(valueName, false);
                    regKey.Close();
                }
            }
            else
            {
                Microsoft.Win32.RegistryKey regKey = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(regKeyName);
                regKey.SetValue(valueName, data, Microsoft.Win32.RegistryValueKind.Binary);
                regKey.Close();
            }
        }

        /// <summary>
        /// Given a mask of kernel flags, set the array stackTracingIds of size stackTracingIdsMax to match.
        /// It returns the number of entries in stackTracingIds that were filled in.
        /// </summary>
        private unsafe int SetStackTraceIds(KernelTraceEventParser.Keywords stackCapture, TraceEventNativeMethods.STACK_TRACING_EVENT_ID* stackTracingIds, int stackTracingIdsMax)
        {
            int curID = 0;

            // PerfInfo (sample profiling)
            if ((stackCapture & KernelTraceEventParser.Keywords.Profile) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.PerfInfoTaskGuid;
                stackTracingIds[curID].Type = 0x2e;     // Sample Profile
                curID++;
            }

            // PCM sample profiling
            if ((stackCapture & KernelTraceEventParser.Keywords.PMCProfile) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.PerfInfoTaskGuid;
                stackTracingIds[curID].Type = 0x2f;     // PMC Sample Profile
                curID++;
            }

            if ((stackCapture & KernelTraceEventParser.Keywords.SystemCall) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.PerfInfoTaskGuid;
                stackTracingIds[curID].Type = 0x33;     // SysCall
                curID++;
            }
            // Thread
            if ((stackCapture & KernelTraceEventParser.Keywords.Thread) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ThreadTaskGuid;
                stackTracingIds[curID].Type = 0x01;     // Thread Create
                curID++;
            }

            if ((stackCapture & KernelTraceEventParser.Keywords.ContextSwitch) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ThreadTaskGuid;
                stackTracingIds[curID].Type = 0x24;     // Context Switch
                curID++;
            }

            if ((stackCapture & KernelTraceEventParser.Keywords.Dispatcher) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ThreadTaskGuid;
                stackTracingIds[curID].Type = 0x32;     // Ready Thread
                curID++;
            }

            // Image
            if ((stackCapture & KernelTraceEventParser.Keywords.ImageLoad) != 0)
            {
                // Confirm this is not ImageTaskGuid
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ProcessTaskGuid;
                stackTracingIds[curID].Type = 0x0A;     // Image Load
                curID++;
            }

            // Process
            if ((stackCapture & KernelTraceEventParser.Keywords.Process) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.ProcessTaskGuid;
                stackTracingIds[curID].Type = 0x01;     // Process Create
                curID++;
            }

            // Disk
            if ((stackCapture & KernelTraceEventParser.Keywords.DiskIOInit) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.DiskIoTaskGuid;
                stackTracingIds[curID].Type = 0x0c;     // Read Init
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.DiskIoTaskGuid;
                stackTracingIds[curID].Type = 0x0d;     // Write Init
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.DiskIoTaskGuid;
                stackTracingIds[curID].Type = 0x0f;     // Flush Init
                curID++;
            }

            // Virtual Alloc
            if ((stackCapture & KernelTraceEventParser.Keywords.VirtualAlloc) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.VirtualAllocTaskGuid;
                stackTracingIds[curID].Type = 0x62;     // Flush Init
                curID++;
            }

            // Hard Faults
            if ((stackCapture & KernelTraceEventParser.Keywords.MemoryHardFaults) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.PageFaultTaskGuid;
                stackTracingIds[curID].Type = 0x20;     // Hard Fault
                curID++;
            }

            // Page Faults 
            if ((stackCapture & KernelTraceEventParser.Keywords.MemoryPageFaults) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.PageFaultTaskGuid;
                stackTracingIds[curID].Type = 0x0A;     // Transition Fault
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.PageFaultTaskGuid;
                stackTracingIds[curID].Type = 0x0B;     // Demand zero Fault
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.PageFaultTaskGuid;
                stackTracingIds[curID].Type = 0x0C;     // Copy on Write Fault
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.PageFaultTaskGuid;
                stackTracingIds[curID].Type = 0x0D;     // Guard Page Fault
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.PageFaultTaskGuid;
                stackTracingIds[curID].Type = 0x0E;     // Hard Page Fault
                curID++;

                // TODO these look interesting.  
                // ! %02 49 ! Pagefile Mapped Section Create
                // ! %02 69 ! Pagefile Backed Image Mapping
                // ! %02 71 ! Contiguous Memory Generation
            }

            if ((stackCapture & KernelTraceEventParser.Keywords.FileIOInit) != 0)
            {
                // TODO allow stacks only on open and close;
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.FileIoTaskGuid;
                stackTracingIds[curID].Type = 0x40;     // Create
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.FileIoTaskGuid;
                stackTracingIds[curID].Type = 0x41;     // Cleanup
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.FileIoTaskGuid;
                stackTracingIds[curID].Type = 0x42;     // Close
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.FileIoTaskGuid;
                stackTracingIds[curID].Type = 0x43;     // Read
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.FileIoTaskGuid;
                stackTracingIds[curID].Type = 0x44;     // Write
                curID++;
            }

            if ((stackCapture & KernelTraceEventParser.Keywords.Registry) != 0)
            {
                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x0A;     // NtCreateKey
                curID++;

                stackTracingIds[curID].EventGuid = KernelTraceEventParser.RegistryTaskGuid;
                stackTracingIds[curID].Type = 0x0B;     // NtOpenKey
                curID++;

            }

            // TODO put these in for advanced procedure calls.  
            //! %1A 21 ! ALPC: SendMessage
            //! %1A 22 ! ALPC: ReceiveMessage
            //! %1A 23 ! ALPC: WaitForReply
            //! %1A 24 ! ALPC: WaitForNewMessage
            //! %1A 25 ! ALPC: UnWait

            // I don't have heap or threadpool.  

            // Confirm we did not overflow.  
            Debug.Assert(curID <= stackTracingIdsMax);
            return curID;
        }
        private bool InsureStarted(TraceEventNativeMethods.EVENT_TRACE_PROPERTIES* properties = null)
        {
            if (!m_Create)
                throw new NotSupportedException("Can not enable providers on a session you don't create directly");

            // Already initialized, nothing to do.  
            if (m_SessionHandle != TraceEventNativeMethods.INVALID_HANDLE_VALUE)
                return false;

            var propertiesBuff = stackalloc byte[PropertiesSize];
            if (properties == null)
                properties = GetProperties(propertiesBuff);
            bool ret = false;

            int retCode = TraceEventNativeMethods.StartTraceW(out m_SessionHandle, m_SessionName, properties);
            if (retCode == 0xB7)      // STIERR_HANDLEEXISTS
            {
                ret = true;
                Stop();
                m_Stopped = false;
                Thread.Sleep(100);  // Give it some time to stop. 
                retCode = TraceEventNativeMethods.StartTraceW(out m_SessionHandle, m_SessionName, properties);
            }
            if (retCode == 5 && Environment.OSVersion.Version.Major > 5)      // On Vista and we get a 'Accessed Denied' message
                throw new UnauthorizedAccessException("Error Starting ETW:  Access Denied (Administrator rights required to start ETW)");
            Marshal.ThrowExceptionForHR(TraceEventNativeMethods.GetHRFromWin32(retCode));
            return ret;
        }
        private TraceEventNativeMethods.EVENT_TRACE_PROPERTIES* GetProperties(byte* buffer)
        {
            TraceEventNativeMethods.ZeroMemory((IntPtr)buffer, (uint)PropertiesSize);
            var properties = (TraceEventNativeMethods.EVENT_TRACE_PROPERTIES*)buffer;

            properties->LoggerNameOffset = (uint)sizeof(TraceEventNativeMethods.EVENT_TRACE_PROPERTIES);
            properties->LogFileNameOffset = properties->LoggerNameOffset + MaxNameSize * sizeof(char);

            // Copy in the session name
            if (m_SessionName.Length > MaxNameSize - 1)
                throw new ArgumentException("File name too long", "sessionName");
            char* sessionNamePtr = (char*)(((byte*)properties) + properties->LoggerNameOffset);
            CopyStringToPtr(sessionNamePtr, m_SessionName);

            properties->Wnode.BufferSize = (uint)PropertiesSize;
            properties->Wnode.Flags = TraceEventNativeMethods.WNODE_FLAG_TRACED_GUID;
            properties->LoggerNameOffset = (uint)sizeof(TraceEventNativeMethods.EVENT_TRACE_PROPERTIES);
            properties->LogFileNameOffset = properties->LoggerNameOffset + MaxNameSize * sizeof(char);
            properties->FlushTimer = 10;             // Only flush every 10 seconds for file based     

            properties->BufferSize = 1024;           // 1Mb buffer blockSize
            if (m_FileName == null)
            {
                properties->FlushTimer = 1;              // flush every second (as fast as possible) for real time. 
                properties->LogFileMode = TraceEventNativeMethods.EVENT_TRACE_REAL_TIME_MODE;
                properties->LogFileNameOffset = 0;
            }
            else
            {
                var fileName = m_FileName;
                if (m_CircularBufferMB != 0)
                {
                    properties->LogFileMode |= TraceEventNativeMethods.EVENT_TRACE_FILE_MODE_CIRCULAR;
                    properties->MaximumFileSize = (uint)m_CircularBufferMB;
                }
                else
                {
                    properties->LogFileMode |= TraceEventNativeMethods.EVENT_TRACE_FILE_MODE_SEQUENTIAL;
                }
                if (fileName.Length > MaxNameSize - 1)
                    throw new ArgumentException("File name too long", "fileName");
                char* fileNamePtr = (char*)(((byte*)properties) + properties->LogFileNameOffset);
                CopyStringToPtr(fileNamePtr, fileName);
            }

            properties->MinimumBuffers = (uint)m_BufferSizeMB;
            properties->MaximumBuffers = (uint)(m_BufferSizeMB * 4);

            properties->Wnode.ClientContext = 1;    // set Timer resolution to 100ns.  
            return properties;
        }

        private unsafe void CopyStringToPtr(char* toPtr, string str)
        {
            fixed (char* fromPtr = str)
            {
                int i = 0;
                while (i < str.Length)
                {
                    toPtr[i] = fromPtr[i];
                    i++;
                }
                toPtr[i] = '\0';   // Null terminate
            }
        }

        private const int MaxNameSize = 1024;
        private const int MaxExtensionSize = 256;
        private int PropertiesSize = sizeof(TraceEventNativeMethods.EVENT_TRACE_PROPERTIES) + 2 * MaxNameSize * sizeof(char) + MaxExtensionSize;

        // Data that is exposed through properties.  
        private string m_SessionName;             // Session name (identifies it uniquely on the machine)
        private string m_FileName;                // Where to log (null means real time session)
        private int m_BufferSizeMB;
        private int m_CircularBufferMB;

        // Internal state
        private bool m_Create;                    // Should create if it does not exist.
        private bool m_IsActive;                  // Session is active (InsureSession has been called)
        private bool m_Stopped;                   // The Stop() method was called (avoids reentrancy)
        private bool m_StopOnDispose;             // Should we Stop() when the object is destroyed?
        private ulong m_SessionHandle;            // OS handle
        #endregion
    }

    /// <summary>
    /// A list of these is returned by GetProviderKeywords
    /// </summary>
    public struct ProviderDataItem
    {
        public string Name;
        public string Description;
        public long Value;

        public override string ToString()
        {
            return string.Format("<ProviderDataItem Name=\"{0}\" Description=\"{1}\" Value=\"0x{2:x}\"/>", Name, Description, Value);
        }
    }

    /// <summary>
    /// These are options to EnableProvider
    /// </summary>
    [Flags]
    public enum TraceEventOptions
    {
        None = 0,
        Stacks = 1,         // Take a stack trace with the event
        // There is also the SID and Term Svr Session, but I have not wired them up.  
    }

    /// <summary>
    /// Indicates to a provider whether verbose events should be logged.  
    /// </summary>
    public enum TraceEventLevel
    {
        Always = 0,
        Critical = 1,
        Error = 2,
        Warning = 3,
        Informational = 4,
        Verbose = 5,
    };
}

