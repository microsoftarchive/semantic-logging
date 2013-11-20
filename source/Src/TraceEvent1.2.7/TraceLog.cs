//     Copyright (c) Microsoft Corporation.  All rights reserved.
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
// 
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// #define DEBUG_SERIALIZE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Diagnostics.Tracing.Parsers;
// For Win32Excption;
using FastSerialization;
using Symbols;
using Utilities;
using Address = System.UInt64;

/* TODO current interesting places */
/* code:#UniqueAddress code:TraceCodeAddresses.LookupSymbols */
/* code:TraceLog.CopyRawEvents code:TraceLog.ToStream */

// See code:#Introduction
namespace Diagnostics.Tracing
{
    /// <summary>
    /// #Introduction
    /// 
    /// While the raw ETW events are valuable, they really need additional processing to be really useful.
    /// Things like symbolic names for addresses, the ability to randomly access events, and having various
    /// links between threads, threads, modules, and eventToStack traces are really needed. This is what
    /// code:TraceLog provides.
    /// 
    /// In addition the format of an ETL file is private (it can only be accessed through OS APIs), and the
    /// only access is as stream of records. This makes it very difficult to do processing on the data
    /// without reading all the data into memory or reading over the file more than once. Because the data is
    /// very large, this is quite undesireable. There is also no good place to put digested information like
    /// symbols, or indexes. code:TraceLog also defines a new file format for trace data, that is public and
    /// seekable, extendable, and versionable. This is a key piece of added value.
    /// 
    /// code:TraceLog is really the entry point for a true object model for event data that are cross linked
    /// to each other as well as the raw events. Here are some of the players
    /// 
    /// * code:TraceLog - represents the event log as a whole. It holds 'global' things, like a list of
    ///     code:TraceProcesss, and the list of code:TraceModuleFiles
    ///     * code:TraceProcesses - represents a list of code:TraceProcess s, that can be looked up by
    ///         (PID,time)
    ///     * code:TraceProcess - represents a single process.
    ///     * code:TraceThread - reprsents a thread within a process.
    ///     * code:TraceLoadedModules - represents a list of code:TraceLoadedModule s that can be looked up
    ///         by (address, time) or (filename, time)
    ///     * code:TraceLoadedModule - represents a loaded DLL or EXE (it knows its image base, and time
    ///         loaded)
    ///     * code:TraceModuleFile - represents a DLL or EXE on the disk (it only contains information that
    ///         is common to all threads that use it (eg its name). In particular it holds all the symbolic
    ///         address to name mappings (extracted from PDBs).  New TraceModuleFiles are generated if a
    ///         files is loaded in another locations (either later in the same process or a different
    ///         process).   Thus the image base becomes a attribute of the ModuleFile
    ///     * code:TraceCallStack - represents a call stack associated with the event (on VISTA). It is
    ///         logically a list of code addresses (from callee to caller).    
    ///     * code:TraceCodeAddress - represents instruction pointer into the code. This can be decorated
    ///         with symbolic information, (methodIndex, source line, source file) information.
    ///     * code:TraceMethod - represents a particular method.  This class allows information that is
    ///         common to many samples (it method name and source file), to be shared.  
    ///     
    /// * See also code:TraceLog.CopyRawEvents for the routine that scans over the events during TraceLog
    ///     creation.
    /// * See also code:#ProcessHandlersCalledFromTraceLog for callbacks made from code:TraceLog.CopyRawEvents
    /// * See also code:#ModuleHandlersCalledFromTraceLog for callbacks made from code:TraceLog.CopyRawEvents
    /// </summary>
    public class TraceLog : TraceEventSource, IDisposable, IFastSerializable, IFastSerializableVersion
    {
        /// <summary>
        /// If etlxFilePath exists, it simply calls the constuctor.  However it it does not exist and a
        /// cooresponding ETL file exists, generate the etlx file from the ETL file.  options indicate
        /// conversion options (can be null). 
        /// </summary>
        public static TraceLog OpenOrConvert(string etlxFilePath, TraceLogOptions options)
        {
            // Accept either Etl or Etlx file name 
            if (etlxFilePath.EndsWith(".etl", StringComparison.OrdinalIgnoreCase))
                etlxFilePath = Path.ChangeExtension(etlxFilePath, ".etlx");

            // See if the etl file exists. 
            string etlFilePath = Path.ChangeExtension(etlxFilePath, ".etl");
            bool created = false;
            if (File.Exists(etlFilePath))
            {
                // Check that etlx is up to date.  
                if (!File.Exists(etlxFilePath) || File.GetLastWriteTimeUtc(etlxFilePath) < File.GetLastWriteTimeUtc(etlFilePath))
                {
                    CreateFromETL(etlFilePath, etlxFilePath, options);
                    created = true;
                }
            }
            try
            {
                return new TraceLog(etlxFilePath);
            }
            catch (Exception)
            {
                if (created)
                    throw;
            }
            // Try again to create from scratch.  
            CreateFromETL(etlFilePath, etlxFilePath, options);
            return new TraceLog(etlxFilePath);
        }
        public static TraceLog OpenOrConvert(string etlxFilePath)
        {
            return OpenOrConvert(etlxFilePath, null);
        }
        /// <summary>
        /// Opens a existing Trace Event log file (and ETLX file).  If you need to create a new log file
        /// from other data see 
        /// </summary>
        public TraceLog(string etlxFilePath)
            : this()
        {
            InitializeFromFile(etlxFilePath);
        }

        /// <summary>
        /// Generates the cooresponding ETLX file from the raw ETL files.  Returns the name of ETLX file. 
        /// </summary>
        public static string CreateFromETL(string etlFilePath)
        {
            return CreateFromETL(etlFilePath, null, null);
        }
        /// <summary>
        /// Given 'etlFilePath' create a etlxFile for the profile data. Options can be null.
        /// </summary>
        public static string CreateFromETL(string etlFilePath, string etlxFilePath, TraceLogOptions options)
        {
            if (etlxFilePath == null)
                etlxFilePath = Path.ChangeExtension(etlFilePath, ".etlx");
            using (ETWTraceEventSource source = new ETWTraceEventSource(etlFilePath))
            {
                if (source.EventsLost != 0 && options != null && options.OnLostEvents != null)
                    options.OnLostEvents(source.EventsLost);
                CreateFromSource(source, etlxFilePath, options);
            }
            return etlxFilePath;
        }
        /// <summary>
        /// Given a source of events 'source' generated a ETLX file representing these events from them. This
        /// file can then be opened with the code:TraceLog constructor. 'options' can be null.
        /// </summary>
        public static void CreateFromSource(TraceEventDispatcher source, string etlxFilePath, TraceLogOptions options)
        {
            CreateFromSourceTESTONLY(source, etlxFilePath, options);
        }
        /// <summary>
        /// TODO: only used for testing, will be removed eventually.  Use CreateFromSource
        /// 
        /// Because the code path when reading from the file (and thus uses the deserializers), is very
        /// different from when the data structures are in memory, and we don't want to have to test both
        /// permutations, we don't allow getting a TraceLog that did NOT come from a file.  
        /// 
        /// However for testing this is useful, because we can see the 'before serialization' and 'after
        /// serialization' behavior and if they are differnet we know we hav a bug.  This routine should be
        /// removed eventually, after we have high confidence that the log file works well.  
        /// </summary>
        //TODO fold into CreateFromSource
        private static TraceLog CreateFromSourceTESTONLY(TraceEventDispatcher source, string etlxFilePath, TraceLogOptions options)
        {
            if (options == null)
                options = new TraceLogOptions();

            // TODO copy the additional data from a ETLX file if the source is ETLX 
            TraceLog newLog = new TraceLog();
            newLog.rawEventSourceToConvert = source;
            newLog.options = options;

            // Copy over all the users data from the source.  
            foreach (string key in source.UserData.Keys)
                newLog.UserData[key] = source.UserData[key];

            // We always create these parsers that the TraceLog knows about.
            new ClrRundownTraceEventParser(newLog);
            var kernelParser = newLog.Kernel;
            var clrParser = newLog.Clr;
            var dynamicParser = newLog.Dynamic;

            // Add any Manifest.xml files to the dynamic parser 
            if (options.ExplicitManifestDir != null)
            {
                if (Directory.Exists(options.ExplicitManifestDir))
                {
                    options.ConversionLog.WriteLine("Looking for explicit manifests in {0}", options.ExplicitManifestDir);
                    dynamicParser.ReadAllManifests(options.ExplicitManifestDir);
                }
            }

            dynamicParser.All += null;                  // TODO can we avoid this?  
            new ClrStressTraceEventParser(newLog);
            new Diagnostics.Tracing.Parsers.JScriptTraceEventParser(newLog);
            new Diagnostics.Tracing.Parsers.JSDumpHeapTraceEventParser(newLog);
            new AspNetTraceEventParser(newLog);
            new TplEtwProviderTraceEventParser(newLog);
            new SymbolTraceEventParser(newLog);
            new HeapTraceProviderTraceEventParser(newLog);

            if (options.ExplicitManifestDir != null)
            {
                var tmfDir = Path.Combine(options.ExplicitManifestDir, "TMF");
                if (Directory.Exists(tmfDir))
                {
                    options.ConversionLog.WriteLine("Looking for WPP metaData in {0}", tmfDir);
                    new WppTraceEventParser(newLog, tmfDir);
                }
            }
            new RegisteredTraceEventParser(newLog);

            // Avoid partially written files by writing to a temp and moving atomically to the
            // final destination.  
            string etlxTempPath = etlxFilePath + ".new";
            try
            {
                // This calls code:TraceLog.ToStream operation on TraceLog which does the real work.  
                using (Serializer serializer = new Serializer(etlxTempPath, newLog)) { }
                if (File.Exists(etlxFilePath))
                    File.Delete(etlxFilePath);
                File.Move(etlxTempPath, etlxFilePath);
            }
            finally
            {
                if (File.Exists(etlxTempPath))
                    File.Delete(etlxTempPath);
            }

            return newLog;      // TODO should return void.  
        }

        /// <summary>
        /// All the events in the stream.  A code:TraceEvent can be used with foreach
        /// directly but it can also be used to filter in arbitrary ways to form other
        /// logical streams of data.  
        /// </summary>
        public TraceEvents Events { get { return events; } }
        /// <summary>
        /// Enumerate all the processes that occured in the trace log. 
        /// </summary>
        public TraceProcesses Processes { get { return processes; } }
        /// <summary>
        /// Enumerate all the threads that occured in the trace log.
        /// </summary>
        public TraceThreads Threads { get { return threads; } }

        /// <summary>
        /// A list of all the files that are loaded by some process during the logging. 
        /// </summary>
        public TraceModuleFiles ModuleFiles { get { return moduleFiles; } }
        /// <summary>
        /// Get the collection of all callstacks.  
        /// </summary>
        public TraceCallStacks CallStacks { get { return callStacks; } }
        /// <summary>
        /// Get the collection of all symbolic code addresses. 
        /// </summary>
        public TraceCodeAddresses CodeAddresses { get { return codeAddresses; } }

        /// <summary>
        /// Has summary statistics on the events in the log.  
        /// </summary>
        public TraceEventStats Stats { get { return stats; } }

        /// <summary>
        /// If the event has a call eventToStack associated with it, retrieve it. 
        /// </summary>
        public TraceCallStack GetCallStackForEvent(TraceEvent anEvent)
        {
            return callStacks[GetCallStackIndexForEvent(anEvent)];
        }
        /// <summary>
        /// If an event has fields of type 'Address' the address can be converted to a symblic value (a
        /// code:TraceCodeAddress) by calling this function.
        /// </summary>
        public TraceCodeAddress GetCodeAddressAtEvent(Address address, TraceEvent context)
        {
            CodeAddressIndex codeAddressIndex = GetCodeAddressIndexAtEvent(address, context);
            if (codeAddressIndex == CodeAddressIndex.Invalid)
                return null;
            return codeAddresses[codeAddressIndex];
        }

        // Allow caller to store EventIndex and only convert to CallStackIndex when needed
        public CallStackIndex GetCallStackIndexForEventIndex(EventIndex eventIndex)
        {
            // TODO optimize for sequential access.  
            lazyEventsToStacks.FinishRead();
            int index;
            if (eventsToStacks.BinarySearch(eventIndex, out index, stackComparer))
                return eventsToStacks[index].CallStackIndex;
            return CallStackIndex.Invalid;
        }

        public CallStackIndex GetCallStackIndexForEvent(TraceEvent anEvent)
        {
            return GetCallStackIndexForEventIndex(anEvent.EventIndex);
        }

        public CodeAddressIndex GetCodeAddressIndexAtEvent(Address address, TraceEvent context)
        {
            // TODO optimize for sequential access.  
            EventIndex eventIndex = context.EventIndex;
            int index;
            if (!eventsToCodeAddresses.BinarySearch(eventIndex, out index, CodeAddressComparer))
                return CodeAddressIndex.Invalid;
            do
            {
                Debug.Assert(eventsToCodeAddresses[index].EventIndex == eventIndex);
                if (eventsToCodeAddresses[index].Address == address)
                    return eventsToCodeAddresses[index].CodeAddressIndex;
                index++;
            } while (index < eventsToCodeAddresses.Count && eventsToCodeAddresses[index].EventIndex == eventIndex);
            return CodeAddressIndex.Invalid;
        }

        /// <summary>
        /// Events are given an Index (ID) that are unique across the whole code:TraceLog.   They are not guarenteed
        /// to be sequential, but they are guarenteed to be between 0 and MaxEventIndex.  Ids can be used to
        /// allow clients to associate additional information with event (with a side lookup table).   See
        /// code:TraceEvent.EventIndex and code:EventIndex for more 
        /// </summary>
        public EventIndex MaxEventIndex { get { return (EventIndex)eventCount; } }
        /// <summary>
        /// Given an eventIndex, get the event.  This is relatively expensive because we need to create a
        /// copy of the event that will not be reused by the TraceLog.   Ideally you woudld not use this API
        /// but rather use iterate over event using code:TraceEvents
        /// </summary>
        public TraceEvent GetEvent(EventIndex eventIndex)
        {
            // TODO NOT Done. 
            Debug.Assert(false, "NOT Done");
            TraceEvent anEvent = null;
            return anEvent;
        }
        /// <summary>
        /// The size of the log file.  
        /// </summary>
        public override long Size
        {
            get
            {
                return new FileInfo(etlxFilePath).Length;
            }
        }

        /// <summary>
        /// There is a size limit for ETLX files.  If we had to truncate becasue of this set this bit.  
        /// </summary>
        public bool Truncated { get { return truncated; } }

        /// <summary>
        /// The total number of events in the log.  
        /// </summary>
        public int EventCount { get { return eventCount; } }
        /// <summary>
        /// The file path name for the ETLX file associated with this log.  
        /// </summary>
        public string FilePath { get { return etlxFilePath; } }
        public long FirstEventTime100ns { get { return firstEventTime100ns; } }
        public DateTime FirstEventTime { get { return DateTime.FromFileTime(FirstEventTime100ns); } }
        /// <summary>
        /// The machine one which the log was collected.  Returns empty string if unknown. 
        /// </summary>
        public string MachineName { get { return machineName; } }
        /// <summary>
        /// The name of the Operating system.  Return empty string if unknown
        /// </summary>
        public string OSName { get { return osName; } }
        /// <summary>
        /// The build number information for the OS.  Return empty string if unknown
        /// </summary>
        public string OSBuild { get { return osBuild; } }
        /// <summary>
        /// The time the machine was booted (note it can be 0 if we don't know)
        /// </summary>
        public DateTime BootTime { get { return DateTime.FromFileTime(bootTime100ns); } }
        /// <summary>
        /// Returns true if the log has information necessary to look up PDBS associated with the images
        /// in the trace.  
        /// </summary>
        public bool HasPdbInfo { get { return hasPdbInfo; } }

        /// <summary>
        /// The size of the main memory (RAM) on the collection machine.  Will return 0 if memory size is unknown 
        /// </summary>
        public int MemorySizeMeg { get { return memorySizeMeg; } }
        /// <summary>
        /// Are there any events with stack traces in them?
        /// </summary>
        public bool HasCallStacks { get { return CallStacks.MaxCallStackIndex > 0; } }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(" <TraceLogHeader ");
            sb.AppendLine("   MachineName=" + XmlUtilities.XmlQuote(MachineName));
            sb.AppendLine("   EventCount=" + XmlUtilities.XmlQuote(EventCount));
            sb.AppendLine("   LogFileName=" + XmlUtilities.XmlQuote(FilePath));
            sb.AppendLine("   EventsLost=" + XmlUtilities.XmlQuote(EventsLost));
            sb.AppendLine("   SessionStartTime=" + XmlUtilities.XmlQuote(SessionStartTime));
            sb.AppendLine("   SessionEndTime=" + XmlUtilities.XmlQuote(SessionEndTime));
            sb.AppendLine("   SessionDuration=" + XmlUtilities.XmlQuote((SessionDuration).ToString()));
            sb.AppendLine("   NumberOfProcessors=" + XmlUtilities.XmlQuote(NumberOfProcessors));
            sb.AppendLine("   CpuSpeedMHz=" + XmlUtilities.XmlQuote(CpuSpeedMHz));
            sb.AppendLine("   MemorySizeMeg=" + XmlUtilities.XmlQuote(MemorySizeMeg));
            sb.AppendLine("   PointerSize=" + XmlUtilities.XmlQuote(PointerSize));
            sb.AppendLine(" />");
            return sb.ToString();
        }

        public int SampleProfileInterval100ns { get { return sampleProfileInterval100ns; } }
        public bool CurrentMachineIsCollectionMachine()
        {
            // Trim off the domain, as there is ambiguity about whether to include that or not.  
            var shortCurrentMachineName = Environment.MachineName;
            var dotIdx = shortCurrentMachineName.IndexOf('.');
            if (dotIdx > 0)
                shortCurrentMachineName = shortCurrentMachineName.Substring(0, dotIdx);
            var shortDataMachineName = MachineName;
            dotIdx = shortDataMachineName.IndexOf('.');
            if (dotIdx > 0)
                shortDataMachineName = shortDataMachineName.Substring(0, dotIdx);

            return string.Compare(shortDataMachineName, shortCurrentMachineName, StringComparison.OrdinalIgnoreCase) == 0;
        }

        /// <summary>
        /// Agressively releases resources associated with the log. 
        /// </summary>
        public void Close()
        {
            lazyRawEvents.Dispose();
        }

        /// <summary>
        /// Returns all the TraceEventParsers associated with this log.  
        /// </summary>
        public IEnumerable<TraceEventParser> Parsers { get { return parsers; } }

        #region ITraceParserServices Members

        protected override void RegisterEventTemplateImpl(TraceEvent template)
        {
            ((ITraceParserServices)sourceWithRegisteredParsers).RegisterEventTemplate(template);

            // If we are converting from raw input, send the callbacks to them during that phase too.   
            if (rawEventSourceToConvert != null)
                ((ITraceParserServices)rawEventSourceToConvert).RegisterEventTemplate(template);
        }

        protected override void RegisterParserImpl(TraceEventParser parser)
        {
            parsers.Add(parser);
            // cause all the events in the parser to be registered with me.
            // Converting raw input is a special case, and we don't do the registration in that case. 
            if (rawEventSourceToConvert == null)
                parser.All += null;
        }

        protected override void RegisterUnhandledEventImpl(Func<TraceEvent, TraceEvent> callback)
        {
            ((ITraceParserServices)sourceWithRegisteredParsers).RegisterUnhandledEvent(callback);

            // If we are converting from raw input, send the callbacks to them during that phase too.   
            if (rawEventSourceToConvert != null)
                ((ITraceParserServices)rawEventSourceToConvert).RegisterUnhandledEvent(callback);
        }

        protected override string TaskNameForGuidImpl(Guid guid)
        {
            return ((ITraceParserServices)sourceWithRegisteredParsers).TaskNameForGuid(guid);
        }
        protected override string ProviderNameForGuidImpl(Guid taskOrProviderGuid)
        {
            return ((ITraceParserServices)sourceWithRegisteredParsers).ProviderNameForGuid(taskOrProviderGuid);
        }
        #endregion
        #region Private
        private TraceLog()
        {
            // TODO: All the IFastSerializable parts of this are discareded, which is unfortunate. 
            this.processes = new TraceProcesses(this);
            this.threads = new TraceThreads(this);
            this.events = new TraceEvents(this);
            this.moduleFiles = new TraceModuleFiles(this);
            this.codeAddresses = new TraceCodeAddresses(this, this.moduleFiles);
            this.callStacks = new TraceCallStacks(this, this.codeAddresses);
            this.parsers = new List<TraceEventParser>();
            this.stats = new TraceEventStats(this);
            this.sourceWithRegisteredParsers = new ETLXTraceEventSource(new TraceEvents(this));
            this.machineName = "";
            this.osName = "";
            this.osBuild = "";
            this.sampleProfileInterval100ns = 10000;    // default is 1 msec
        }

        internal override unsafe Guid GetRelatedActivityID(TraceEventNativeMethods.EVENT_RECORD* eventRecord)
        {
            // See code:TraceLog.ProcessExtendedData for more on our use of ExtendedData to hold a index.   
            if (eventRecord->ExtendedDataCount == 1)
            {
                int idIndex = (int)eventRecord->ExtendedData;
                if (idIndex < events.log.relatedActivityIDs.Count)
                    return events.log.relatedActivityIDs[idIndex];
            }
            return Guid.Empty;
        }


        /// <summary>
        ///  Copies the events from the 'rawEvents' dispatcher to the output stream 'IStreamWriter'.  It
        ///  also creates auxillery data structures associated with the raw events (eg, processes, threads,
        ///  modules, address lookup maps...  Basically any information that needs to be determined by
        ///  scanning over the events during TraceLog creation should hook in here.  
        /// </summary>
        unsafe private void CopyRawEvents(TraceEventDispatcher rawEvents, IStreamWriter writer)
        {
            bool removeFromStream = false;
            bool bookKeepingEvent = false;                  // BookKeeping events are removed from the stream by default
            bool bookeepingEventThatMayHaveStack = false;   // Some bookeeping events (ThreadDCStart) might have stacks 
            // To avoid firing asserts about orphan stacks, remember that these
            // events need to be put into the 'prevEvent' info
            bool noStack = false;                           // This event should never have a stack associated with it. 
            int numberOnPage = eventsPerPage;
            PastEventInfo pastEventInfo = new PastEventInfo(this);
            eventCount = 0;
            const int defaultMaxEventCount = 10000000;                   // 10M events produces about 3-4GB of data.  which is close to the limit of ETLX. 
            int maxEventCount = defaultMaxEventCount;
            long start100ns = rawEvents.SessionStartTime100ns;
            if (options != null)
            {
                if (options.SkipMSec != 0)
                    options.ConversionLog.WriteLine("Skipping the {0:n3} MSec of the trace.", options.SkipMSec);
                if (options.MaxEventCount != 0)
                    options.ConversionLog.WriteLine("Collecting a maximum of {0:n0} events.", options.MaxEventCount);
                if (options.MaxEventCount > 10000)          // Numbers smaller than this are almost certainly errors
                    maxEventCount = options.MaxEventCount;

                start100ns += (long)(options.SkipMSec * 10000);
            }

            // FIX NOW HACK, because Method and Module unload methods are missing. 
            var jittedMethods = new List<MethodLoadUnloadVerboseTraceData>();
            var jsJittedMethods = new List<MethodLoadUnloadJSTraceData>();
            var sourceFilesByID = new Dictionary<JavaScriptSourceKey, string>();

            if (!(rawEvents is ETLXTraceEventSource))
            {
                // If this is a ETL file, we also need to compute all the normal TraceLog stuff the raw
                // stream.  

                // TODO fail if you merge logs of varying pointer size.  
                this.pointerSize = rawEvents.PointerSize;
                this.sessionEndTime100ns = rawEvents.SessionEndTime100ns;
                this.sessionStartTime100ns = rawEvents.SessionStartTime100ns;
                this.cpuSpeedMHz = rawEvents.CpuSpeedMHz;
                this._QPCFreq = rawEvents._QPCFreq;
                this.numberOfProcessors = rawEvents.NumberOfProcessors;
                this.eventsLost = rawEvents.EventsLost;
                this.osVersion = rawEvents.OSVersion;

                // TODO need all events that might have Addresses in them, can we be more efficient.  
                rawEvents.Kernel.All += delegate(TraceEvent data) { };
                // Parse CLR events so that TDH does not try to look them up either.  TODO FIX NOW more efficient 
                rawEvents.Clr.All += delegate(TraceEvent data) { };
                // And the CLR Rundown events too.  
                var ClrRundownParser = parsers[0] as ClrRundownTraceEventParser;
                Debug.Assert(ClrRundownParser != null);     // We register this one first, so we don't have to search for it.  
                ClrRundownParser.All += delegate(TraceEvent data) { };

                Debug.Assert(((eventsPerPage - 1) & eventsPerPage) == 0, "eventsPerPage must be a power of 2");

                // TODO, these are put first, but the user mode header has a time-stamp that is out of order
                // which messes up our binary search.   For now just remove them, as the data is really
                // accessed from the log, not the event.  
                rawEvents.Kernel.EventTraceHeader += delegate(EventTraceHeaderTraceData data)
                {
                    bootTime100ns = data.BootTime100ns;
                    bookKeepingEvent = true;
                };

                rawEvents.Kernel.SystemConfigCPU += delegate(SystemConfigCPUTraceData data)
                {
                    this.memorySizeMeg = data.MemSize;
                    if (data.DomainName.Length > 0)
                        this.machineName = data.ComputerName + "." + data.DomainName;
                    else
                        this.machineName = data.ComputerName;
                };

                rawEvents.Kernel.BuildInfo += delegate(BuildInfoTraceData data)
                {
                    this.osName = data.ProductName;
                    this.osBuild = data.BuildLab;
                };

                // Process level events. 
                rawEvents.Kernel.ProcessStartGroup += delegate(ProcessTraceData data)
                {
                    this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStamp100ns, data.Opcode == TraceEventOpcode.Start).ProcessStart(data);
                    // Don't filter them out (not that many, useful for finding command line)
                };

                rawEvents.Kernel.ProcessEndGroup += delegate(ProcessTraceData data)
                {
                    this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStamp100ns).ProcessEnd(data);
                    // Don't filter them out (not that many, useful for finding command line)
                };
                // Thread level events
                rawEvents.Kernel.ThreadStartGroup += delegate(ThreadTraceData data)
                {
                    TraceProcess process = this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStamp100ns);
                    TraceThread thread = this.Threads.GetOrCreateThread(data.ThreadID, data.TimeStamp100ns, process, data.Opcode == TraceEventOpcode.Start);
                    thread.startTime100ns = data.TimeStamp100ns;
                    if (data.Opcode == TraceEventOpcode.DataCollectionStart)
                    {
                        bookKeepingEvent = true;
                        thread.startTime100ns = sessionStartTime100ns;
                    }
                    else if (data.Opcode == TraceEventOpcode.Start)
                    {
                        var threadProc = thread.Process;
                        if (!threadProc.anyThreads)
                        {
                            // We saw a real process start (not a DCStart or a non at all)
                            if (sessionStartTime100ns < threadProc.StartTime100ns && threadProc.StartTime100ns < data.TimeStamp100ns)
                                thread.threadInfo = "Startup Thread";
                            threadProc.anyThreads = true;
                        }
                    }
                };
                rawEvents.Kernel.ThreadEndGroup += delegate(ThreadTraceData data)
                {
                    TraceProcess process = this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStamp100ns);
                    TraceThread thread = this.Threads.GetOrCreateThread(data.ThreadID, data.TimeStamp100ns, process);
                    if (thread.process == null)
                        thread.process = process;
                    Debug.Assert(thread.process == process, "Different events disagree on the process object!");
                    DebugWarn(thread.endTime100ns == ETWTraceEventSource.MaxTime100ns || thread.ThreadID == 0,
                        "Thread end on a terminated thread " + data.ThreadID + " that ended at " + RelativeTimeMSec(thread.endTime100ns), data);
                    DebugWarn(thread.Process.EndTime100ns == ETWTraceEventSource.MaxTime100ns, "Thread ending on ended process", data);
                    thread.endTime100ns = data.TimeStamp100ns;
                    if (data.Opcode == TraceEventOpcode.DataCollectionStop)
                    {
                        thread.endTime100ns = sessionEndTime100ns;
                        bookKeepingEvent = true;
                        bookeepingEventThatMayHaveStack = true;
                    }
                };

                // ModuleFile level events
                DbgIDRSDSTraceData lastDbgData = null;
                ImageIDTraceData lastImageIDData = null;
                FileVersionTraceData lastFileVersionData = null;

                rawEvents.Kernel.ImageGroup += delegate(ImageLoadTraceData data)
                {
                    var isLoad = ((data.Opcode == (TraceEventOpcode)10) || (data.Opcode == TraceEventOpcode.DataCollectionStart));
                    var moduleFile = this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStamp100ns).LoadedModules.ImageLoadOrUnload(data, isLoad);
                    // TODO FIX NOW review:  is using the timestamp the best way to make the association
                    if (lastDbgData != null && data.TimeStamp100ns == lastDbgData.TimeStamp100ns)
                    {
                        moduleFile.pdbName = lastDbgData.PdbFileName;
                        moduleFile.pdbSignature = lastDbgData.GuidSig;
                        moduleFile.pdbAge = lastDbgData.Age;
                    }
                    if (lastImageIDData != null && data.TimeStamp100ns == lastImageIDData.TimeStamp100ns)
                        moduleFile.timeDateStamp = lastImageIDData.TimeDateStamp;
                    if (lastFileVersionData != null && data.TimeStamp100ns == lastFileVersionData.TimeStamp100ns)
                        moduleFile.fileVersion = lastFileVersionData.FileVersion;

                    /* allow these to remain in the trace.  Otherwise you can't just look at the events view and look up DLL info
                     * which is pretty convinient.   If we have a good image view that we can remove these. 
                    if (data.Opcode == TraceEventOpcode.DataCollectionStart)
                        bookKeepingEvent = true;
                    else if (data.Opcode == TraceEventOpcode.DataCollectionStop)
                        bookKeepingEvent = true;
                     ***/
                };
                var symbolParser = new SymbolTraceEventParser(rawEvents);

                symbolParser.None += delegate(EmptyTraceData data)
                {
                    // If I don't have this, the code ends up not attaching the stack to the image load which has the same timestamp. 
                    noStack = true;
                };
                symbolParser.DbgIDRSDS += delegate(DbgIDRSDSTraceData data)
                {
                    hasPdbInfo = true;
                    lastDbgData = (DbgIDRSDSTraceData)data.Clone();
                    noStack = true;
                };
                symbolParser.ImageID += delegate(ImageIDTraceData data)
                {
                    lastImageIDData = (ImageIDTraceData)data.Clone();
                    noStack = true;
                };
                symbolParser.FileVersion += delegate(FileVersionTraceData data)
                {
                    lastFileVersionData = (FileVersionTraceData)data.Clone();
                    noStack = true;
                };
                symbolParser.MetaData2Opcode37 += delegate
                {
                    noStack = true;
                };

                rawEvents.Kernel.FileIoName += delegate(FileIoNameTraceData data)
                {
                    bookKeepingEvent = true;
                };

                rawEvents.Clr.LoaderModuleLoad += delegate(ModuleLoadUnloadTraceData data)
                {
                    this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStamp100ns).LoadedModules.ManagedModuleLoadOrUnload(data, true);
                };
                rawEvents.Clr.LoaderModuleUnload += delegate(ModuleLoadUnloadTraceData data)
                {
                    this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStamp100ns).LoadedModules.ManagedModuleLoadOrUnload(data, false);
                };
                rawEvents.Clr.LoaderModuleDCStopV2 += delegate(ModuleLoadUnloadTraceData data)
                {
                    this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStamp100ns).LoadedModules.ManagedModuleLoadOrUnload(data, false);
                };
                ClrRundownParser.LoaderModuleDCStop += delegate(ModuleLoadUnloadTraceData data)
                {
                    this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStamp100ns).LoadedModules.ManagedModuleLoadOrUnload(data, false);
                };

                // Method level events
                rawEvents.Clr.MethodLoadVerbose += delegate(MethodLoadUnloadVerboseTraceData data)
                {
                    // We only capture data on unload, because we collect the addresses first. 
                    if (!data.IsDynamic && !data.IsJitted)
                        bookKeepingEvent = true;

                    if (data.IsJitted)
                        jittedMethods.Add((MethodLoadUnloadVerboseTraceData)data.Clone());
                };
                rawEvents.Clr.MethodUnloadVerbose += delegate(MethodLoadUnloadVerboseTraceData data)
                {
                    codeAddresses.AddMethod(data);
                    if (!data.IsJitted)
                        bookKeepingEvent = true;
                };
                rawEvents.Clr.MethodILToNativeMap += delegate(MethodILToNativeMapTraceData data)
                {
                    codeAddresses.AddILMapping(data);
                    bookKeepingEvent = true;
                };

                ClrRundownParser.MethodILToNativeMapDCStop += delegate(MethodILToNativeMapTraceData data)
                {
                    codeAddresses.AddILMapping(data);
                    bookKeepingEvent = true;
                };

                Action<MethodLoadUnloadVerboseTraceData> onMethodDCStop = delegate(MethodLoadUnloadVerboseTraceData data)
                {
                    codeAddresses.AddMethod(data);
                    bookKeepingEvent = true;
                };

                rawEvents.Clr.MethodDCStopVerboseV2 += onMethodDCStop;
                ClrRundownParser.MethodDCStopVerbose += onMethodDCStop;

                var jScriptParser = new Diagnostics.Tracing.Parsers.JScriptTraceEventParser(rawEvents);

                Action<MethodLoadUnloadJSTraceData> onJScriptMethodUnload =
                    delegate(MethodLoadUnloadJSTraceData data)
                    {
                        codeAddresses.AddMethod(data, sourceFilesByID);
                        bookKeepingEvent = true;
                    };

                jScriptParser.AddToAllMatching<Diagnostics.Tracing.Parsers.SourceLoadUnloadTraceData>(
                    delegate(Diagnostics.Tracing.Parsers.SourceLoadUnloadTraceData data)
                    {
                        sourceFilesByID[new JavaScriptSourceKey(data.SourceID, data.ScriptContextID)] = data.Url;
                    });

                jScriptParser.MethodRuntimeMethodUnload += onJScriptMethodUnload;
                jScriptParser.MethodRundownMethodDCEnd += onJScriptMethodUnload;
                jScriptParser.MethodRuntimeMethodLoad += delegate(MethodLoadUnloadJSTraceData data)
                {
                    jsJittedMethods.Add((MethodLoadUnloadJSTraceData)data.Clone());
                };

                // We know that Disk I/O events should never have a stack associated with them (the init events do)
                // these sometimes have the same kernel timestamp as CSWITCHs, which cause ambiguity.  
                rawEvents.Kernel.AddToAllMatching(delegate(DiskIoTraceData data)
                {
                    noStack = true;
                });

                Action<ClrStackWalkTraceData> clrStackWalk = delegate(ClrStackWalkTraceData data)
                {
                    bookKeepingEvent = true;

                    // Avoid creating data structures for events we will throw away
                    if (data.TimeStamp100ns < start100ns || eventCount >= maxEventCount)
                        return;

                    // Look for the previous CLR event on this same thread.  
                    PastEventInfoIndex prevEventIndex = pastEventInfo.GetPreviousEventIndex(data, true);
                    if (prevEventIndex != PastEventInfoIndex.Invalid)
                    {
                        Debug.Assert(pastEventInfo.IsClrEvent(prevEventIndex));
                        var process = Processes.GetOrCreateProcess(data.ProcessID, data.TimeStamp100ns);
                        var thread = Threads.GetOrCreateThread(data.ThreadID, data.TimeStamp100ns, process);

                        CallStackIndex callStackIndex = callStacks.GetStackIndexForStackEvent(
                            data.TimeStamp100ns, data.InstructionPointers, data.FrameCount, data.PointerSize == 8, thread);
                        Debug.Assert(callStacks.Depth(callStackIndex) == data.FrameCount);
                        DebugWarn(pastEventInfo.GetThreadID(prevEventIndex) == data.ThreadID, "Mismatched thread for CLR Stack Trace", data);

                        // Get the previous event on the same thread. 
                        EventIndex eventIndex = pastEventInfo.GetEventIndex(prevEventIndex);
                        eventsToStacks.Add(new EventsToStackIndex(eventIndex, callStackIndex));
                        pastEventInfo.GetEventCounts(prevEventIndex).m_stackCount++;
                    }
                    else
                        DebugWarn(false, "Could not find a previous event for a CLR stack trace.", data);
                };
                rawEvents.Clr.ClrStackWalk += clrStackWalk;

                // Matches User and StackKeyKernel and StackKeyUser events 
                Action<TraceEvent, StackWalkTraceData, StackWalkRefTraceData> processStackWalk = delegate(TraceEvent data, StackWalkTraceData asStackWalk, StackWalkRefTraceData asStackRef)
                {
                    bookKeepingEvent = true;
                    if (data.TimeStamp100ns < start100ns || eventCount >= maxEventCount)
                        return;

                    Debug.Assert((asStackWalk != null && asStackRef == null) || (asStackRef != null && asStackWalk == null));

                    var eventTimeStampQPC = data.GetInt64At(0); // This is really EventTimeStampQPC, but I need it work work for StackWalkRefTraceData and StackWalkTraceData
                    PastEventInfoIndex prevEventIndex = pastEventInfo.GetEventForQPC(eventTimeStampQPC, data.ThreadID);
                    if (prevEventIndex != PastEventInfoIndex.Invalid)   // Could we find an event for this stack?
                    {
                        var eventIndex = pastEventInfo.GetEventIndex(prevEventIndex);
                        // Bookeeping events (e.g ThreadStop) that have stacks are given an event index of Invalid.
                        // Just ignore them.  
                        if (eventIndex == EventIndex.Invalid)
                            return;

                        // Do we have any previous stack Information on this 
                        EventStackInfo stackInfo = pastEventInfo.GetEventStackInfo(prevEventIndex);
                        if (stackInfo == null)
                        {
                            // This is the 'first fragment', create a new EventStackInfo structure.  
                            var eventTimeStamp100ns = QPCTimeToFileTime(eventTimeStampQPC);
                            var process = Processes.GetOrCreateProcess(data.ProcessID, eventTimeStamp100ns);
                            var thread = Threads.GetOrCreateThread(data.ThreadID, eventTimeStamp100ns, process);

                            stackInfo = AllocateEventStackInfo(eventIndex, thread, eventTimeStamp100ns);
                            pastEventInfo.SetEventStackInfo(prevEventIndex, stackInfo); // Remember that we have info about this event.  
                            pastEventInfo.GetEventCounts(prevEventIndex).m_stackCount++;
                        }

                        // If this is a user mode stack part, attach it to all events that have kernel traces between the event that
                        // the user mode stack is 'attached' to and the current time.   Kernel fragments simply log the Key and exit.  
                        for (; ; )
                        {
                            // Add the information from the event to the stackInfo structure. 
                            if (stackInfo != null)
                            {
                                Debug.Assert(stackInfo.Thread != null);

                                // Add the stack fragement to the stackInfo.  
                                if (asStackWalk != null)
                                    UpdateStackInfoForStackEvent(stackInfo, asStackWalk);
                                else
                                    UpdateStackInfoForStackEvent(stackInfo, asStackRef);

                                // Check if we are doned, and if so emit the final CallStackIndex and free the StackInfo.    
                                EmitStackForEventIfReady(stackInfo);
                            }

                            prevEventIndex = pastEventInfo.GetNextEventIndex(prevEventIndex, data.ThreadID);
                            if (prevEventIndex == PastEventInfoIndex.Invalid)
                                break;

                            stackInfo = pastEventInfo.GetEventStackInfo(prevEventIndex);
                        }
                    }
                    else
                    {
                        // Warn about dropped stacks unless they occur very early in the trace
                        var expectedTime100ns = data.source.QPCTimeToFileTime(eventTimeStampQPC);
                        DebugWarn(expectedTime100ns < start100ns + 100000, "Stack looking up event at  " +
                            rawEvents.RelativeTimeMSec(expectedTime100ns).ToString("f4") +
                            " failed to find event.  Stack dropped.", data);
                    }
                };
                rawEvents.Kernel.AddToAllMatching<StackWalkRefTraceData>(delegate(StackWalkRefTraceData data) { processStackWalk(data, null, data); });
                rawEvents.Kernel.StackWalk += delegate(StackWalkTraceData data) { processStackWalk(data, data, null); };

                // Matches Delete and Rundown events;
                rawEvents.Kernel.AddToAllMatching<StackWalkDefTraceData>(delegate(StackWalkDefTraceData data)
                {
                    bookKeepingEvent = true;

                    // Delete or Rundown, I don't really care which.  
                    Debug.Assert(data.Opcode == (TraceEventOpcode)35 || data.Opcode == (TraceEventOpcode)36);

                    // Get the linked list of events that use this stack key.  
                    EventStackInfo stackInfo;
                    if (etwStackKeyToInfo.TryGetValue(data.StackKey, out stackInfo))
                    {
                        // Once we have a def, we remove it.  
                        etwStackKeyToInfo.Remove(data.StackKey);

                        EventStackInfo nextStackInfo;
                        for (; stackInfo != null; stackInfo = nextStackInfo)
                        {
                            Debug.Assert(stackInfo.Thread != null);

                            if (!stackInfo.KernelStackDefined && stackInfo.KernelStackKey == data.StackKey)
                            {
                                nextStackInfo = stackInfo.NextEventWithKernelKey;
                                stackInfo.NextEventWithKernelKey = null;
                                Debug.Assert(!stackInfo.KernelStackDefined);
                                stackInfo.KernelStackDefined = true;         // Indicate we found the def.   

                                // If we don't have any fragment converted to a CallStackIndex, this means we have
                                // not seen any UserMode stack.  We can't convert the kernel stack yet because we 
                                // don't have the top of the stack yet.  So we simply remember the kernel info and go on.  
                                // We know that the Idle and System process don't have user stacks, so we can agressively
                                // convert them 
                                var processID = stackInfo.Thread.Process.ProcessID;
                                if (processID == 0 || processID == 4)
                                {
                                    stackInfo.UserStackKey = ulong.MaxValue;        // Indicate done to EmitStackForEventIfReady
                                    stackInfo.UserStackDefined = true;
                                }
                                else if (stackInfo.StackIndex == CallStackIndex.Invalid)
                                {
                                    stackInfo.KernelStackData = (StackWalkDefTraceData)data.Clone();
                                    continue;
                                }
                            }
                            else
                            {
                                Debug.Assert(!stackInfo.UserStackDefined);
                                Debug.Assert(stackInfo.UserStackKey == data.StackKey);
                                nextStackInfo = stackInfo.NextEventWithUserKey;
                                stackInfo.NextEventWithUserKey = null;
                                Debug.Assert(!stackInfo.UserStackDefined);
                                stackInfo.UserStackDefined = true;
                            }

                            // Accumulate the stack fragment we just got to what we already have.  
                            stackInfo.StackIndex = callStacks.GetStackIndexForStackEvent(stackInfo.TimeStamp100ns,
                                data.InstructionPointers, data.FrameCount, data.PointerSize == 8, stackInfo.Thread, stackInfo.StackIndex);
                            EmitStackForEventIfReady(stackInfo);
                        }
                    }
                });

                // The following 3 callbacks for a small state machine to determine whether the process
                // is running server GC and what the server GC threads are.   
                // We assume we are server GC if there are more than one thread doing the 'MarkHandles' event
                // during a GC, and the threads that do that are the server threads.  We use this to mark the
                // threads as Server GC Threads.  
                Clr.GCStart += delegate(GCStartTraceData data)
                {
                    var process = Processes.GetProcess(data.ProcessID, data.TimeStamp100ns);
                    if (process == null)
                        return;
                    process.numMarkTheadsInGC = 0;
                };
                Clr.GCStop += delegate(GCEndTraceData data)
                {
                    var process = Processes.GetProcess(data.ProcessID, data.TimeStamp100ns);
                    if (process == null)
                        return;
                    if (!process.isServerGC && process.numMarkTheadsInGC > 1)
                        process.isServerGC = true;
                };
 
                var aspNetParser = new AspNetTraceEventParser(rawEvents);
                aspNetParser.AspNetReqStart += delegate(AspNetStartTraceData data) { CategorizeThread(data, "Incoming Request Thread"); };
                Clr.GCFinalizersStart += delegate(GCNoUserDataTraceData data) { CategorizeThread(data, ".NET Finalizer Thread"); };
                Clr.ThreadPoolWorkerThreadStart += delegate(ThreadPoolWorkerThreadTraceData data) { CategorizeThread(data, ".NET ThreadPool"); };
                Clr.ThreadPoolWorkerThreadAdjustmentSample += delegate(ThreadPoolWorkerThreadAdjustmentSampleTraceData data)
                {
                    CategorizeThread(data, ".NET ThreadPool");
                };

                // Attribute CPU samples to processes.
                rawEvents.Kernel.PerfInfoSampleProf += delegate(SampledProfileTraceData data)
                {
                    // Avoid creating data structures for events we will throw away
                    if (data.TimeStamp100ns < start100ns || eventCount >= maxEventCount)
                        return;

                    if (data.ThreadID == 0)    // Don't count process 0 (idle)
                    {
                        removeFromStream = true;
                        return;
                    }

                    var process = Processes.GetOrCreateProcess(data.ProcessID, data.TimeStamp100ns);
                    var thread = Threads.GetOrCreateThread(data.ThreadID, data.TimeStamp100ns, process);
                    thread.cpuSamples++;
                };

                // We assume that the sampling interval is uniform over the trace.   We pick the start if it 
                // is there, otherwise the OLD value of the LAST set interval (since we RESET the interval at the end)
                // OR the OLD value at the end.  
                bool setSeen = false;
                bool startSeen = false;

                rawEvents.Kernel.PerfInfoCollectionStart += delegate(SampledProfileIntervalTraceData data)
                {
                    startSeen = true;
                    sampleProfileInterval100ns = data.NewInterval;
                };

                rawEvents.Kernel.PerfInfoSetInterval += delegate(SampledProfileIntervalTraceData data)
                {
                    setSeen = true;
                    if (!startSeen)
                        sampleProfileInterval100ns = data.OldInterval;
                };

                rawEvents.Kernel.PerfInfoSetInterval += delegate(SampledProfileIntervalTraceData data)
                {
                    if (!setSeen && !startSeen)
                        sampleProfileInterval100ns = data.OldInterval;
                };
            }

            // This callback will fire on every event that has an address that needs to be associated with
            // symbolic information (methodIndex, and source file line / name information).  
            Func<TraceEvent, Address, bool> addToCodeAddressMap = delegate(TraceEvent data, Address address)
            {
                TraceProcess process = Processes.GetOrCreateProcess(data.ProcessID, data.TimeStamp100ns);
                CodeAddressIndex codeAddressIndex = codeAddresses.GetOrCreateCodeAddressIndex(process, data.TimeStamp100ns, address);

                // I require that the list be sorted by event ID.  
                Debug.Assert(eventsToCodeAddresses.Count == 0 ||
                    eventsToCodeAddresses[eventsToCodeAddresses.Count - 1].EventIndex <= (EventIndex)eventCount);

                eventsToCodeAddresses.Add(new EventsToCodeAddressIndex(MaxEventIndex, address, codeAddressIndex));
                return true;
            };

            uint rawEventCount = 0;
            double rawInputSizeMB = rawEvents.Size / 1000000.0;
            var startTime = DateTime.Now;

            // While scanning over the stream, copy all data to the file. 
            rawEvents.EveryEvent += delegate(TraceEvent data)
            {
                // TODO FIX NOW, redundant.  Decide what to do here.  
                if (data.ProcessID > 0)
                {
                    // var process = Processes.GetOrCreateProcess(data.ProcessID, data.TimeStamp100ns);
                }


                // Show status every 128K events
                if ((rawEventCount & 0x1FFFF) == 0)
                {
                    if (options != null && options.ConversionLog != null)
                    {
                        if (rawEventCount == 0)
                        {
                            options.ConversionLog.WriteLine("[Opening a log file of size {0:n0} MB of duration {1:n1} sec.]",
                                rawInputSizeMB, rawEvents.SessionDuration.TotalSeconds);
                        }
                        else
                        {
                            var curOutputSizeMB = ((double)(uint)writer.GetLabel()) / 1000000.0;
                            var curDurationSec = (DateTime.Now - startTime).TotalSeconds;

                            var ratioOutputToInput = (double)eventCount / (double)rawEventCount;
                            var estimatedFinalSizeMB = Math.Max(rawInputSizeMB * ratioOutputToInput * 1.15, curOutputSizeMB * 1.02);
                            var ratioSizeComplete = curOutputSizeMB / estimatedFinalSizeMB;
                            var estTimeLeftSec = (int)(curDurationSec / ratioSizeComplete - curDurationSec);

                            var message = "";
                            if (data.TimeStamp100ns < start100ns)
                                message = "  Before StartMSec truncating";
                            else if (eventCount >= maxEventCount)
                                message = "  Hit MaxEventCount, truncating.";

                            options.ConversionLog.WriteLine(
                                "[Sec {0,4:f0} Read {1,10:n0} events. At {2,7:n0}ms.  Wrote {3,4:f0}MB ({4,3:f0}%).  EstDone {5,2:f0} min {6,2:f0} sec.{7}]",
                                curDurationSec,
                                rawEventCount,
                                data.TimeStampRelativeMSec,
                                curOutputSizeMB,
                                ratioSizeComplete * 100.0,
                                estTimeLeftSec / 60,
                                estTimeLeftSec % 60,
                                message);
                        }
                    }
                }
                rawEventCount++;
#if DEBUG
                if (data is UnhandledTraceEvent)
                {
                    Debug.Assert((byte)data.opcode != unchecked((byte)-1));        // Means PrepForCallback not done. 
                    Debug.Assert(data.TaskName != "ERRORTASK");
                    Debug.Assert(data.OpcodeName != "ERROROPCODE");
                }
#endif
                if (data.TimeStamp100ns < start100ns || eventCount >= maxEventCount)
                    return;
                // Sadly we have seen cases of merged ETL files where there are events past the end of the session.
                // This confuses later logic so insure that this does not happen.  Note that we also want the
                // any module-DCEnds to happen at sessionEndTime so we have to do this after processing all events
                if (data.TimeStamp100ns > sessionEndTime100ns)
                    sessionEndTime100ns = data.TimeStamp100ns;

                // Update the counts
                var countForEvent = stats.GetEventCounts(data);
                countForEvent.m_count++;
                countForEvent.m_eventDataLenTotal += data.EventDataLength;

                if (bookKeepingEvent)
                {
                    bookKeepingEvent = false;
                    if (bookeepingEventThatMayHaveStack)
                    {
                        // We log the event so that we don't get sperious warnings about not finding the event for a stack,
                        // but we mark the EventIndex as invalid so that we know not to actually log this stack.  
                        pastEventInfo.LogEvent(data, EventIndex.Invalid, countForEvent);
                        bookeepingEventThatMayHaveStack = false;
                    }
                    // But unless the user explicitly asked for them, we remove them from the trace.  
                    if (!options.KeepAllEvents)
                        return;
                }
                else
                {
                    // Remember the event (to attach latter Stack Events) and also log event counts in TraceStats
                    if (!noStack)
                        pastEventInfo.LogEvent(data, removeFromStream ? EventIndex.Invalid : ((EventIndex)eventCount), countForEvent);
                    else
                        noStack = false;
                    if (removeFromStream)
                    {
                        removeFromStream = false;
                        if (!options.KeepAllEvents)
                            return;
                    }
                    else // Remember any code address in the event.  
                        data.LogCodeAddresses(addToCodeAddressMap);
                }

                var extendedDataCount = data.eventRecord->ExtendedDataCount;
                if (extendedDataCount != 0)
                    ProcessExtendedData(data, extendedDataCount, countForEvent);

                if (numberOnPage >= eventsPerPage)
                {
                    // options.ConversionLog.WriteLine("Writing page " + this.eventPages.BatchCount, " Start " + writer.GetLabel());
                    this.eventPages.Add(new EventPageEntry(data.TimeStamp100ns, writer.GetLabel()));
                    numberOnPage = 0;
                }
                unsafe
                {
                    Debug.Assert(data.eventRecord->EventHeader.TimeStamp < long.MaxValue);
                    WriteBlob((IntPtr)data.eventRecord, writer, headerSize);
                    WriteBlob(data.userData, writer, (data.EventDataLength + 3 & ~3));
                }
                if (eventCount == 0)
                    firstEventTime100ns = data.TimeStamp100ns;
                numberOnPage++;
                eventCount++;
            };

            rawEvents.Process();                  // Run over the data. 
            if (eventCount >= maxEventCount)
            {
                if (options != null && options.ConversionLog != null)
                    options.ConversionLog.WriteLine("Trucated events to {0:n} events.  Use /MaxEventCount to change.", maxEventCount);
            }

            // Finish off the processing of the ETW compressed stacks.  This means doing all the deferred Kernel stack processing
            // and conerting all pseudo-callStack indexes into real ones. 
            for (int i = 0; i < activeEventStackInfos.Count; i++)
            {
                var stackInfo = activeEventStackInfos[i];
                if (stackInfo.Thread != null)
                {
                    // we have a orphan entry.  
                    EmitStackForEventIfReady(stackInfo, true);
                }
            }

            freeEventStackInfos = null;
            activeEventStackInfos.Clear();
            etwStackKeyToInfo = null;

            // TODO FIX NOW hack because unloadMethod not present 
            foreach (var jittedMethod in jittedMethods)
                codeAddresses.AddMethod(jittedMethod);

            foreach (var jsJittedMethod in jsJittedMethods)
                codeAddresses.AddMethod(jsJittedMethod, sourceFilesByID);

            // Make sure that all threads have a process 
            foreach (var thread in Threads)
            {
                if (thread.process == null)
                {
                    DebugWarn(true, "Warning: could not determine the process for thread " + thread.ThreadID, null);
                    var unknownProcess = Processes.GetOrCreateProcess(-1, 0);
                    unknownProcess.imageFileName = "UNKNOWN_PROCESS";
                    thread.process = unknownProcess;
                }
                thread.Process.cpuSamples += thread.cpuSamples;         // Roll up CPU to the process. 
            }

            // Make sure we are not missing any ImageEnds that we have ImageStarts for.   
            foreach (var process in Processes)
            {
                foreach (var module in process.LoadedModules)
                {
                    // We did not unload the module 
                    if (module.unloadTime100ns == ETWTraceEventSource.MaxTime100ns)
                    {
                        // simulate a module unload, and resolve all code addresses in the module's range.   
                        CodeAddresses.ForAllUnresolvedCodeAddressesInRange(module.ImageBase, module.ModuleFile.ImageSize, process, null,
                            delegate(TraceProcess proc, ref TraceCodeAddresses.CodeAddressInfo info)
                            {
                                if (info.ModuleFileIndex == Diagnostics.Tracing.ModuleFileIndex.Invalid)
                                    info.ModuleFileIndex = module.ModuleFile.ModuleFileIndex;
                            });
                    }
                    if (module.unloadTime100ns > sessionEndTime100ns)
                        module.unloadTime100ns = sessionEndTime100ns;
                }

                if (process.endTime100ns > sessionEndTime100ns)
                    process.endTime100ns = sessionEndTime100ns;
            }

            // Sum up the module level statistics for code addresses.  
            for (int codeAddrIdx = 0; codeAddrIdx < CodeAddresses.MaxCodeAddressIndex; codeAddrIdx++)
            {
                var inclusiveCount = CodeAddresses.codeAddresses[codeAddrIdx].InclusiveCount;

                var moduleIdx = CodeAddresses.ModuleFileIndex((CodeAddressIndex)codeAddrIdx);
                if (moduleIdx != ModuleFileIndex.Invalid)
                {
                    var module = CodeAddresses.ModuleFiles[moduleIdx];
                    module.codeAddressesInModule += inclusiveCount;
                }
                CodeAddresses.totalCodeAddresses += inclusiveCount;
            }
#if DEBUG
            // Confirm that the CPU stats make sense.  
            foreach (var process in Processes)
            {
                float cpuFromThreads = 0;
                foreach (var thread in process.Threads)
                    cpuFromThreads += thread.CPUMSec;
                Debug.Assert(Math.Abs(cpuFromThreads - process.CPUMSec) < .01);     // We add up 
            }
#endif
            this.sessionStartTimeQPC = rawEvents.sessionStartTimeQPC;

            // Insert the event to stack table is in sorted order.  
            eventsToStacks.Sort(delegate(EventsToStackIndex x, EventsToStackIndex y)
            {
                return (int)x.EventIndex - (int)y.EventIndex;
            });

            Debug.Assert(eventCount % eventsPerPage == numberOnPage || numberOnPage == eventsPerPage || eventCount == 0);
            options.ConversionLog.WriteLine("{0} distinct processes.", processes.MaxProcessIndex);
            foreach (TraceProcess process in processes)
            {
                if (process.StartTime100ns > sessionStartTime100ns && process.ExitStatus.HasValue)
                    options.ConversionLog.WriteLine("  Process {0,-16} Start: {1,7:n3} End {1,7:n3}",
                        process.Name, process.StartTimeRelativeMsec, process.EndTimeRelativeMsec);
            }
            options.ConversionLog.WriteLine("Totals");
            options.ConversionLog.WriteLine("  {0,8:n0} events.", eventCount);
            options.ConversionLog.WriteLine("  {0,8:n0} events with stack traces.", eventsToStacks.Count);
            options.ConversionLog.WriteLine("  {0,8:n0} events with code addresses in them.", eventsToCodeAddresses.Count);
            options.ConversionLog.WriteLine("  {0,8:n0} total code address instances. (stacks or other)", codeAddresses.TotalCodeAddresses);
            options.ConversionLog.WriteLine("  {0,8:n0} unique code addresses. ", codeAddresses.MaxCodeAddressIndex);
            options.ConversionLog.WriteLine("  {0,8:n0} unique stacks.", callStacks.MaxCallStackIndex);
            options.ConversionLog.WriteLine("  {0,8:n0} unique managed methods parsed.", codeAddresses.Methods.MaxMethodIndex);
            options.ConversionLog.WriteLine("  {0,8:n0} CLR method event records.", codeAddresses.ManagedMethodRecordCount);
            this.options.ConversionLog.WriteLine("[Conversion complete {0:n0} events.  Conversion took {1:n0} sec.]",
                eventCount, (DateTime.Now - startTime).TotalSeconds);
        }

        private void UpdateStackInfoForStackEvent(EventStackInfo stackInfo, StackWalkRefTraceData asStackRef)
        {
            // Insert this eventInfo into the etwStackKeyToInfo list (which holds all events that need that def)
            EventStackInfo prevStackInfoWithSameKey;
            etwStackKeyToInfo.TryGetValue(asStackRef.StackKey, out prevStackInfoWithSameKey);
            var isKernelStackFragment = (asStackRef.Opcode == (TraceEventOpcode)37);
            if (isKernelStackFragment)
            {
                if (stackInfo.KernelStackKey != 0)
                {
                    DebugWarn(stackInfo.TimeStamp100ns != QPCTimeToFileTime(asStackRef.EventTimeStampQPC), "Two kernel fragments for the same event, dropping second one.", asStackRef);
                    return;
                }
                Debug.Assert(prevStackInfoWithSameKey == null || prevStackInfoWithSameKey.KernelStackKey == asStackRef.StackKey);
                stackInfo.KernelStackKey = asStackRef.StackKey;
                stackInfo.NextEventWithKernelKey = prevStackInfoWithSameKey;
                etwStackKeyToInfo[asStackRef.StackKey] = stackInfo;   // Add this to the list of events needing this key defintion.  
            }
            else
            {
                Debug.Assert(asStackRef.Opcode == (TraceEventOpcode)38);    // User stack Ref. 
                if (stackInfo.UserStackKey != 0)
                {
                    DebugWarn(false, "Two user fragments for the same event, dropping second one.", asStackRef);
                    return;
                }
                Debug.Assert(prevStackInfoWithSameKey == null || prevStackInfoWithSameKey.UserStackKey == asStackRef.StackKey);
                stackInfo.UserStackKey = asStackRef.StackKey;
                stackInfo.NextEventWithUserKey = prevStackInfoWithSameKey;
                etwStackKeyToInfo[asStackRef.StackKey] = stackInfo;   // Add this to the list of events needing this key defintion.  
            }
        }

        unsafe private void UpdateStackInfoForStackEvent(EventStackInfo stackInfo, StackWalkTraceData asStackWalk)
        {
            // If we don't have a kernel entry, and this has a kernel address then we assume it is a kernel stack fragment, otherwise we assume user stack fragment. 
            if (IsKernelAddress(asStackWalk.InstructionPointer(0), asStackWalk.PointerSize))
            {
                if (stackInfo.KernelStackKey != 0)
                {
                    DebugWarn(stackInfo.TimeStamp100ns != QPCTimeToFileTime(asStackWalk.EventTimeStampQPC), "Two kernel fragments for the same event, dropping second one.", asStackWalk);
                    return;
                }
                stackInfo.KernelStackKey = ulong.MaxValue;  // we use this special value to represent 'defined by a StackWalkEvent'
                stackInfo.KernelStackDefined = true;

                // If we don't have any fragment converted to a CallStackIndex, this means we have
                // not seen any UserMode stack.  We can't convert the kernel stack yet because we 
                // don't have the top of the stack yet.  So we simply remember the kernel info and go on.  
                // We know that the Idle and System process don't have user stacks, so we can agressively
                // convert them 
                var processID = stackInfo.Thread.Process.ProcessID;
                if (processID == 0 || processID == 4)
                {
                    stackInfo.UserStackKey = ulong.MaxValue;        // Indicate done to EmitStackForEventIfReady
                    stackInfo.UserStackDefined = true;
                }
                else if (stackInfo.StackIndex == CallStackIndex.Invalid)
                {
                    stackInfo.KernelStackData = asStackWalk.Clone();
                    return;
                }
            }
            else
            {
                if (stackInfo.UserStackKey != 0)
                {
                    DebugWarn(false, "Two user fragments for the same event, dropping second one.", asStackWalk);
                    return;
                }
                stackInfo.UserStackKey = ulong.MaxValue;  // we use this special value to represent 'defined by a StackWalkEvent'
                stackInfo.UserStackDefined = true;
            }

            // Accumulate the stack fragment we just got to what we already have.  
            stackInfo.StackIndex = callStacks.GetStackIndexForStackEvent(stackInfo.TimeStamp100ns,
                asStackWalk.InstructionPointers, asStackWalk.FrameCount, asStackWalk.PointerSize == 8, stackInfo.Thread, stackInfo.StackIndex);
        }

        /// <summary>
        /// Determine if 'stackInfo' is complete and if so emit it to the 'eventsToStacks' array.  If 'force' is true 
        /// then force what information there is out even if it is not complete (there is nothing else comming). 
        /// </summary>
        unsafe private void EmitStackForEventIfReady(EventStackInfo stackInfo, bool force = false)
        {
            if (stackInfo.UserStackDefined || force)
            {
                if (force)
                {
                    DebugWarn(stackInfo.UserStackKey == 0 || stackInfo.UserStackDefined, "Could not find Def for user stack key " + stackInfo.UserStackKey.ToString("x"), null);
                    DebugWarn(stackInfo.KernelStackKey == 0 || stackInfo.KernelStackDefined, "Could not find Def for kernel stack key " + stackInfo.UserStackKey.ToString("x"), null);
                }

                if (stackInfo.KernelStackData != null)  // If we had to defer some KernelStack processing, do it now.  
                {
                    Debug.Assert(stackInfo.KernelStackDefined);
                    var asStackWalk = stackInfo.KernelStackData as StackWalkTraceData;
                    if (asStackWalk != null)
                    {
                        stackInfo.StackIndex = callStacks.GetStackIndexForStackEvent(stackInfo.TimeStamp100ns,
                            asStackWalk.InstructionPointers, asStackWalk.FrameCount, asStackWalk.PointerSize == 8,
                            stackInfo.Thread, stackInfo.StackIndex);
                    }
                    else
                    {
                        var asStackRef = stackInfo.KernelStackData as StackWalkDefTraceData;
                        stackInfo.StackIndex = callStacks.GetStackIndexForStackEvent(stackInfo.TimeStamp100ns,
                            asStackRef.InstructionPointers, asStackRef.FrameCount, asStackRef.PointerSize == 8,
                            stackInfo.Thread, stackInfo.StackIndex);
                    }
                    stackInfo.KernelStackData = null;
                }

                // Do we have everything?  If so Emit the actual EventsToStackIndex entry and free the StackInfo structure. 
                if (stackInfo.KernelStackDefined || stackInfo.KernelStackKey == 0)
                {
                    eventsToStacks.Add(new EventsToStackIndex(stackInfo.EventIndex, stackInfo.StackIndex));
                    // We don't free when we force because we know we will throw things away in bulk shortly afterward.  
                    if (!force)
                        FreeEventStackInfo(stackInfo);
                }
            }
        }

        private void FreeEventStackInfo(EventStackInfo toFree)
        {
            toFree.Thread = null;                       // Mark as free.  
            toFree.NextEventWithUserKey = null;         // Not strictly needed.  
            toFree.NextEventWithKernelKey = freeEventStackInfos;
            freeEventStackInfos = toFree;
        }

        private EventStackInfo AllocateEventStackInfo(EventIndex eventIndex, TraceThread thread, long timeStamp100ns)
        {
            var ret = freeEventStackInfos;
            if (ret == null)
            {
                ret = new EventStackInfo();
#if DEBUG
                ret.Index = activeEventStackInfos.Count;        // Lets me find it
#endif
                activeEventStackInfos.Add(ret);     // Keep track of it.  
            }
            else
            {
                freeEventStackInfos = ret.NextEventWithKernelKey;
                ret.NextEventWithKernelKey = null;
                ret.NextEventWithUserKey = null;
                ret.KernelStackData = null;
                ret.KernelStackKey = 0;
                ret.UserStackKey = 0;
                ret.UserStackDefined = false;
                ret.KernelStackDefined = false;
            }
            ret.StackIndex = CallStackIndex.Invalid;
            ret.EventIndex = eventIndex;
            ret.Thread = thread;
            ret.TimeStamp100ns = timeStamp100ns;
            return ret;
        }

        // Note that this contains both stack infos that are incomplete as well as free stack infos (those with Thread == null)
        // This is used after event processing to pick up 'stragglers' that have only partial information (but we can still
        // emit that partial infomation).  
        GrowableArray<EventStackInfo> activeEventStackInfos;

        // this is a linked list of unused EventStackInfos.  
        EventStackInfo freeEventStackInfos;

        // For event ETWStackKey we hold a linked list of EventStackInfo of events that used that key.  
        Dictionary<Address, EventStackInfo> etwStackKeyToInfo = new Dictionary<Address, EventStackInfo>();

        /// <summary>
        /// Holds information Stacks associated with an event.  THis is a transient strucutre.  We only need it 
        /// until all the information is collected for a particular event, at which point we can create a 
        /// CallStackIndex for the stack and eventsToStacks table.  
        /// </summary>
        internal class EventStackInfo
        {
            // Stuff about the event itself.  
            public EventIndex EventIndex;
            public TraceThread Thread;      // Needed to compute very top of call stack. 
            public long TimeStamp100ns;     // Used to look up the module.  
            public double TimeStampRelMSec { get { return Thread.Process.Log.RelativeTimeMSec(TimeStamp100ns); } }

            // The raw stack keys used in ETW compressed stacks to represent the stack fragments. 
            public Address KernelStackKey;
            public Address UserStackKey;
            public bool UserStackDefined;
            public bool KernelStackDefined;

            public EventStackInfo NextEventWithKernelKey;     // We form a linked list of all events with a certain key
            public EventStackInfo NextEventWithUserKey;       // We form a linked list of all events with a certain key

            // If the kernel data comes in before the user stack (a common case), we can't create a stack index
            // (because we don't know the thread-end of the stack), so we simply remember it. 
            public TraceEvent KernelStackData;

            // If data for the user stack comes in first, we can convert it immediately to a stack index.
            public CallStackIndex StackIndex;
#if DEBUG
            public int Index;
#endif
        }

        /// <summary>
        /// Put the thread that owns 'data' in to the cateory 'category.  
        /// </summary>
        private void CategorizeThread(TraceEvent data, string category)
        {
            var thread = Threads.GetThread(data.ThreadID, data.TimeStamp100ns);
            if (thread == null)
                return;

            if (thread.threadInfo == null)
                thread.threadInfo = category;
        }

        internal static bool IsKernelAddress(Address ip, int pointerSize)
        {
            if (pointerSize == 4)
                return ip >= 0x80000000;
            return ip >= 0xFFFF000000000000;        // TODO I don't know what the true cutoff is.  
        }

        /// <summary>
        /// Process any extended data (like Win7 style stack traces) associated with 'data'
        /// </summary>
        private unsafe void ProcessExtendedData(TraceEvent data, ushort extendedDataCount, TraceEventCounts countForEvent)
        {
            var extendedData = data.eventRecord->ExtendedData;
            Debug.Assert(extendedData != null && extendedDataCount != 0);
            Guid* relatedActivityIDPtr = null;
            for (int i = 0; i < extendedDataCount; i++)
            {
                if (extendedData[i].ExtType == TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_STACK_TRACE64 ||
                    extendedData[i].ExtType == TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_STACK_TRACE32)
                {
                    bool is64BitStacks = extendedData[i].ExtType == TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_STACK_TRACE64;

                    var stackRecord = (TraceEventNativeMethods.EVENT_EXTENDED_ITEM_STACK_TRACE32*)extendedData->DataPtr;
                    // TODO Debug.Assert(stackRecord->MatchId == 0);
                    var addresses = &stackRecord->Address[0];
                    var addressesCount = (extendedData->DataSize - sizeof(ulong)) / sizeof(uint);
                    if (is64BitStacks)
                        addressesCount /= 2;

                    TraceProcess process = this.processes.GetOrCreateProcess(data.ProcessID, data.TimeStamp100ns);
                    TraceThread thread = this.Threads.GetOrCreateThread(data.ThreadID, data.TimeStamp100ns, process);
                    CallStackIndex callStackIndex = callStacks.GetStackIndexForStackEvent(
                        data.TimeStamp100ns, addresses, addressesCount, is64BitStacks, thread);
                    Debug.Assert(callStacks.Depth(callStackIndex) == addressesCount);

                    // Get the previous event on the same thread. 
                    var eventIndex = (EventIndex)eventCount;
                    eventsToStacks.Add(new EventsToStackIndex(eventIndex, callStackIndex));

                    countForEvent.m_stackCount++;   // Update stack counts
                }
                else if (extendedData[i].ExtType == TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_RELATED_ACTIVITYID)
                {
                    relatedActivityIDPtr = (Guid*)(extendedData->DataPtr);
                }
            }

            if (relatedActivityIDPtr != null)
            {
                // TODO This is a bit of a hack.   We wack these fields in place 
                // We encode this as index into the relatedActivityID GrowableArray.
                data.eventRecord->ExtendedDataCount = 1;
                data.eventRecord->ExtendedData = (TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM*)relatedActivityIDs.Count;
                relatedActivityIDs.Add(*relatedActivityIDPtr);
            }
        }

        protected override internal string ProcessName(int processID, long time100ns)
        {
            TraceProcess process = Processes.GetProcess(processID, time100ns);
            if (process == null)
                return base.ProcessName(processID, time100ns);
            return process.Name;
        }

        public override void Dispose()
        {
            Close();
        }
        unsafe private static void WriteBlob(IntPtr source, IStreamWriter writer, int byteCount)
        {
            Debug.Assert(unchecked((int)source) % 4 == 0);
            Debug.Assert(byteCount % 4 == 0);
            int* sourcePtr = (int*)source;
            int intCount = byteCount >> 2;
            while (intCount > 0)
            {
                writer.Write(*sourcePtr++);
                --intCount;
            }
        }

        // [Conditional("DEBUG")]
        internal void DebugWarn(bool condition, string message, TraceEvent data)
        {
            if (!condition)
            {
                TextWriter writer;
                if (options != null)
                    writer = options.ConversionLog;
                else
                    writer = Console.Out;

                writer.Write("WARNING: ");
                if (data != null)
                    writer.Write("Time: " + data.TimeStampRelativeMSec.ToString("f4").PadLeft(12) + " PID: " + data.ProcessID.ToString().PadLeft(4) + ": ");
                writer.WriteLine(message);

                ImageLoadTraceData asImageLoad = data as ImageLoadTraceData;
                if (asImageLoad != null)
                {
                    writer.WriteLine("    FILE: " + asImageLoad.FileName);
                    writer.WriteLine("    BASE: 0x" + asImageLoad.ImageBase.ToString("x"));
                    writer.WriteLine("    SIZE: 0x" + asImageLoad.ImageSize.ToString("x"));
                }
                ModuleLoadUnloadTraceData asModuleLoad = data as ModuleLoadUnloadTraceData;
                if (asModuleLoad != null)
                {
                    writer.WriteLine("    NGEN:     " + asModuleLoad.ModuleNativePath);
                    writer.WriteLine("    ILFILE:   " + asModuleLoad.ModuleILPath);
                    writer.WriteLine("    MODULEID: 0x" + ((ulong)asModuleLoad.ModuleID).ToString("x"));
                }
                MethodLoadUnloadVerboseTraceData asMethodLoad = data as MethodLoadUnloadVerboseTraceData;
                if (asMethodLoad != null)
                {
                    writer.WriteLine("    METHOD:   " + GetFullName(asMethodLoad));
                    writer.WriteLine("    MODULEID: " + ((ulong)asMethodLoad.ModuleID).ToString("x"));
                    writer.WriteLine("    START:    " + ((ulong)asMethodLoad.MethodStartAddress).ToString("x"));
                    writer.WriteLine("    LENGTH:   " + asMethodLoad.MethodSize.ToString("x"));
                }
            }
        }
        internal static string GetFullName(MethodLoadUnloadVerboseTraceData data)
        {
            string sig = data.MethodSignature;
            int parens = sig.IndexOf('(');
            string args;
            if (parens >= 0)
                args = sig.Substring(parens);
            else
                args = "";
            string fullName = data.MethodNamespace + "." + data.MethodName + args;
            return fullName;
        }

        internal int FindPageIndex(long time100ns)
        {
            int pageIndex;
            // TODO error conditions. 
            // TODO? extra copy of EventPageEntry during search.  
            eventPages.BinarySearch(time100ns, out pageIndex, delegate(long targetTime100ns, EventPageEntry entry)
            {
                return targetTime100ns.CompareTo(entry.Time100ns);
            });
            // TODO completely empty logs.  
            if (pageIndex < 0)
                pageIndex = 0;
            return pageIndex;
        }

        /// <summary>
        /// Advance 'reader' until it point at a event that occurs on or after 'time100ns'.  on page
        /// 'pageIndex'.  If 'positions' is non-null, fill in that array.  Also return the index in
        /// 'positions' for the entry that was found.  
        /// </summary>
        internal unsafe void SeekToTimeOnPage(PinnedStreamReader reader, long time100ns, int pageIndex, out int indexOnPage, StreamLabel[] positions)
        {
            reader.Goto(eventPages[pageIndex].Position);
            int i = -1;
            while (i < TraceLog.eventsPerPage - 1)
            {
                i++;
                if (positions != null)
                    positions[i] = reader.Current;
                TraceEventNativeMethods.EVENT_RECORD* ptr = (TraceEventNativeMethods.EVENT_RECORD*)reader.GetPointer(headerSize);

                // Header sanity checks.
                Debug.Assert(ptr->EventHeader.Level <= 6);
                Debug.Assert(ptr->EventHeader.Version <= 4);

                long eventTime100ns = QPCTimeToFileTime(ptr->EventHeader.TimeStamp);
                Debug.Assert(sessionStartTime100ns <= eventTime100ns && eventTime100ns < DateTime.Now.Ticks || eventTime100ns == long.MaxValue);

                if (eventTime100ns >= time100ns)
                    break;

                int eventDataLength = ptr->UserDataLength;
                Debug.Assert(eventDataLength < 0x20000);
                reader.Skip(headerSize + ((eventDataLength + 3) & ~3));
            }
            indexOnPage = i;
        }

        internal unsafe PinnedStreamReader AllocReader()
        {
            if (freeReader == null)
                freeReader = ((PinnedStreamReader)lazyRawEvents.Deserializer.Reader).Clone();
            PinnedStreamReader ret = freeReader;
            freeReader = null;
            return ret;
        }
        internal unsafe void FreeReader(PinnedStreamReader reader)
        {
            if (freeReader == null)
                freeReader = reader;
        }
        internal unsafe TraceEventDispatcher AllocLookup()
        {
            if (freeLookup == null)
                freeLookup = sourceWithRegisteredParsers.Clone();
            TraceEventDispatcher ret = freeLookup;
            freeLookup = null;
            return ret;
        }
        internal unsafe void FreeLookup(TraceEventDispatcher lookup)
        {
            if (freeLookup == null)
                freeLookup = lookup;
        }

        private unsafe void InitializeFromFile(string etlxFilePath)
        {
            // If this Assert files, fix the declaration of code:headerSize to match
            Debug.Assert(sizeof(TraceEventNativeMethods.EVENT_HEADER) == 0x50 && sizeof(TraceEventNativeMethods.ETW_BUFFER_CONTEXT) == 4);

            Deserializer deserializer = new Deserializer(new PinnedStreamReader(etlxFilePath, 0x10000), etlxFilePath);

            // when the deserializer needs a TraceLog we return the current instance.  We also assert that
            // we only do this once.  
            deserializer.RegisterFactory(typeof(TraceLog), delegate
            {
                Debug.Assert(sessionStartTime100ns == 0 && sessionEndTime100ns == 0);
                return this;
            });
            deserializer.RegisterFactory(typeof(TraceProcess), delegate { return new TraceProcess(0, null, 0); });
            deserializer.RegisterFactory(typeof(TraceProcesses), delegate { return new TraceProcesses(null); });
            deserializer.RegisterFactory(typeof(TraceThreads), delegate { return new TraceThreads(null); });
            deserializer.RegisterFactory(typeof(TraceThread), delegate { return new TraceThread(0, null, (ThreadIndex)0); });
            deserializer.RegisterFactory(typeof(TraceModuleFiles), delegate { return new TraceModuleFiles(null); });
            deserializer.RegisterFactory(typeof(TraceModuleFile), delegate { return new TraceModuleFile(null, 0, 0); });
            deserializer.RegisterFactory(typeof(TraceMethods), delegate { return new TraceMethods(null); });
            deserializer.RegisterFactory(typeof(TraceCodeAddresses), delegate { return new TraceCodeAddresses(null, null); });
            deserializer.RegisterFactory(typeof(TraceCallStacks), delegate { return new TraceCallStacks(null, null); });
            deserializer.RegisterFactory(typeof(TraceEventStats), delegate { return new TraceEventStats(null); });
            deserializer.RegisterFactory(typeof(TraceEventCounts), delegate { return new TraceEventCounts(null, null); });

            deserializer.RegisterFactory(typeof(TraceLoadedModules), delegate { return new TraceLoadedModules(null); });
            deserializer.RegisterFactory(typeof(TraceLoadedModule), delegate { return new TraceLoadedModule(null, null, 0UL); });
            deserializer.RegisterFactory(typeof(TraceManagedModule), delegate { return new TraceManagedModule(null, null, 0L); });

            deserializer.RegisterFactory(typeof(ProviderManifest), delegate
            {
                return new ProviderManifest(null, ManifestEnvelope.ManifestFormats.SimpleXmlFormat, 0, 0);
            });
            deserializer.RegisterFactory(typeof(DynamicTraceEventData), delegate
            {
                return new DynamicTraceEventData(null, 0, 0, null, Guid.Empty, 0, null, Guid.Empty, null);
            });

            // when the serserializer needs any TraceEventParser class, we assume that its constructor
            // takes an argument of type TraceEventSource and that you can pass null to make an
            // 'empty' parser to fill in with FromStream.  
            deserializer.RegisterDefaultFactory(delegate(Type typeToMake)
            {
                if (typeToMake.IsSubclassOf(typeof(TraceEventParser)))
                    return (IFastSerializable)Activator.CreateInstance(typeToMake, new object[] { null });
                return null;
            });

            IFastSerializable entry = deserializer.GetEntryObject();
            // TODO this needs to be a runtime error, not an assert.  
            Debug.Assert(entry == this);
            // Our deserializer is now attached to our defered events.  
            Debug.Assert(lazyRawEvents.Deserializer == deserializer);

            this.etlxFilePath = etlxFilePath;

            // Sanity checking.  
            Debug.Assert(pointerSize == 4 || pointerSize == 8, "Bad pointer size");
            Debug.Assert(10 <= cpuSpeedMHz && cpuSpeedMHz <= 100000, "Bad cpu speed");
            Debug.Assert(0 < numberOfProcessors && numberOfProcessors < 1024, "Bad number of processors");
            Debug.Assert(0 < MaxEventIndex);

            if (eventsLost > 0 && options != null)
                options.ConversionLog.WriteLine("Warning: " + eventsLost + " events were lost");
        }

#if DEBUG
        /// <summary>
        /// Returns true if 'str' has only normal ascii (printable) characters.
        /// </summary>
        static internal bool NormalChars(string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                Char c = str[i];
                if (c < ' ' && !Char.IsWhiteSpace(c) || '~' < c)
                    return false;
            }
            return true;
        }
#endif
        void IFastSerializable.ToStream(Serializer serializer)
        {
            // Write out the events themselves, Before we do this we write a reference past the end of the
            // events so we can skip them without actually reading them. 
            // The real work is done in code:CopyRawEvents

            // Align to 8 bytes
            StreamLabel pos = serializer.Writer.GetLabel();
            int align = ((int)pos + 1) & 7;          // +1 take into acount we always write the count
            if (align > 0)
                align = 8 - align;
            serializer.Write((byte)align);
            for (int i = 0; i < align; i++)
                serializer.Write((byte)0);
            Debug.Assert((int)serializer.Writer.GetLabel() % 8 == 0);

            serializer.Log("<Marker name=\"RawEvents\"/>");
            lazyRawEvents.Write(serializer, delegate
            {
                // Get the events from a given raw stream
                TraceEventDispatcher dispatcher = rawEventSourceToConvert;
                if (dispatcher == null)
                    dispatcher = events.GetSource();
                CopyRawEvents(dispatcher, serializer.Writer);
                // Write sentinal event with a long.MaxValue timestamp mark the end of the data. 
                for (int i = 0; i < 11; i++)
                {
                    if (i == 2)
                        serializer.Write(long.MaxValue);
                    else
                        serializer.Write((long)0);          // The important field here is the EventDataSize field 
                }

                if (HasCallStacks || options.AlwaysResolveSymbols)
                    codeAddresses.LookupSymbols(options);
            });

            serializer.Log("<Marker name=\"sessionStartTime100ns\"/>");
            serializer.Write(sessionStartTime100ns);
            serializer.Write(firstEventTime100ns);
            serializer.Write(sessionEndTime100ns);
            serializer.Write(pointerSize);
            serializer.Write(numberOfProcessors);
            serializer.Write(cpuSpeedMHz);
            serializer.Write((byte)osVersion.Major);
            serializer.Write((byte)osVersion.Minor);
            serializer.Write((byte)osVersion.MajorRevision);
            serializer.Write((byte)osVersion.MinorRevision);
            serializer.Write(QPCFreq);
            serializer.Write(sessionStartTimeQPC);
            serializer.Write(eventsLost);
            serializer.Write(machineName);
            serializer.Write(memorySizeMeg);

            serializer.Write(processes);
            serializer.Write(threads);
            serializer.Write(codeAddresses);
            serializer.Write(stats);
            serializer.Write(callStacks);
            serializer.Write(moduleFiles);

            serializer.Log("<WriteColection name=\"eventPages\" count=\"" + eventPages.Count + "\">\r\n");
            serializer.Write(eventPages.Count);
            for (int i = 0; i < eventPages.Count; i++)
            {
                serializer.Write(eventPages[i].Time100ns);
                serializer.Write(eventPages[i].Position);
            }
            serializer.Write(eventPages.Count);                 // redundant as a checksum
            serializer.Log("</WriteColection>\r\n");
            serializer.Write(eventCount);

            serializer.Log("<Marker Name=\"eventsToStacks\"/>");
            lazyEventsToStacks.Write(serializer, delegate
            {
                serializer.Log("<WriteColection name=\"eventsToStacks\" count=\"" + eventsToStacks.Count + "\">\r\n");
                serializer.Write(eventsToStacks.Count);
                for (int i = 0; i < eventsToStacks.Count; i++)
                {
                    EventsToStackIndex eventToStack = eventsToStacks[i];
                    Debug.Assert(i == 0 || eventsToStacks[i - 1].EventIndex <= eventsToStacks[i].EventIndex, "event list not sorted");
                    serializer.Write((int)eventToStack.EventIndex);
                    serializer.Write((int)eventToStack.CallStackIndex);
                }
                serializer.Write(eventsToStacks.Count);             // Redundant as a checksum
                serializer.Log("</WriteColection>\r\n");
            });

            serializer.Log("<Marker Name=\"eventsToCodeAddresses\"/>");
            lazyEventsToCodeAddresses.Write(serializer, delegate
            {
                serializer.Log("<WriteColection name=\"eventsToCodeAddresses\" count=\"" + eventsToCodeAddresses.Count + "\">\r\n");
                serializer.Write(eventsToCodeAddresses.Count);
                foreach (EventsToCodeAddressIndex eventsToCodeAddress in eventsToCodeAddresses)
                {
                    serializer.Write((int)eventsToCodeAddress.EventIndex);
                    serializer.Write((long)eventsToCodeAddress.Address);
                    serializer.Write((int)eventsToCodeAddress.CodeAddressIndex);
                }
                serializer.Write(eventsToCodeAddresses.Count);       // Redundant as a checksum
                serializer.Log("</WriteColection>\r\n");
            });

            serializer.Log("<WriteColection name=\"userData\" count=\"" + userData.Count + "\">\r\n");
            serializer.Write(userData.Count);
            foreach (KeyValuePair<string, object> pair in UserData)
            {
                serializer.Write(pair.Key);
                IFastSerializable asFastSerializable = (IFastSerializable)pair.Value;
                serializer.Write(asFastSerializable);
            }
            serializer.Write(userData.Count);                   // Redundant as a checksum
            serializer.Log("</WriteColection>\r\n");

            serializer.Log("<WriteColection name=\"parsers\" count=\"" + parsers.Count + "\">\r\n");
            serializer.Write(parsers.Count);
            for (int i = 0; i < parsers.Count; i++)
                serializer.Write(parsers[i].GetType().FullName);
            serializer.Write(parsers.Count);                    // redundant as a checksum
            serializer.Log("</WriteColection>\r\n");

            serializer.Write(sampleProfileInterval100ns);
            serializer.Write(osName);
            serializer.Write(osBuild);
            serializer.Write(bootTime100ns);
            serializer.Write(hasPdbInfo);

            serializer.Log("<WriteColection name=\"m_relatedActivityIds\" count=\"" + relatedActivityIDs.Count + "\">\r\n");
            serializer.Write(relatedActivityIDs.Count);
            for (int i = 0; i < relatedActivityIDs.Count; i++)
                serializer.Write(relatedActivityIDs[i]);
            serializer.Log("</WriteColection>\r\n");

            serializer.Write(truncated);
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            deserializer.Log("<Marker Name=\"RawEvents\"/>");
            byte align;
            deserializer.Read(out align);
            while (align > 0)
            {
                byte zero;
                deserializer.Read(out zero);
                --align;
            }
            Debug.Assert((int)deserializer.reader.Current % 8 == 0);    // We expect alignment. 

            // Skip all the raw events.  
            lazyRawEvents.Read(deserializer, null);

            deserializer.Log("<Marker Name=\"sessionStartTime100ns\"/>");
            deserializer.Read(out sessionStartTime100ns);
            deserializer.Read(out firstEventTime100ns);
            deserializer.Read(out sessionEndTime100ns);
            deserializer.Read(out pointerSize);
            deserializer.Read(out numberOfProcessors);
            deserializer.Read(out cpuSpeedMHz);
            osVersion = new Version(deserializer.ReadByte(), deserializer.ReadByte(), deserializer.ReadByte(), deserializer.ReadByte());
            deserializer.Read(out _QPCFreq);
            deserializer.Read(out sessionStartTimeQPC);
            deserializer.Read(out eventsLost);
            deserializer.Read(out machineName);
            deserializer.Read(out memorySizeMeg);

            deserializer.Read(out processes);
            deserializer.Read(out threads);
            deserializer.Read(out codeAddresses);
            deserializer.Read(out stats);
            deserializer.Read(out callStacks);
            deserializer.Read(out moduleFiles);

            deserializer.Log("<Marker Name=\"eventPages\"/>");
            int count = deserializer.ReadInt();
            eventPages = new GrowableArray<EventPageEntry>(count + 1);
            EventPageEntry entry = new EventPageEntry();
            for (int i = 0; i < count; i++)
            {
                deserializer.Read(out entry.Time100ns);
                deserializer.Read(out entry.Position);
                eventPages.Add(entry);
            }
            int checkCount = deserializer.ReadInt();
            if (count != checkCount)
                throw new SerializationException("Redundant count check fail.");
            deserializer.Read(out eventCount);

            lazyEventsToStacks.Read(deserializer, delegate
            {
                int stackCount = deserializer.ReadInt();
                deserializer.Log("<Marker name=\"eventToStackIndex\" count=\"" + stackCount + "\"/>");
                eventsToStacks = new GrowableArray<EventsToStackIndex>(stackCount + 1);
                EventsToStackIndex eventToStackIndex = new EventsToStackIndex();
                for (int i = 0; i < stackCount; i++)
                {
                    eventToStackIndex.EventIndex = (EventIndex)deserializer.ReadInt();
                    Debug.Assert((int)eventToStackIndex.EventIndex < eventCount);
                    eventToStackIndex.CallStackIndex = (CallStackIndex)deserializer.ReadInt();
                    eventsToStacks.Add(eventToStackIndex);
                }
                int stackCheckCount = deserializer.ReadInt();
                if (stackCount != stackCheckCount)
                    throw new SerializationException("Redundant count check fail.");

            });
            lazyEventsToStacks.FinishRead();        // TODO REMOVE

            lazyEventsToCodeAddresses.Read(deserializer, delegate
            {
                int codeAddressCount = deserializer.ReadInt();
                deserializer.Log("<Marker Name=\"eventToCodeAddressIndex\" count=\"" + codeAddressCount + "\"/>");
                eventsToCodeAddresses = new GrowableArray<EventsToCodeAddressIndex>(codeAddressCount + 1);
                EventsToCodeAddressIndex eventToCodeAddressIndex = new EventsToCodeAddressIndex();
                for (int i = 0; i < codeAddressCount; i++)
                {
                    eventToCodeAddressIndex.EventIndex = (EventIndex)deserializer.ReadInt();
                    deserializer.ReadAddress(out eventToCodeAddressIndex.Address);
                    eventToCodeAddressIndex.CodeAddressIndex = (CodeAddressIndex)deserializer.ReadInt();
                    eventsToCodeAddresses.Add(eventToCodeAddressIndex);
                }
                int codeAddressCheckCount = deserializer.ReadInt();
                if (codeAddressCount != codeAddressCheckCount)
                    throw new SerializationException("Redundant count check fail.");
            });
            lazyEventsToCodeAddresses.FinishRead();        // TODO REMOVE

            count = deserializer.ReadInt();
            deserializer.Log("<Marker Name=\"userData\" count=\"" + count + "\"/>");
            for (int i = 0; i < count; i++)
            {
                string key;
                deserializer.Read(out key);
                IFastSerializable value = deserializer.ReadObject();
                userData[key] = value;
            }
            checkCount = deserializer.ReadInt();
            if (count != checkCount)
                throw new SerializationException("Redundant count check fail.");

            deserializer.Log("<Marker Name=\"parsers\"/>");
            count = deserializer.ReadInt();
            for (int i = 0; i < count; i++)
            {
                string fullTypeName = deserializer.ReadString();
                Type type = Type.GetType(fullTypeName, true);
                ConstructorInfo constructor = type.GetConstructor(new Type[] { typeof(TraceEventSource) });
                if (constructor == null)
                    throw new SerializationException("Type: " + fullTypeName + " does not have a constructor taking a TraceSource");
                TraceEventParser parser = (TraceEventParser)constructor.Invoke(new object[] { this });

                var asDynamic = parser as DynamicTraceEventParser;
                if (asDynamic != null)
                    _Dynamic = asDynamic;

                var asKernel = parser as KernelTraceEventParser;
                if (asKernel != null)
                    _Kernel = asKernel;

                var asClr = parser as ClrTraceEventParser;
                if (asClr != null)
                    _CLR = asClr;

                parsers.Add(parser);
            }
            checkCount = deserializer.ReadInt();    // TODO make this checksumming automatic. 
            if (count != checkCount)
                throw new SerializationException("Redundant count check fail.");

            deserializer.Read(out sampleProfileInterval100ns);
            deserializer.Read(out osName);
            deserializer.Read(out osBuild);
            deserializer.Read(out bootTime100ns);
            deserializer.Read(out hasPdbInfo);

            count = deserializer.ReadInt();
            Guid guid;
            relatedActivityIDs.Clear();
            for (int i = 0; i < count; i++)
            {
                deserializer.Read(out guid);
                relatedActivityIDs.Add(guid);
            }
        }
        int IFastSerializableVersion.Version
        {
            get { return 36; }
        }
        int IFastSerializableVersion.MinimumVersionCanRead
        {
            // We don't support backward compatibility for now.  
            get { return ((IFastSerializableVersion)this).Version; }
        }
        int IFastSerializableVersion.MinimumReaderVersion
        {
            // We don't support old readers reading new formats.  
            get { return ((IFastSerializableVersion)this).Version; }
        }

        // headerSize is the size we persist of code:TraceEventNativeMethods.EVENT_RECORD which is up to and
        // including the UserDataLength field (after this field the fields are architecture dependent in
        // size. 
        // TODO: we add 16 just to keep compatibility with the size we used before.  This is a complete
        // waste at the moment.  When we decide to break compatibility we should reclaim this.  
        internal const int headerSize = 0x50 /* EVENT_HEADER */ + 4 /* ETW_BUFFER_CONTEXT */ + 4 /* 2 shorts */ + 16;

        // #TraceLogVars
        // see code:#TraceEventVars
        private string etlxFilePath;
        protected long firstEventTime100ns;
        private int memorySizeMeg;
        private string osName;
        private string osBuild;
        private long bootTime100ns;
        private bool hasPdbInfo;
        private bool truncated;     // stopped because the file was too large.  
        private int sampleProfileInterval100ns;
        private string machineName;
        private TraceProcesses processes;
        private TraceThreads threads;
        private TraceCallStacks callStacks;
        private TraceCodeAddresses codeAddresses;
        private TraceEventStats stats;

        private DeferedRegion lazyRawEvents;
        private DeferedRegion lazyEventsToStacks;
        private DeferedRegion lazyEventsToCodeAddresses;
        private TraceEvents events;
        private GrowableArray<EventPageEntry> eventPages;   // The offset offset of a page
        private int eventCount;                             // Total number of events
        private TraceModuleFiles moduleFiles;
        private GrowableArray<EventsToStackIndex> eventsToStacks;
        private GrowableArray<EventsToCodeAddressIndex> eventsToCodeAddresses;

        private TraceEventDispatcher freeLookup;    // Try to reused old ones. 
        private PinnedStreamReader freeReader;

        private List<TraceEventParser> parsers;
        internal GrowableArray<Guid> relatedActivityIDs;

        internal ETLXTraceEventSource sourceWithRegisteredParsers;

        #region EventPages
        internal const int eventsPerPage = 1024;    // We keep track of  where events are in 'pages' of this size.
        private struct EventPageEntry
        {
            public EventPageEntry(long Time100ns, StreamLabel Position)
            {
                this.Time100ns = Time100ns;
                this.Position = Position;
            }
            public long Time100ns;                      // Time for the first items in this page. 
            public StreamLabel Position;                // Offset to this page. 
        }
        #endregion

        // These clases are only used during conversion from ETL files 
        // They are not needed for ETLX consumption.  
        #region PastEventInfo
        enum PastEventInfoIndex { Invalid = -1 };

        /// <summary>
        /// We need to remember the the EventIndexes of the events that were 'just before' this event so we can
        /// associate eventToStack traces with the event that actually caused them.  PastEventInfo does this.  
        /// </summary>
        struct PastEventInfo
        {
            public PastEventInfo(TraceLog log)
            {
                this.log = log;
                pastEventInfo = new PastEventInfoEntry[historySize];
                curPastEventInfo = 0;
                Debug.Assert(((historySize - 1) & historySize) == 0);       // historySize is a power of 2 
            }
            public void LogEvent(TraceEvent data, EventIndex eventIndex, TraceEventCounts countForEvent)
            {
                int threadID = data.ThreadID;
                // Thread an process events need to be munged slightly.  
                if (data.ParentThread >= 0)
                {
                    Debug.Assert(data is ProcessTraceData || data is ThreadTraceData);
                    threadID = data.ParentThread;
                }

                pastEventInfo[curPastEventInfo].ThreadID = threadID;
                pastEventInfo[curPastEventInfo].QPCTime = data.TimeStampQPC;
                pastEventInfo[curPastEventInfo].EventIndex = eventIndex;

                pastEventInfo[curPastEventInfo].CountForEvent = countForEvent;
                pastEventInfo[curPastEventInfo].isClrEvent = (data.ProviderGuid == ClrTraceEventParser.ProviderGuid);
                curPastEventInfo = (curPastEventInfo + 1) & (historySize - 1);
            }

            public PastEventInfoIndex GetPreviousEventIndex(TraceEvent anEvent, bool clrEventsOnly)
            {
                int idx = curPastEventInfo;
                for (int i = 0; i < historySize; i++)
                {
                    --idx;
                    if (idx < 0)
                        idx = historySize - 1;
                    if (pastEventInfo[idx].ThreadID == anEvent.ThreadID)
                    {
                        if (!clrEventsOnly || pastEventInfo[idx].isClrEvent)
                            return (PastEventInfoIndex)idx;
                    }
                }
                return PastEventInfoIndex.Invalid;
            }

            // Find the event event on thread threadID to the given QPC timestamp
            public PastEventInfoIndex GetEventForQPC(long QPCTime, int threadID)
            {
                // There are times when we have the same timestamp for different events, thus we need to
                // choose the best one (thread IDs match), when we also have a 'poorer' match (when we don't
                // have a thread ID for the event) 
                int idx = curPastEventInfo;
                var ret = PastEventInfoIndex.Invalid;
                bool exactMatch = false;
                bool updateThread = false;
                for (int i = 0; i < historySize; i++)
                {
                    --idx;
                    if (idx < 0)
                        idx = historySize - 1;

                    // We match timestamps.  This is the main criteria 
                    if (QPCTime == pastEventInfo[idx].QPCTime)
                    {
                        if (threadID == pastEventInfo[idx].ThreadID)
                        {
                            if (exactMatch)
                            {
                                // We hope this does not happen, ambiguity: two events with the same timestamp and thread ID. 
                                // This seems to happen for CSWITCH and SAMPLING on the phone (where timestamps are coarse); 
                                log.DebugWarn(false, "Two events with the same Timestamp " +
                                    log.RelativeTimeMSec(log.QPCTimeToFileTime(QPCTime)).ToString("f4"), null);
                            }

                            exactMatch = true;
                            ret = (PastEventInfoIndex)idx;
                            updateThread = false;
                        }
                        // Some events, (like VirtualAlloc, ReadyThread) don't have the thread ID set
                        if (pastEventInfo[idx].ThreadID == -1 && !exactMatch)
                        {
                            ret = (PastEventInfoIndex)idx;
                            updateThread = true;                // we match against ThreadID == -1, remember the true thread forever.  
                        }
                    }
                    // Once we found a timestamp match, we stop searching when the timestamps no longer match.   
                    else if (ret != PastEventInfoIndex.Invalid)
                        break;
                }
                // Remember the thread ID that we were 'attached to'.  
                if (updateThread)
                {
                    Debug.Assert(pastEventInfo[(int)ret].ThreadID == -1);
                    pastEventInfo[(int)ret].ThreadID = threadID;
                }
                return ret;
            }

            /// <summary>
            /// Searches forward in time (until the current time) after 'eventIndex'
            /// for events on 'threadID'.   Returns PastEventInfoIndex.Invalid if there are none
            /// </summary>
            public PastEventInfoIndex GetNextEventIndex(PastEventInfoIndex eventIndex, int threadID)
            {
                int idx = (int)eventIndex;
                for (; ; )
                {
                    idx++;
                    if (idx >= historySize)
                        idx = 0;
                    if (idx == curPastEventInfo)
                        return PastEventInfoIndex.Invalid;
                    if (pastEventInfo[idx].ThreadID == threadID)
                        return (PastEventInfoIndex)idx;
                }
            }
            public bool IsClrEvent(PastEventInfoIndex index) { return pastEventInfo[(int)index].isClrEvent; }
            public int GetThreadID(PastEventInfoIndex index) { return pastEventInfo[(int)index].ThreadID; }
            public EventIndex GetEventIndex(PastEventInfoIndex index) { return pastEventInfo[(int)index].EventIndex; }
            public TraceEventCounts GetEventCounts(PastEventInfoIndex index) { return pastEventInfo[(int)index].CountForEvent; }
            public long GetQPCTime(PastEventInfoIndex index) { return pastEventInfo[(int)index].QPCTime; }
            public EventStackInfo GetEventStackInfo(PastEventInfoIndex index)
            {
                var stackInfo = pastEventInfo[(int)index].EventStackInfo;
                if (stackInfo == null)
                    return null;

                // We reuse EventStackInfos agressively, make sure that this one is for us
                var eventIndex = GetEventIndex(index);
                if (stackInfo.EventIndex != eventIndex || stackInfo.Thread == null)
                    return null;
                return stackInfo;
            }
            public void SetEventStackInfo(PastEventInfoIndex index, EventStackInfo stackInfo) { pastEventInfo[(int)index].EventStackInfo = stackInfo; }

            /// <summary>
            /// Gets the next event between index and now that has a matching 'threadID'.  
            /// </summary>
            public PastEventInfoIndex GetNextEvent(PastEventInfoIndex index, int threadID)
            {
                int idx = (int)index;
                Debug.Assert(idx != curPastEventInfo);
                for (; ; )
                {
                    idx++;
                    if (idx >= historySize)
                        idx = 0;
                    if (idx == curPastEventInfo)
                        break;
                    if (pastEventInfo[idx].ThreadID == threadID)
                        return (PastEventInfoIndex)idx;
                }
                return PastEventInfoIndex.Invalid;
            }
            #region private
            // Stuff we remember about past events 
            private struct PastEventInfoEntry
            {
#if DEBUG
                public double TimeStampRelativeMSec(PastEventInfo pastEventInfo)
                { return pastEventInfo.log.RelativeTimeMSec(pastEventInfo.log.QPCTimeToFileTime(QPCTime)); }
#endif
                public bool isClrEvent;
                public long QPCTime;
                public int ThreadID;

                public EventStackInfo EventStackInfo;   // If this event actually had a stack, this holds info about it.  
                public EventIndex EventIndex;
                public TraceEventCounts CountForEvent;
            }

            const int historySize = 8192 * 2;          // Must be a power of 2
            PastEventInfoEntry[] pastEventInfo;
            int curPastEventInfo;                   // points at the first INVALD entry.  
            TraceLog log;
            #endregion
        }
        #endregion

        #region EventsToStackIndex
        internal struct EventsToStackIndex
        {
            public EventsToStackIndex(EventIndex eventIndex, CallStackIndex stackIndex)
            {
                EventIndex = eventIndex;
                CallStackIndex = stackIndex;
            }
            public EventIndex EventIndex;
            public CallStackIndex CallStackIndex;
        }

        private GrowableArray<EventsToStackIndex>.Comparison<EventIndex> stackComparer = delegate(EventIndex eventID, EventsToStackIndex elem)
            { return TraceEvent.Compare(eventID, elem.EventIndex); };

        #endregion

        #region EventsToCodeAddressIndex

        struct EventsToCodeAddressIndex
        {
            public EventsToCodeAddressIndex(EventIndex eventIndex, Address address, CodeAddressIndex codeAddressIndex)
            {
                EventIndex = eventIndex;
                Address = address;
                CodeAddressIndex = codeAddressIndex;
            }
            public EventIndex EventIndex;
            public Address Address;
            public CodeAddressIndex CodeAddressIndex;
        }
        private GrowableArray<EventsToCodeAddressIndex>.Comparison<EventIndex> CodeAddressComparer = delegate(EventIndex eventIndex, EventsToCodeAddressIndex elem)
            { return TraceEvent.Compare(eventIndex, elem.EventIndex); };

        #endregion

        // These are only used when converting from ETL
        internal TraceEventDispatcher rawEventSourceToConvert;      // used to convert from raw format only.  Null for ETLX files.
        internal TraceLogOptions options;
        #endregion
    }

    public class TraceEventStats : IEnumerable<TraceEventCounts>, IFastSerializable
    {
        public int Count { get { return m_counts.Count; } }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<TraceEventStats Count=").Append(XmlUtilities.XmlQuote(Count)).AppendLine(">");
            foreach (var counts in this)
                sb.Append("  ").Append(counts.ToString()).AppendLine();
            sb.AppendLine("</TraceEventStats>");
            return sb.ToString();
        }
        /// <summary>
        /// Given an event 'data' look up the statistics for events that type.  
        /// </summary>
        public TraceEventCounts GetEventCounts(TraceEvent data)
        {
            var countsForEvent = data.EventTypeUserData as TraceEventCounts;
            if (countsForEvent == null)
            {
                TraceEventCounts key = new TraceEventCounts(this, data);
                if (!m_counts.TryGetValue(key, out countsForEvent))
                {
                    countsForEvent = key;
                    m_counts.Add(key, key);
                }
                if (!(data is UnhandledTraceEvent))
                    data.EventTypeUserData = countsForEvent;
            }
#if DEBUG
            if (data.ClassicProvider)
            {
                Debug.Assert(countsForEvent.m_classicProvider);
                Debug.Assert(countsForEvent.TaskGuid == data.taskGuid);
                if (!data.lookupAsWPP)
                    Debug.Assert(countsForEvent.Opcode == data.Opcode);
            }
            else
            {
                Debug.Assert(!countsForEvent.m_classicProvider);
                Debug.Assert(countsForEvent.ProviderGuid == data.ProviderGuid);
                Debug.Assert(countsForEvent.EventID == data.ID);
            }

#endif
            return countsForEvent;
        }
        #region private

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(m_log);
            serializer.Write(m_counts.Count);
            foreach (var counts in m_counts.Keys)
                serializer.Write(counts);
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            deserializer.Read(out m_log);
            m_counts.Clear();
            int count = deserializer.ReadInt();
            for (int i = 0; i < count; i++)
            {
                TraceEventCounts elem; deserializer.Read(out elem);
                m_counts.Add(elem, elem);
            }
        }

        IEnumerator<TraceEventCounts> IEnumerable<TraceEventCounts>.GetEnumerator()
        {
            return m_counts.Keys.GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { throw new NotImplementedException(); }
        internal TraceEventStats(TraceLog log)
        {
            m_counts = new Dictionary<TraceEventCounts, TraceEventCounts>();
            m_log = log;
        }

        Dictionary<TraceEventCounts, TraceEventCounts> m_counts;      // really a set. 
        internal TraceLog m_log;
        #endregion
    }

    /// <summary>
    /// TraceEventCounts remembers some statistics about a particular event type.
    /// An event is determined by its ETW identity (provider guid and event id for
    /// manifest, task-opcode for classic).   This structure looks up the guid
    /// LAZILY to find the name, which means it is the providers at USE time
    /// not COLLECTION time that determine if names are found.  
    /// </summary>
    public class TraceEventCounts : IFastSerializable, IEquatable<TraceEventCounts>
    {
        public TraceEvent Template
        {
            get
            {
                if (!m_templateInited)
                {
                    m_template = m_stats.m_log.sourceWithRegisteredParsers.LookupTemplate(m_providerGuid, m_eventId, m_classicProvider);
                    m_templateInited = true;
                }
                return m_template;
            }
        }
        public string ProviderName
        {
            get
            {
                var template = Template;
                if (template == null)
                {
                    var name = ((ITraceParserServices)m_stats.m_log).ProviderNameForGuid(m_providerGuid);
                    if (name != null)
                        return name;
                    if (m_classicProvider)
                        return "UnknownProvider";
                    return "Provider(" + m_providerGuid.ToString() + ")";
                }
                return template.ProviderName;
            }
        }
        public string EventName
        {
            get
            {
                var template = Template;
                if (template == null)
                {
                    if (m_classicProvider)
                    {
                        var taskName = ((ITraceParserServices)m_stats.m_log).TaskNameForGuid(m_providerGuid);
                        if (taskName == null)
                            taskName = "Task(" + m_providerGuid.ToString() + ")";
                        if (m_eventId == 0)
                            return taskName;
                        return taskName + "/Opcode(" + ((int)m_eventId).ToString() + ")";
                    }
                    return "EventID(" + ((int)m_eventId).ToString() + ")";
                }
                return template.EventName;
            }
        }
        public bool IsClassic { get { return m_classicProvider; } }

        // Only meaningful if event is Manifest based (not classic)
        public Guid ProviderGuid { get { if (m_classicProvider) return Guid.Empty; else return m_providerGuid; } }
        public TraceEventID EventID { get { if (m_classicProvider) return TraceEventID.Illegal; else return m_eventId; } }

        // Only meaningful if the event is classic 
        public Guid TaskGuid { get { if (m_classicProvider) return m_providerGuid; else return Guid.Empty; } }
        public TraceEventOpcode Opcode { get { if (m_classicProvider) return (TraceEventOpcode)m_eventId; else return TraceEventOpcode.Info; } }

        public double AveragePayloadSize { get { return ((double)m_eventDataLenTotal) / m_count; } }
        public int Count { get { return m_count; } }
        public int StackCount { get { return m_stackCount; } }
        public string FullName
        {
            get
            {
                return ProviderName + "/" + EventName;
            }
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<TraceEventCounts");
            // TODO put in GUID, ID?  
            sb.Append(" ProviderName=").Append(XmlUtilities.XmlQuote(Template.ProviderName));
            sb.Append(" EventName=").Append(XmlUtilities.XmlQuote(Template.EventName));
            sb.Append(" Count=").Append(XmlUtilities.XmlQuote(Count));
            sb.Append(" StackCount=").Append(XmlUtilities.XmlQuote(StackCount));
            sb.AppendLine("/>");
            return sb.ToString();
        }
        #region private
        internal unsafe TraceEventCounts(TraceEventStats stats, TraceEvent data)
        {
            if (data == null)       // This happens in the deserialization case.  
                return;
            m_stats = stats;

            if (data.ClassicProvider)
            {
                m_providerGuid = data.taskGuid;

                // We use the sum of the opcode and eventID so that it works with WPP as well as classic.  
                Debug.Assert(data.eventRecord->EventHeader.Id == 0 || data.eventRecord->EventHeader.Opcode == 0);
                m_eventId = (TraceEventID)(data.eventRecord->EventHeader.Id + data.eventRecord->EventHeader.Opcode);
                m_classicProvider = true;
            }
            else
            {
                m_providerGuid = data.ProviderGuid;
                m_eventId = data.ID;
            }
        }

        bool IEquatable<TraceEventCounts>.Equals(TraceEventCounts other)
        {
            if (m_eventId != other.m_eventId)
                return false;
            if (m_classicProvider != other.m_classicProvider)
                return false;
            return (m_providerGuid == other.m_providerGuid);
        }
        public override int GetHashCode()
        {
            return m_providerGuid.GetHashCode() + (int)m_eventId;
        }

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(m_stats);
            serializer.Write(m_providerGuid);
            serializer.Write((int)m_eventId);
            serializer.Write(m_classicProvider);
            serializer.Write(m_count);
            serializer.Write(m_stackCount);
            serializer.Write(m_eventDataLenTotal);
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            deserializer.Read(out m_stats);
            deserializer.Read(out m_providerGuid);
            m_eventId = (TraceEventID)deserializer.ReadInt();
            deserializer.Read(out m_classicProvider);
            deserializer.Read(out m_count);
            deserializer.Read(out m_stackCount);
            deserializer.Read(out m_eventDataLenTotal);
        }

        TraceEventStats m_stats;
        internal bool m_classicProvider;     // This changes the meaning of m_providerGuid and m_eventId;
        internal Guid m_providerGuid;        // If classic this is task Guid
        TraceEventID m_eventId;              // If classic this is the opcode

        internal long m_eventDataLenTotal;
        internal int m_count;
        internal int m_stackCount;

        // Not serialized
        bool m_templateInited;
        TraceEvent m_template;
        #endregion
    }

    public class TraceEvents : IEnumerable<TraceEvent>
    {
        IEnumerator<TraceEvent> IEnumerable<TraceEvent>.GetEnumerator()
        {
            if (this.backwards)
                return new TraceEvents.BackwardEventEnumerator(this);
            else
                return new TraceEvents.ForwardEventEnumerator(this);
        }
        public IEnumerable<T> ByEventType<T>() where T : TraceEvent
        {
            foreach (TraceEvent anEvent in this)
            {
                T asTypedEvent = anEvent as T;
                if (asTypedEvent != null)
                    yield return asTypedEvent;
            }
        }
        public TraceEventDispatcher GetSource() { return new ETLXTraceEventSource(this); }
        public TraceEvents Backwards()
        {
            return new TraceEvents(log, startTime100ns, endTime100ns, predicate, true);
        }
        /// <summary>
        /// Filter the events by time.  both startime and endTime are INCLUSIVE. 
        /// </summary>
        public TraceEvents FilterByTime(DateTime startTime, DateTime endTime)
        {
            return FilterByTime(startTime.ToFileTime(), endTime.ToFileTime());
        }
        /// <summary>
        /// Filters by time expressed as MSec from the start of the trace. 
        /// </summary>
        public TraceEvents FilterByTime(double startTimeRelMSec, double endTimeRelMSec)
        {
            return FilterByTime(Log.RelativeTimeMSecTo100ns(startTimeRelMSec), Log.RelativeTimeMSecTo100ns(endTimeRelMSec));
        }
        /// <summary>
        /// Filter the events by time.  both startTime100ns and endTime100ns are INCLUSIVE. 
        /// </summary>
        public TraceEvents FilterByTime(long startTime100ns, long endTime100ns)
        {
            return Filter(startTime100ns, endTime100ns, null);
        }
        public TraceEvents Filter(Predicate<TraceEvent> predicate)
        {
            return Filter(0, TraceEventDispatcher.MaxTime100ns, predicate);
        }

        public TraceLog Log { get { return log; } }

        /// <summary>
        /// StartTime100ns for a code:TraceEvents is defined to be any time of the first event (or any time
        /// before it and after any event in the whole log that is before the first event in the
        /// TraceEvents).   
        /// </summary>
        public long StartTime100ns { get { return startTime100ns; } }
        public DateTime StartTime { get { return DateTime.FromFileTime(startTime100ns); } }
        public double StartTimeRelMSec { get { return log.RelativeTimeMSec(startTime100ns); } }
        public long EndTime100ns { get { return endTime100ns; } }
        public DateTime EndTime { get { return DateTime.FromFileTime(endTime100ns); } }
        public double EndTimeRelMSec { get { return log.RelativeTimeMSec(endTime100ns); } }

        #region private
        internal TraceEvents(TraceLog log)
        {
            this.log = log;
            this.endTime100ns = long.MaxValue - 100000000; // ten seconds from infinity
        }
        internal TraceEvents(TraceLog log, long startTime100ns, long endTime100ns, Predicate<TraceEvent> predicate, bool backwards)
        {
            this.log = log;
            this.startTime100ns = startTime100ns;
            this.endTime100ns = endTime100ns;
            this.predicate = predicate;
            this.backwards = backwards;
        }

        internal TraceEvents Filter(long startTime100ns, long endTime100ns, Predicate<TraceEvent> predicate)
        {
            // merge the two predicates
            if (predicate == null)
                predicate = this.predicate;
            else if (this.predicate != null)
            {
                Predicate<TraceEvent> predicate1 = this.predicate;
                Predicate<TraceEvent> predicate2 = predicate;
                predicate = delegate(TraceEvent anEvent)
                {
                    return predicate1(anEvent) && predicate2(anEvent);
                };
            }
            return new TraceEvents(log,
                Math.Max(startTime100ns, this.startTime100ns),
                Math.Min(endTime100ns, this.endTime100ns),
                predicate, this.backwards);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException(); // GetEnumerator
        }

        internal abstract class EventEnumeratorBase
        {
            protected EventEnumeratorBase(TraceEvents events)
            {
                this.events = events;
                reader = events.Log.AllocReader();
                lookup = events.Log.AllocLookup();
            }
            public TraceEvent Current { get { return current; } }
            public void Dispose()
            {
                events.Log.FreeReader(reader);
                events.Log.FreeLookup(lookup);
            }
            public void Reset()
            {
                throw new Exception("The method or operation is not implemented.");
            }
            protected unsafe TraceEvent GetNext()
            {
                TraceEventNativeMethods.EVENT_RECORD* ptr = (TraceEventNativeMethods.EVENT_RECORD*)reader.GetPointer(TraceLog.headerSize);
                TraceEvent ret = lookup.Lookup(ptr);

                // This first check is just a perf optimization so in the common case we don't to
                // the extra logic 
                if (ret.opcode == unchecked((TraceEventOpcode)(-1)))
                {
                    UnhandledTraceEvent unhandled = ret as UnhandledTraceEvent;
                    if (unhandled != null)
                        unhandled.PrepForCallback();
                }
                Debug.Assert(ret.source == events.log);

                // Confirm we have a half-way sane event, to catch obvious loss of sync.  
                Debug.Assert(ret.Level <= (TraceEventLevel)64);
                Debug.Assert(ret.Version <= 4);

                // We have to insure we have a pointer to the whole blob, not just the header.  
                int totalLength = TraceLog.headerSize + (ret.EventDataLength + 3 & ~3);
                Debug.Assert(totalLength < 0x10000);
                ret.eventRecord = (TraceEventNativeMethods.EVENT_RECORD*)reader.GetPointer(totalLength);
                ret.userData = TraceEventRawReaders.Add((IntPtr)ret.eventRecord, TraceLog.headerSize);
                reader.Skip(totalLength);

                ret.DebugValidate();
                return ret;
            }

            protected TraceEvent current;
            protected TraceEvents events;
            protected internal PinnedStreamReader reader;
            protected internal TraceEventDispatcher lookup;
            protected StreamLabel[] positions;
            protected int indexOnPage;
            protected int pageIndex;
        }

        internal sealed class ForwardEventEnumerator : EventEnumeratorBase, IEnumerator<TraceEvent>
        {
            public ForwardEventEnumerator(TraceEvents events)
                : base(events)
            {
                pageIndex = events.Log.FindPageIndex(events.startTime100ns);
                events.Log.SeekToTimeOnPage(reader, events.startTime100ns, pageIndex, out indexOnPage, positions);
                lookup.currentID = (EventIndex)(pageIndex * TraceLog.eventsPerPage + indexOnPage);
            }
            public bool MoveNext()
            {
                for (; ; )
                {
                    current = GetNext();
                    if (current.TimeStampQPC == long.MaxValue || current.TimeStamp100ns > events.endTime100ns)
                        return false;

                    // TODO confirm this works with nested predicates
                    if (events.predicate == null || events.predicate(current))
                        return true;
                }
            }
            public new object Current { get { return current; } }
        }

        internal sealed class BackwardEventEnumerator : EventEnumeratorBase, IEnumerator<TraceEvent>
        {
            public BackwardEventEnumerator(TraceEvents events)
                : base(events)
            {
                long endTime = events.endTime100ns;
                if (endTime != long.MaxValue)
                    endTime++;
                pageIndex = events.Log.FindPageIndex(endTime);
                positions = new StreamLabel[TraceLog.eventsPerPage];
                events.Log.SeekToTimeOnPage(reader, endTime, pageIndex, out indexOnPage, positions);
            }
            public bool MoveNext()
            {
                for (; ; )
                {
                    if (indexOnPage == 0)
                    {
                        if (pageIndex == 0)
                            return false;
                        --pageIndex;
                        events.Log.SeekToTimeOnPage(reader, long.MaxValue, pageIndex, out indexOnPage, positions);
                    }
                    else
                        --indexOnPage;
                    reader.Goto(positions[indexOnPage]);
                    lookup.currentID = (EventIndex)(pageIndex * TraceLog.eventsPerPage + indexOnPage);
                    current = GetNext();

                    if (current.TimeStamp100ns < events.startTime100ns)
                        return false;

                    // TODO confirm this works with nested predicates
                    if (events.predicate == null || events.predicate(current))
                        return true;
                }
            }
            public new object Current { get { return current; } }
        }

        private TraceEvent GetTemplateAtStreamLabel(StreamLabel label)
        {
            return null;
        }

        // #TraceEventVars
        // see code:#TraceLogVars
        internal TraceLog log;
        internal long startTime100ns;
        internal long endTime100ns;
        internal Predicate<TraceEvent> predicate;
        internal bool backwards;
        #endregion
    }

    /// <summary>
    /// We give each process a unique index from 0 to code:TraceProcesses.MaxProcessIndex. Thus it is unique
    /// within the whole code:TraceLog. You are explictly allowed take advantage of the fact that this number
    /// is in the range from 0 to code:TracesProcesses.MaxProcessIndex (you can create arrays indexed by
    /// code:ProcessIndex). We create the Enum because the strong typing avoids a class of user errors.
    /// </summary>
    public enum ProcessIndex { Invalid = -1 };

    /// <summary>
    /// A code:TraceProcesses represents the list of procsses in the Event log.  
    /// 
    /// TraceProcesses are IEnumerable, and will return the processes in order of time created.   
    /// </summary>
    public sealed class TraceProcesses : IEnumerable<TraceProcess>, IFastSerializable
    {
        /// <summary>
        /// Enumerate all the threads that occured in the trace log.  It does so in order of their process
        /// offset events in the log.  
        /// </summary> 
        IEnumerator<TraceProcess> IEnumerable<TraceProcess>.GetEnumerator()
        {
            for (int i = 0; i < processes.Count; i++)
                yield return processes[i];
        }
        /// <summary>
        /// The log associated with this collection of threads. 
        /// </summary>
        public TraceLog Log { get { return log; } }
        /// <summary>
        /// The count of the number of code:TraceProcess s in the trace log. 
        /// </summary>
        public int MaxProcessIndex { get { return processes.Count; } }
        /// <summary>
        /// Each process that occurs in the log is given a unique index (which unlike the PID is unique), that
        /// ranges from 0 to code:BatchCount - 1.   Return the code:TraceProcess for the given index.  
        /// </summary>
        public TraceProcess this[ProcessIndex processIndex]
        {
            get
            {
                if (processIndex == ProcessIndex.Invalid)
                    return null;
                return processes[(int)processIndex];
            }
        }
        /// <summary>
        /// Given a OS process ID and a time, return the last code:TraceProcess that has the same process ID,
        /// and whose offset start time is less than 'time100ns'. If 'time100ns' is during the threads lifetime this
        /// is guarenteed to be the correct process. Using time100ns = code:TraceLog.SessionEndTime100ns will return the
        /// last process with the given PID, even if it had died.
        /// </summary>
        public TraceProcess GetProcess(int processID, long time100ns)
        {
            int index;
            var ret = FindProcessAndIndex(processID, time100ns, out index);
            return ret;
        }
        /// <summary>
        /// Return the last process in the log with the given process ID.  Useful when the logging session
        /// was stopped just after the processes completed (a common scenario).  
        /// </summary>
        /// <param name="processID"></param>
        /// <returns></returns>
        public TraceProcess LastProcessWithID(int processID)
        {
            return GetProcess(processID, Log.SessionEndTime100ns);
        }

        /// <summary>
        /// Gets the first process (in time) that has the name 'processName' that started after 'afterTime'
        /// (inclusive). The name of a process is the file name (not full path), without its extension. Returns
        /// null on failure
        /// </summary>
        public TraceProcess FirstProcessWithName(string processName, long afterTime100ns = 0)
        {
            for (int i = 0; i < MaxProcessIndex; i++)
            {
                TraceProcess process = processes[i];
                if (afterTime100ns <= process.StartTime100ns &&
                    string.Compare(process.Name, processName, StringComparison.OrdinalIgnoreCase) == 0)
                    return process;
            }
            return null;
        }
        public TraceProcess LastProcessWithName(string processName, long afterTime100ns = 0)
        {
            TraceProcess ret = null;
            for (int i = 0; i < MaxProcessIndex; i++)
            {
                TraceProcess process = processes[i];
                if (afterTime100ns <= process.StartTime100ns &&
                    string.Compare(process.Name, processName, StringComparison.OrdinalIgnoreCase) == 0)
                    ret = process;
            }
            return ret;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<TraceProcesses Count=").Append(XmlUtilities.XmlQuote(MaxProcessIndex)).AppendLine(">");
            foreach (TraceProcess process in this)
                sb.Append("  ").Append(process.ToString()).AppendLine();
            sb.AppendLine("</TraceProcesses>");
            return sb.ToString();
        }
        #region Private
        /// <summary>
        /// TraceProcesses represents the entire ETL moduleFile log.   At the node level it is organized by threads.  
        /// 
        /// The TraceProcesses also is where we put various caches that are independent of the process involved. 
        /// These include a cache for code:TraceModuleFile that represent native images that can be loaded into a
        /// process, as well as the process lookup tables and a cache that remembers the last calls to
        /// GetNameForAddress(). 
        /// </summary>
        internal TraceProcesses(TraceLog log)
        {
            this.log = log;
            this.processes = new GrowableArray<TraceProcess>(64);
            this.processesByPID = new GrowableArray<TraceProcess>(64);
        }
        internal TraceProcess GetOrCreateProcess(int processID, long time100ns, bool isProcessStartEvent = false)
        {
            Debug.Assert(processes.Count == processesByPID.Count);
            int index;
            TraceProcess retProcess = FindProcessAndIndex(processID, time100ns, out index);
            if (retProcess == null || isProcessStartEvent)
            {
                // We can have events before process start, (sigh) so fix that.  
                if (retProcess != null && isProcessStartEvent)
                {
                    // If the process entry we found does not have a start or an end, then it is orphaned 
                    if (retProcess.StartTime100ns == 0 && retProcess.EndTime100ns == ETWTraceEventSource.MaxTime100ns)
                    {
                        // it should be within 10msec (or it is the Process DCStart and this firstEvent was the log header (which has time offset 0
                        log.DebugWarn(time100ns - retProcess.firstEvent100ns < 10 * 10000 || retProcess.firstEvent100ns == 0,
                            "Events occured > 10msec before process " + processID.ToString() +
                            " start at " + log.RelativeTimeMSec(retProcess.firstEvent100ns).ToString("f3") + " msec", null);
                        return retProcess;
                    }
                }
                retProcess = new TraceProcess(processID, log, (ProcessIndex)processes.Count);
                retProcess.firstEvent100ns = time100ns;
                processes.Add(retProcess);
                processesByPID.Insert(index + 1, retProcess);
            }
            return retProcess;
        }

        internal TraceProcess FindProcessAndIndex(int processID, long time100ns, out int index)
        {
            if (processesByPID.BinarySearch(processID, out index, compareByProcessID))
            {
                for (int candidateIndex = index; candidateIndex >= 0; --candidateIndex)
                {
                    TraceProcess candidate = processesByPID[candidateIndex];
                    if (candidate.ProcessID != processID)
                        break;

                    if (candidate.firstEvent100ns <= time100ns)
                    {
                        index = candidateIndex;
                        return candidate;
                    }
                }
            }
            return null;
        }

        // State variables.  
        private GrowableArray<TraceProcess> processes;          // The threads ordered in time. 
        private GrowableArray<TraceProcess> processesByPID;     // The threads ordered by processID.  
        private TraceLog log;

        static public GrowableArray<TraceProcess>.Comparison<int> compareByProcessID = delegate(int processID, TraceProcess process)
        {
            return (processID - process.ProcessID);
        };
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            for (int i = 0; i < processes.Count; i++)
                yield return processes[i];
        }

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(log);
            serializer.Log("<WriteColection name=\"Processes\" count=\"" + processes.Count + "\">\r\n");
            serializer.Write(processes.Count);
            for (int i = 0; i < processes.Count; i++)
                serializer.Write(processes[i]);
            serializer.Log("</WriteColection>\r\n");

            serializer.Log("<WriteColection name=\"ProcessesByPID\" count=\"" + processesByPID.Count + "\">\r\n");
            serializer.Write(processesByPID.Count);
            for (int i = 0; i < processesByPID.Count; i++)
                serializer.Write(processesByPID[i]);
            serializer.Log("</WriteColection>\r\n");
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            deserializer.Read(out log);

            Debug.Assert(processes.Count == 0);
            int count = deserializer.ReadInt();
            processes = new GrowableArray<TraceProcess>(count + 1);
            for (int i = 0; i < count; i++)
            {
                TraceProcess elem; deserializer.Read(out elem);
                processes.Add(elem);
            }

            count = deserializer.ReadInt();
            processesByPID = new GrowableArray<TraceProcess>(count + 1);
            for (int i = 0; i < count; i++)
            {
                TraceProcess elem; deserializer.Read(out elem);
                processesByPID.Add(elem);
            }
        }

        #endregion
    }

    /// <summary>
    /// A code:TraceProcess represents a process.  
    /// 
    /// </summary>
    public sealed class TraceProcess : IFastSerializable
    {
        /// <summary>
        /// The OS process ID associated with the process.   It is NOT unique across the whole log.  Use
        /// code:ProcessIndex for if you need that. 
        /// </summary>
        public int ProcessID { get { return processID; } }
        /// <summary>
        /// The index into the logical array of code:TraceProcesses for this process.  Unlike ProcessID (which
        /// may be reused after the process dies, the process index is unique in the log. 
        /// </summary>
        public ProcessIndex ProcessIndex { get { return processIndex; } }
        /// <summary>
        /// The log file associated with the process. 
        /// </summary>
        public TraceLog Log { get { return log; } }
        /// <summary>
        /// Enumerate all the threads that occured in this process.  
        /// </summary> 
        public IEnumerable<TraceThread> Threads
        {
            get
            {
                for (int i = 0; i < log.Threads.MaxThreadIndex; i++)
                {
                    TraceThread thread = log.Threads[(ThreadIndex)i];
                    if (thread.Process == this)
                        yield return thread;
                }
            }
        }

        public string CommandLine { get { return commandLine; } }
        public string ImageFileName { get { return imageFileName; } }

        /// <summary>
        /// This is a short name for the process.  It is the image file name without the path or suffix.  
        /// </summary>
        public string Name
        {
            get
            {
                if (name == null)
                    name = Path.GetFileNameWithoutExtension(ImageFileName);
                return name;
            }
        }
        public DateTime StartTime { get { return DateTime.FromFileTime(StartTime100ns); } }
        public double StartTimeRelativeMsec { get { return Log.RelativeTimeMSec(StartTime100ns); } }
        public long StartTime100ns { get { return startTime100ns; } }
        public DateTime EndTime { get { return DateTime.FromFileTime(EndTime100ns); } }
        public double EndTimeRelativeMsec { get { return Log.RelativeTimeMSec(EndTime100ns); } }
        public long EndTime100ns { get { return endTime100ns; } }
        public int ParentID { get { return parentID; } }
        public TraceProcess Parent { get { return parent; } }
        public int? ExitStatus { get { return exitStatus; } }
        public float CPUMSec { get { return cpuSamples * (Log.SampleProfileInterval100ns / 10000.0F); } }
        /// <summary>
        /// Is the process a 64 bit process
        /// </summary>
        public bool Is64Bit
        {
            get
            {
                // We are 64 bit if any module was loaded high or
                // (if we are on a 64 bit and there were no modules loaded, we assume we are the OS system process)
                return loadedAModuleHigh || (!anyModuleLoaded && log.PointerSize == 8);
            }
        }

        /// <summary>
        /// Filters events to only those for a particular process. 
        /// </summary>
        public TraceEvents EventsInProcess
        {
            get
            {
                return log.Events.Filter(StartTime100ns, EndTime100ns, delegate(TraceEvent anEvent)
                {
                    // FIX Virtual allocs
                    if (anEvent.ProcessID == processID)
                        return true;
                    // FIX Virtual alloc's Process ID? 
                    if (anEvent.ProcessID == -1)
                        return true;
                    return false;
                });
            }
        }
        /// <summary>
        /// Filters events to only that occured during the time a the process was alive. 
        /// </summary>
        /// 
        public TraceEvents EventsDuringProcess
        {
            get
            {
                return log.Events.FilterByTime(StartTime100ns, EndTime100ns);
            }
        }

        public TraceLoadedModules LoadedModules { get { return loadedModules; } }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<TraceProcess ");
            sb.Append("PID=").Append(XmlUtilities.XmlQuote(ProcessID)).Append(" ");
            sb.Append("ProcessIndex=").Append(XmlUtilities.XmlQuote(ProcessIndex)).Append(" ");
            // TODO null parent pointers should be impossible
            if (Parent != null)
                sb.Append("ParentPID=").Append(XmlUtilities.XmlQuote(Parent.ProcessID)).Append(" ");
            sb.Append("Exe=").Append(XmlUtilities.XmlQuote(Path.GetFileNameWithoutExtension(ImageFileName))).Append(" ");
            sb.Append("Start=").Append(XmlUtilities.XmlQuote(StartTimeRelativeMsec)).Append(" ");
            sb.Append("End=").Append(XmlUtilities.XmlQuote(EndTimeRelativeMsec)).Append(" ");
            if (ExitStatus.HasValue)
                sb.Append("ExitStatus=").Append(XmlUtilities.XmlQuote(ExitStatus.Value)).Append(" ");
            sb.Append("CPUMSec=").Append(XmlUtilities.XmlQuote(CPUMSec)).Append(" ");
            sb.Append("Is64Bit=").Append(XmlUtilities.XmlQuote(Is64Bit)).Append(" ");
            sb.Append("CommandLine=").Append(XmlUtilities.XmlQuote(CommandLine)).Append(" ");
            sb.Append("/>");
            return sb.ToString();
        }

        #region Private
        #region EventHandlersCalledFromTraceLog
        // #ProcessHandlersCalledFromTraceLog
        // 
        // called from code:TraceLog.CopyRawEvents
        internal void ProcessStart(ProcessTraceData data)
        {
            Log.DebugWarn(parentID == 0, "Events for process happen before process start.  PrevEventTime: " + Log.RelativeTimeMSec(StartTime100ns), data);

            if (data.Opcode == TraceEventOpcode.DataCollectionStart)
                this.startTime100ns = log.SessionStartTime100ns;
            else
            {
                Debug.Assert(data.Opcode == TraceEventOpcode.Start);
                Debug.Assert(EndTime100ns == ETWTraceEventSource.MaxTime100ns); // We would create a new Process record otherwise 
                this.startTime100ns = data.TimeStamp100ns;
            }
            this.commandLine = data.CommandLine;
            this.imageFileName = data.ImageFileName;
            this.parentID = data.ParentID;
            this.parent = log.Processes.GetProcess(data.ParentID, data.TimeStamp100ns);
        }
        internal void ProcessEnd(ProcessTraceData data)
        {
            if (this.commandLine.Length == 0)
                this.commandLine = data.CommandLine;
            this.imageFileName = data.ImageFileName;        // Always overwrite as we might have guessed via the image loads
            if (this.parentID == 0 && data.ParentID != 0)
            {
                this.parentID = data.ParentID;
                this.parent = log.Processes.GetProcess(data.ParentID, data.TimeStamp100ns);
            }

            if (data.Opcode != TraceEventOpcode.DataCollectionStop)
            {
                Debug.Assert(data.Opcode == TraceEventOpcode.Stop);
                // Only set the exit code if it really is a process exit (not a DCEnd). 
                if (data.Opcode == TraceEventOpcode.Stop)
                    this.exitStatus = data.ExitStatus;
                this.endTime100ns = data.TimeStamp100ns;
            }
            Log.DebugWarn(StartTime100ns <= EndTime100ns, "Process Ends before it starts! StartTime: " + Log.RelativeTimeMSec(StartTime100ns), data);
        }
        #endregion

        /// <summary>
        /// Create a new code:TraceProcess.  It should only be done by code:log.CreateTraceProcess because
        /// only code:TraceLog is responsible for generating a new ProcessIndex which we need.   'processIndex'
        /// is a index that is unique for the whole log file (where as processID can be reused).  
        /// </summary>
        internal TraceProcess(int processID, TraceLog log, ProcessIndex processIndex)
        {
            this.log = log;
            this.processID = processID;
            this.processIndex = processIndex;
            this.endTime100ns = ETWTraceEventSource.MaxTime100ns;
            this.commandLine = "";
            this.imageFileName = "";
            this.loadedModules = new TraceLoadedModules(this);
        }

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(processID);
            serializer.Write((int)processIndex);
            serializer.Write(log);
            serializer.Write(commandLine);
            serializer.Write(imageFileName);
            serializer.Write(firstEvent100ns);
            serializer.Write(startTime100ns);
            serializer.Write(endTime100ns);
            serializer.Write(exitStatus);
            serializer.Write(parentID);
            serializer.Write(parent);
            serializer.Write(loadedModules);
            serializer.Write(cpuSamples);
            serializer.Write(loadedAModuleHigh);
            serializer.Write(anyModuleLoaded);
        }

        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            deserializer.Read(out processID);
            int processIndex; deserializer.Read(out processIndex); this.processIndex = (ProcessIndex)processIndex;
            deserializer.Read(out log);
            deserializer.Read(out commandLine);
            deserializer.Read(out imageFileName);
            deserializer.Read(out firstEvent100ns);
            deserializer.Read(out startTime100ns);
            deserializer.Read(out endTime100ns);
            deserializer.Read(out exitStatus);
            deserializer.Read(out parentID);
            deserializer.Read(out parent);
            deserializer.Read(out loadedModules);
            deserializer.Read(out cpuSamples);
            deserializer.Read(out loadedAModuleHigh);
            deserializer.Read(out anyModuleLoaded);
        }

        private int processID;
        internal ProcessIndex processIndex;
        private TraceLog log;

        private string commandLine;
        internal string imageFileName;
        private string name;
        internal long firstEvent100ns;      // Sadly there are events before process start.   This is minimum of those times.  
        internal long startTime100ns;
        internal long endTime100ns;
        private int? exitStatus;
        private int parentID;
        private TraceProcess parent;

        internal int cpuSamples;
        internal bool loadedAModuleHigh;    // Was any module loaded above 0x100000000?  (which indicates it is a 64 bit process)
        internal bool anyModuleLoaded;
        internal bool anyThreads;

        internal bool isServerGC;
        internal byte numMarkTheadsInGC;   // Used during collection to determine if we are server GC or not. 

        private TraceLoadedModules loadedModules;
        #endregion
    }

    /// <summary>
    /// We give each thread  a unique index from 0 to code:TraceThreads.MaxThreadIndex. Thus it is unique
    /// within the whole code:TraceLog. You are explictly allowed take advantage of the fact that this
    /// number is in the range from 0 to code:TracesThreads.MaxThreadIndex (you can create arrays indexed by
    /// code:ThreadIndex). We create the Enum because the strong typing avoids a class of user errors.
    /// </summary>
    public enum ThreadIndex { Invalid = -1 };

    /// <summary>
    /// A code:TraceThreads represents the list of threads in a process. 
    /// </summary>
    public sealed class TraceThreads : IEnumerable<TraceThread>, IFastSerializable
    {
        /// <summary>
        /// Enumerate all the threads that occured in the trace log.  It does so in order of their thread
        /// offset events in the log.  
        /// </summary> 
        IEnumerator<TraceThread> IEnumerable<TraceThread>.GetEnumerator()
        {
            for (int i = 0; i < threads.Count; i++)
                yield return threads[i];
        }
        /// <summary>
        /// The count of the number of code:TraceThread s in the trace log. 
        /// </summary>
        public int MaxThreadIndex { get { return threads.Count; } }
        /// <summary>
        /// Each thread that occurs in the log is given a unique index (which unlike the PID is unique), that
        /// ranges from 0 to code:BatchCount - 1.   Return the code:TraceThread for the given index.  
        /// </summary>
        public TraceThread this[ThreadIndex threadIndex]
        {
            get
            {
                if (threadIndex == ThreadIndex.Invalid)
                    return null;
                return threads[(int)threadIndex];
            }
        }
        /// <summary>
        /// Given a OS thread ID and a time, return the last code:TraceThread that has the same thread index,
        /// and whose offset time is less than 'time100ns'. If 'time100ns' is during the threads lifetime this
        /// is guarenteed to be the correct thread. 
        /// </summary>
        public TraceThread GetThread(int threadID, long time100ns)
        {
            InitThread();
            TraceThread ret;
            threadIDtoThread.TryGetValue((Address)threadID, time100ns, out ret);
            return ret;
        }
        /// <summary>
        /// Get the thread for threadID and time100ns.   Create if necessary.  If 'isThreadCreateEvent' is true, 
        /// then force  the creation of a new thread EVEN if the thread exist since we KNOW it is a new thread 
        /// (and somehow we missed the threadEnd event).   Process is the process associated with the thread.  
        /// It can be null if you really don't know the process ID.  We will try to fill it in on another event
        /// where we DO know the process id (ThreadEnd event).     
        /// </summary>
        internal TraceThread GetOrCreateThread(int threadID, long time100ns, TraceProcess process, bool isThreadCreateEvent = false)
        {
            TraceThread retThread = GetThread(threadID, time100ns);
            if (retThread == null || isThreadCreateEvent)
            {
                InitThread();

                if (process == null)
                    process = log.Processes.GetOrCreateProcess(-1, time100ns);      // Unknown process

                retThread = new TraceThread(threadID, process, (ThreadIndex)threads.Count);
                if (isThreadCreateEvent)
                    retThread.startTime100ns = time100ns;
                threads.Add(retThread);
                threadIDtoThread.Add((Address)threadID, time100ns, retThread);
            }

            // Set the process if we had to set this threads process ID to the 'unknown' process.  
            if (process != null && retThread.process.ProcessID == -1)
                retThread.process = process;

            return retThread;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<TraceThreads Count=").Append(XmlUtilities.XmlQuote(MaxThreadIndex)).AppendLine(">");
            foreach (TraceThread thread in this)
                sb.Append("  ").Append(thread.ToString()).AppendLine();
            sb.AppendLine("</TraceThreads>");
            return sb.ToString();
        }
        #region Private
        /// <summary>
        /// TraceThreads   represents the collection of threads in a process. 
        /// 
        /// </summary>
        internal TraceThreads(TraceLog log)
        {
            this.log = log;
        }
        private void InitThread()
        {
            // Create a cache for this because it can be common
            if (threadIDtoThread == null)
            {
                threadIDtoThread = new HistoryDictionary<TraceThread>(1000);
                for (int i = 0; i < threads.Count; i++)
                {
                    var thread = threads[i];
                    threadIDtoThread.Add((Address)thread.ThreadID, thread.startTime100ns, thread);
                }
            }
        }

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(log);

            serializer.Log("<WriteColection name=\"threads\" count=\"" + threads.Count + "\">\r\n");
            serializer.Write(threads.Count);
            for (int i = 0; i < threads.Count; i++)
                serializer.Write(threads[i]);
            serializer.Log("</WriteColection>\r\n");
        }

        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            deserializer.Read(out log);
            Debug.Assert(threads.Count == 0);
            int count = deserializer.ReadInt();
            threads = new GrowableArray<TraceThread>(count + 1);

            for (int i = 0; i < count; i++)
            {
                TraceThread elem; deserializer.Read(out elem);
                threads.Add(elem);
            }
        }
        // State variables.  
        private GrowableArray<TraceThread> threads;          // The threads ordered in time. 
        private TraceLog log;
        private HistoryDictionary<TraceThread> threadIDtoThread;

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException(); // GetEnumerator
        }
        #endregion
    }

    /// <summary>
    /// A code:TraceThread represents a tread of execution in a process.  
    /// </summary>
    public sealed class TraceThread : IFastSerializable
    {
        /// <summary>
        /// The OS process ID associated with the process. 
        /// </summary>
        public int ThreadID { get { return threadID; } }
        /// <summary>
        /// The index into the logical array of code:TraceThreads for this process.  Unlike ThreadId (which
        /// may be reused after the trhead dies, the T index is unique over the log.  
        /// </summary>
        public ThreadIndex ThreadIndex { get { return threadIndex; } }
        /// <summary>
        /// The process associated with the thread. 
        /// </summary>
        public TraceProcess Process { get { return process; } }
        public DateTime StartTime { get { return DateTime.FromFileTime(StartTime100ns); } }
        public double StartTimeRelative { get { return process.Log.RelativeTimeMSec(StartTime100ns); } }
        public long StartTime100ns { get { return startTime100ns; } }
        public DateTime EndTime { get { return DateTime.FromFileTime(EndTime100ns); } }
        public double EndTimeRelative { get { return process.Log.RelativeTimeMSec(EndTime100ns); } }
        public long EndTime100ns { get { return endTime100ns; } }
        public float CPUMSec { get { return cpuSamples * Process.Log.SampleProfileInterval100ns / 10000.0F; } }
        /// <summary>
        /// Filters events to only those for a particular thread. 
        /// </summary>
        public TraceEvents EventsInThread
        {
            get
            {
                return Process.Log.Events.Filter(StartTime100ns, EndTime100ns, delegate(TraceEvent anEvent)
                {
                    return anEvent.ThreadID == ThreadID;
                });
            }
        }
        /// <summary>
        /// Filters events to only that occured during the time a the thread was alive. 
        /// </summary>
        /// 
        public TraceEvents EventsDuringThread
        {
            get
            {
                return Process.Log.Events.FilterByTime(StartTime100ns, EndTime100ns);
            }
        }
        public override string ToString()
        {
            return "<TraceThread " +
                    "TID=" + XmlUtilities.XmlQuote(ThreadID).PadRight(5) + " " +
                    "ThreadIndex=" + XmlUtilities.XmlQuote(threadIndex).PadRight(5) + " " +
                    "StartTimeRelative=" + XmlUtilities.XmlQuote(StartTimeRelative).PadRight(8) + " " +
                    "EndTimeRelative=" + XmlUtilities.XmlQuote(EndTimeRelative).PadRight(8) + " " +
                   "/>";
        }

        /// <summary>
        /// ThreadInfo is a string that tries to identify the thread symbolically.   e.g. .NET THreadpool, .NET GC 
        /// </summary>
        public string ThreadInfo { get { return threadInfo; } }

        public string VerboseThreadName
        {
            get
            {
                if (verboseThreadName == null)
                {
                    verboseThreadName = string.Format("Thread ({0}) CPU={1:f0}ms", ThreadID, CPUMSec);
                    if (ThreadInfo != null)
                        verboseThreadName += " (" + ThreadInfo + ")";
                }
                return verboseThreadName;
            }
        }

        #region Private
        /// <summary>
        /// Create a new code:TraceProcess.  It should only be done by code:log.CreateTraceProcess because
        /// only code:TraceLog is responsible for generating a new ProcessIndex which we need.   'processIndex'
        /// is a index that is unique for the whole log file (where as processID can be reused).  
        /// </summary>
        internal TraceThread(int threadID, TraceProcess process, ThreadIndex threadIndex)
        {
            this.threadID = threadID;
            this.threadIndex = threadIndex;
            this.process = process;
            this.endTime100ns = ETWTraceEventSource.MaxTime100ns;
        }

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(threadID);
            serializer.Write((int)threadIndex);
            serializer.Write(process);
            serializer.Write(startTime100ns);
            serializer.Write(endTime100ns);
            serializer.Write(cpuSamples);
            serializer.Write(threadInfo);
        }

        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            deserializer.Read(out threadID);
            int threadIndex; deserializer.Read(out threadIndex); this.threadIndex = (ThreadIndex)threadIndex;
            deserializer.Read(out process);
            deserializer.Read(out startTime100ns);
            deserializer.Read(out endTime100ns);
            deserializer.Read(out cpuSamples);
            deserializer.Read(out threadInfo);
        }

        private int threadID;
        private ThreadIndex threadIndex;
        internal TraceProcess process;
        internal long startTime100ns;
        internal long endTime100ns;
        internal int cpuSamples;
        internal string threadInfo;
        internal bool threadDidGCMark;
        private string verboseThreadName;
        #endregion
    }

    /// <summary>
    /// code:TraceLoadedModules represents the collection of static modules (loaded DLLs or EXEs that
    /// directly runnable) in a particular process.  
    /// </summary>
    public sealed class TraceLoadedModules : IEnumerable<TraceLoadedModule>, IFastSerializable
    {
        // TODO do we want a LoadedModuleIndex?
        public TraceProcess Process { get { return process; } }
        // Returns all modules in the process.  Note that managed modules may appear twice 
        // (once for the managed load and once for an unmanaged (LoadLibrary) load.  
        public IEnumerator<TraceLoadedModule> GetEnumerator()
        {
            for (int i = 0; i < modules.Count; i++)
                yield return modules[i];
        }
        /// <summary>
        /// Returns the managedModule with the given moduleID.  For native images the managedModule ID is the image base.  For
        /// managed images the managedModule returned is always the IL managedModule. 
        /// TODO should managedModuleID be given an opaque type?
        /// </summary>
        public TraceManagedModule GetManagedModule(long managedModuleID, long time100ns)
        {
            int index;
            TraceManagedModule managedModule = FindManagedModuleAndIndex(managedModuleID, time100ns, out index);
            return managedModule;
        }
        /// <summary>
        /// This function will find the module assocated with 'address' at 'time100ns' however it will only
        /// find modules that are mapped in memory (module assocated with JIT compiled methods will not be found).  
        /// </summary>
        public TraceLoadedModule GetModuleContainingAddress(Address address, long time100ns)
        {
            int index;
            TraceLoadedModule module = FindModuleAndIndexContainingAddress(address, time100ns, out index);
            return module;
        }
        /// <summary>
        /// Returns the module representing the unmanaged load of a file. 
        /// </summary>
        public TraceLoadedModule GetLoadedModule(string fileName, long time100ns)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                TraceLoadedModule module = modules[i];
                if (module.FileName == fileName && module.loadTime100ns <= time100ns && time100ns < module.unloadTime100ns)
                    return module;
            }
            return null;
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<TraceLoadedModules Count=").Append(XmlUtilities.XmlQuote(modules.Count)).AppendLine(">");
            foreach (TraceLoadedModule module in this)
                sb.Append("  ").Append(module.ToString()).AppendLine();
            sb.AppendLine("</TraceLoadedModules>");
            return sb.ToString();
        }

        #region Private
        // #ModuleHandlersCalledFromTraceLog
        internal TraceModuleFile ImageLoadOrUnload(ImageLoadTraceData data, bool isLoad)
        {
            int index;
            string dataFileName = data.FileName;
            TraceLoadedModule module = FindModuleAndIndexContainingAddress(data.ImageBase, data.TimeStamp100ns, out index);
            if (module == null)
            {
                // We need to make a new module 
                TraceModuleFile newModuleFile = process.Log.ModuleFiles.GetOrCreateModuleFile(dataFileName, data.ImageBase);
                newModuleFile.imageSize = data.ImageSize;
                module = new TraceLoadedModule(process, newModuleFile, data.ImageBase);
                InsertAndSetOverlap(index + 1, module);
            }

            // If we load a module higher than 32 bits can do, then we must be a 64 bit process.  
            if (!process.loadedAModuleHigh && (ulong)data.ImageBase >= 0x100000000L)
            {
                //  On win8 ntdll gets loaded into 32 bit processes so ignore it
                if (!dataFileName.EndsWith("ntdll.dll", StringComparison.OrdinalIgnoreCase))
                    process.loadedAModuleHigh = true;
            }
            process.anyModuleLoaded = true;

            TraceModuleFile moduleFile = module.ModuleFile;
            Debug.Assert(moduleFile != null);

            // WORK-AROUND.   I have had problem on 64 bit systems with image load (but not the unload being only a prefix of
            // the full file path.   We 'fix it' here.   
            if (!isLoad && module.ModuleFile.FilePath.Length < dataFileName.Length)
            {
                process.Log.DebugWarn(false, "Needed to fix up a truncated load file path at unload time.", data);
                module.ModuleFile.fileName = dataFileName;
            }

            // TODO we get different prefixes. skip it 
            int len = Math.Max(Math.Min(module.ModuleFile.FilePath.Length - 4, dataFileName.Length - 4), 0);
            int start1 = module.ModuleFile.FilePath.Length - len;
            int start2 = dataFileName.Length - len;
            process.Log.DebugWarn(string.Compare(module.ModuleFile.FilePath, start1, dataFileName, start2, len, StringComparison.OrdinalIgnoreCase) == 0,
                "Filename Load/Unload mismatch.\r\n    FILE1: " + module.ModuleFile.FilePath, data);
            process.Log.DebugWarn(module.ModuleFile.ImageSize == 0 || module.ModuleFile.ImageSize == data.ImageSize,
                "ImageSize not consistant over all Loads Size 0x" + module.ModuleFile.ImageSize.ToString("x"), data);
            /* TODO this one fails.  decide what to do about it. 
            process.Log.DebugWarn(module.ModuleFile.DefaultBase == 0 || module.ModuleFile.DefaultBase == data.DefaultBase,
                "DefaultBase not consistant over all Loads Size 0x" + module.ModuleFile.DefaultBase.ToString("x"), data);
             ***/

            moduleFile.imageSize = data.ImageSize;
            moduleFile.defaultBase = data.DefaultBase;
            if (isLoad)
            {
                process.Log.DebugWarn(module.loadTime100ns == 0 || data.Opcode == TraceEventOpcode.DataCollectionStart, "Events for module happened before load.  PrevEventTime: " + process.Log.RelativeTimeMSec(module.loadTime100ns), data);
                process.Log.DebugWarn(data.TimeStamp100ns < module.unloadTime100ns, "Unload time < load time!", data);

                module.loadTime100ns = data.TimeStamp100ns;
                if (data.Opcode == TraceEventOpcode.DataCollectionStart)
                    module.loadTime100ns = process.Log.SessionStartTime100ns;
            }
            else
            {
                process.Log.DebugWarn(module.loadTime100ns < data.TimeStamp100ns, "Unload time < load time!", data);
                process.Log.DebugWarn(module.unloadTime100ns == ETWTraceEventSource.MaxTime100ns,
                    "Unloading a image twice PrevUnloadTime: " + process.Log.RelativeTimeMSec(module.unloadTime100ns), data);
                if (data.Opcode == TraceEventOpcode.DataCollectionStop)
                {
                    // For circular logs, we don't have the process name but we can infer it from the module DCEnd events
                    if (Process.imageFileName.Length == 0 && dataFileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        Process.imageFileName = dataFileName;
                }
                else
                {
                    module.unloadTime100ns = data.TimeStamp100ns;
                    // TODO there seem to be unmatched unloads in many traces.  This has make this diagnostic too noisy.
                    // ideally we could turn this back on. 
                    // process.Log.DebugWarn(module.loadTime100ns != 0, "Unloading image not loaded.", data);
                }

                // Look for all code addresses those that don't have modules that are in my range are assumed to be mine.  
                Process.Log.CodeAddresses.AddNativeModule(data, module.ModuleFile.ModuleFileIndex);
            }
            CheckClassInvarients();
            return moduleFile;
        }
        internal void ManagedModuleLoadOrUnload(ModuleLoadUnloadTraceData data, bool isLoad)
        {
            int index;
            TraceManagedModule module = FindManagedModuleAndIndex(data.ModuleID, data.TimeStamp100ns, out index);
            if (module == null)
            {
                // We need to make a new module 
                TraceModuleFile newModuleFile = process.Log.ModuleFiles.GetOrCreateModuleFile(data.ModuleILPath, 0);
                module = new TraceManagedModule(process, newModuleFile, data.ModuleID);
                modules.Insert(index + 1, module);      // put it where it belongs in the sorted list
            }

            process.Log.DebugWarn(module.assemblyID == 0 || module.assemblyID == data.AssemblyID, "Inconsistant Assembly ID previous ID = 0x" + module.assemblyID.ToString("x"), data);
            module.assemblyID = data.AssemblyID;
            module.flags = data.ModuleFlags;
            if (data.ModuleNativePath.Length > 0)
                module.nativeModule = GetLoadedModule(data.ModuleNativePath, data.TimeStamp100ns);
            if (module.ModuleFile.fileName == null)
                process.Log.ModuleFiles.SetModuleFileName(module.ModuleFile, data.ModuleILPath);
            if (data.ManagedPdbSignature != Guid.Empty && module.ModuleFile.pdbSignature == Guid.Empty)
            {
                module.ModuleFile.pdbSignature = data.ManagedPdbSignature;
                module.ModuleFile.pdbAge = data.ManagedPdbAge;
                module.ModuleFile.pdbName = data.ManagedPdbBuildPath;
            }
            if (module.NativeModule != null)
            {
                Debug.Assert(module.NativeModule.managedModule == null ||
                    module.NativeModule.ModuleFile.managedModule.FilePath == module.ModuleFile.FilePath);

                module.NativeModule.ModuleFile.managedModule = module.ModuleFile;
                if (data.NativePdbSignature != Guid.Empty && module.NativeModule.ModuleFile.pdbSignature == Guid.Empty)
                {
                    module.NativeModule.ModuleFile.pdbSignature = data.NativePdbSignature;
                    module.NativeModule.ModuleFile.pdbAge = data.NativePdbAge;
                    module.NativeModule.ModuleFile.pdbName = data.NativePdbBuildPath;
                }
            }

            // TODO factor this with the unmanaged case.  
            if (isLoad)
            {
                process.Log.DebugWarn(module.loadTime100ns == 0 || data.Opcode == TraceEventOpcode.DataCollectionStart, "Events for module happened before load.  PrevEventTime: " + process.Log.RelativeTimeMSec(module.loadTime100ns), data);
                process.Log.DebugWarn(data.TimeStamp100ns < module.unloadTime100ns, "Managed Unload time < load time!", data);

                module.loadTime100ns = data.TimeStamp100ns;
                if (data.Opcode == TraceEventOpcode.DataCollectionStart)
                    module.loadTime100ns = process.Log.SessionStartTime100ns;
            }
            else
            {
                process.Log.DebugWarn(module.loadTime100ns < data.TimeStamp100ns, "Managed Unload time < load time!", data);
                process.Log.DebugWarn(module.unloadTime100ns == ETWTraceEventSource.MaxTime100ns, "Unloading a managed image twice PrevUnloadTime: " + process.Log.RelativeTimeMSec(module.unloadTime100ns), data);
                if (data.Opcode != TraceEventOpcode.DataCollectionStop)
                    module.unloadTime100ns = data.TimeStamp100ns;
            }
            CheckClassInvarients();
        }

        public TraceManagedModule GetOrCreateManagedModule(long managedModuleID, long time100ns)
        {
            int index;
            TraceManagedModule module = FindManagedModuleAndIndex(managedModuleID, time100ns, out index);
            if (module == null)
            {
                // We need to make a new module entry (which is pretty empty)
                TraceModuleFile newModuleFile = process.Log.ModuleFiles.GetOrCreateModuleFile(null, 0);
                module = new TraceManagedModule(process, newModuleFile, managedModuleID);
                modules.Insert(index + 1, module);      // put it where it belongs in the sorted list
            }
            return module;
        }
        /// <summary>
        /// Finds the index and module for an a given managed module ID.  If not found, new module
        /// should be inserated at index + 1;
        /// </summary>
        private TraceManagedModule FindManagedModuleAndIndex(long moduleID, long time100ns, out int index)
        {
            modules.BinarySearch((ulong)moduleID, out index, compareByKey);
            // Index now points at the last place where module.key <= moduleId;  
            // Search backwards from where for a module that is loaded and in range.  
            while (index >= 0)
            {
                TraceLoadedModule candidateModule = modules[index];
                if (candidateModule.key < (ulong)moduleID)
                    break;
                Debug.Assert(candidateModule.key == (ulong)moduleID);

                // We keep managed modules after unmanaged modules 
                TraceManagedModule managedModule = candidateModule as TraceManagedModule;
                if (managedModule == null)
                    break;

                // we also sort all modules with the same module ID by unload time
                if (!(time100ns < candidateModule.UnloadTime100ns))
                    break;

                // Is it in range? 
                if (candidateModule.LoadTime100ns <= time100ns)
                    return managedModule;
                --index;
            }
            return null;
        }
        /// <summary>
        /// Finds the index and module for an address that lives within the image.  If the module
        /// did not match the new entry should go at index+1.   
        /// </summary>
        private TraceLoadedModule FindModuleAndIndexContainingAddress(Address address, long time100ns, out int index)
        {
            modules.BinarySearch((ulong)address, out index, compareByKey);
            // Index now points at the last place where module.ImageBase <= address;  
            // Search backwards from where for a module that is loaded and in range.  
            int candidateIndex = index;
            while (candidateIndex >= 0)
            {
                TraceLoadedModule canidateModule = modules[candidateIndex];
                ulong candidateImageEnd = (ulong)canidateModule.ImageBase + (uint)canidateModule.ModuleFile.ImageSize;
                if ((ulong)address < candidateImageEnd)
                {
                    // Have we found a match? 
                    if ((ulong)canidateModule.ImageBase <= (ulong)address)
                    {
                        if (canidateModule.LoadTime100ns <= time100ns && time100ns <= canidateModule.UnloadTime100ns)
                        {
                            index = candidateIndex;
                            return canidateModule;
                        }
                    }
                }
                else if (!canidateModule.overlaps)
                    break;
                --candidateIndex;
            }
            // We return the index associated with the binary search. 
            return null;
        }
        private void InsertAndSetOverlap(int moduleIndex, TraceLoadedModule module)
        {
            modules.Insert(moduleIndex, module);      // put it where it belongs in the sorted list

            // Does it overlap with the previous entry
            if (moduleIndex > 0)
            {
                var prevModule = modules[moduleIndex - 1];
                ulong prevImageEnd = (ulong)prevModule.ImageBase + (uint)prevModule.ModuleFile.ImageSize;
                if (prevImageEnd > (ulong)module.ImageBase)
                {
                    prevModule.overlaps = true;
                    module.overlaps = true;
                }
            }
            // does it overlap with the next entry 
            if (moduleIndex + 1 < modules.Count)
            {
                var nextModule = modules[moduleIndex + 1];
                ulong moduleImageEnd = (ulong)module.ImageBase + (uint)module.ModuleFile.ImageSize;
                if (moduleImageEnd > (ulong)nextModule.ImageBase)
                {
                    nextModule.overlaps = true;
                    module.overlaps = true;
                }
            }

            // I should not have to look at entries further away 
        }
        static internal GrowableArray<TraceLoadedModule>.Comparison<ulong> compareByKey = delegate(ulong x, TraceLoadedModule y)
        {
            if (x > y.key)
                return 1;
            if (x < y.key)
                return -1;
            return 0;
        };

        [Conditional("DEBUG")]
        private void CheckClassInvarients()
        {
            // Modules better be sorted
            ulong lastkey = 0;
            TraceLoadedModule lastModule = null;
            for (int i = 0; i < modules.Count; i++)
            {
                TraceLoadedModule module = modules[i];
                Debug.Assert(module.key != 0);
                Debug.Assert(module.key >= lastkey, "regions not sorted!");

                TraceManagedModule asManaged = module as TraceManagedModule;
                if (asManaged != null)
                {
                    Debug.Assert((ulong)asManaged.ModuleID == module.key);
                }
                else
                {
                    Debug.Assert((ulong)module.ImageBase == module.key);
                }
                lastkey = module.key;
                lastModule = module;
            }
        }

        internal TraceLoadedModules(TraceProcess process)
        {
            this.process = process;
        }
        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(process);
            serializer.Log("<WriteColection count=\"" + modules.Count + "\">\r\n");
            serializer.Write(modules.Count);
            for (int i = 0; i < modules.Count; i++)
                serializer.Write(modules[i]);
            serializer.Log("</WriteColection>\r\n");
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            deserializer.Read(out process);
            Debug.Assert(modules.Count == 0);
            int count; deserializer.Read(out count);
            for (int i = 0; i < count; i++)
            {
                TraceLoadedModule elem; deserializer.Read(out elem);
                modules.Add(elem);
            }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException(); // GetEnumerator
        }

        TraceProcess process;
        GrowableArray<TraceLoadedModule> modules;               // Contains unmanaged modules sorted by key
        #endregion
    }

    /// <summary>
    /// A code:TraceLoadedModule represents a collection of code that is ready to run (it is loaded into a
    /// process. 
    /// </summary>
    public class TraceLoadedModule : IFastSerializable
    {
        // TODO do we want loadedModuleIndex?
        /// <summary>
        /// 0 for managed modules without NGEN images.  
        /// </summary>
        public Address ImageBase { get { if (moduleFile == null) return 0; else return moduleFile.ImageBase; } }
        /// <summary>
        /// The load time is the time the LoadLibrary was done if it was loaded from a file, otherwise is the
        /// time the CLR loaded the module. 
        /// </summary>
        public DateTime LoadTime { get { return DateTime.FromFileTime(LoadTime100ns); } }
        public double LoadTimeRelative { get { return Process.Log.RelativeTimeMSec(LoadTime100ns); } }
        public long LoadTime100ns { get { return loadTime100ns; } }
        public DateTime UnloadTime { get { return DateTime.FromFileTime(UnloadTime100ns); } }
        public double UnloadTimeRelative { get { return Process.Log.RelativeTimeMSec(UnloadTime100ns); } }
        public long UnloadTime100ns { get { return unloadTime100ns; } }
        public TraceProcess Process { get { return process; } }
        virtual public long ModuleID { get { return (long)ImageBase; } }

        /// <summary>
        /// If this managedModule was a file that was mapped into memory (eg LoadLibary), then ModuleFile points at
        /// it.  If a managed module does not have a file associated with it, this can be null.  
        /// </summary>
        public TraceModuleFile ModuleFile { get { return moduleFile; } }
        public string FileName { get { if (ModuleFile == null) return ""; else return ModuleFile.FilePath; } }
        public string Name { get { if (ModuleFile == null) return ""; else return ModuleFile.Name; } }
        public override string ToString()
        {
            string moduleFileRef = "";
            return "<TraceLoadedModule " +
                    "Name=" + XmlUtilities.XmlQuote(Name).PadRight(24) + " " +
                    moduleFileRef +
                    "ImageBase=" + XmlUtilities.XmlQuoteHex((ulong)ImageBase) + " " +
                    "ImageSize=" + XmlUtilities.XmlQuoteHex((ModuleFile != null) ? ModuleFile.ImageSize : 0) + " " +
                    "LoadTimeRelative=" + XmlUtilities.XmlQuote(LoadTimeRelative) + " " +
                    "UnloadTimeRelative=" + XmlUtilities.XmlQuote(UnloadTimeRelative) + " " +
                    "FileName=" + XmlUtilities.XmlQuote(FileName) + " " +
                   "/>";
        }
        /// <summary>
        /// If this module is an NGEN (or IL) image, return the first instance that this module was loaded as a
        /// managed module (note that there may be more than one (if the code is Appdomain specific and loaded
        /// in several appdomains).  
        /// 
        /// TODO: provide a way of getting at all the loaded images.  
        /// </summary>
        public TraceManagedModule ManagedModule { get { return managedModule; } }
        #region Private
        internal TraceLoadedModule(TraceProcess process, TraceModuleFile moduleFile, Address imageBase)
        {
            this.process = process;
            this.moduleFile = moduleFile;
            this.unloadTime100ns = ETWTraceEventSource.MaxTime100ns;
            this.key = (ulong)imageBase;
        }
        protected TraceLoadedModule(TraceProcess process, TraceModuleFile moduleFile, long moduleID)
        {
            this.process = process;
            this.moduleFile = moduleFile;
            this.unloadTime100ns = ETWTraceEventSource.MaxTime100ns;
            this.key = (ulong)moduleID;
        }

        public void ToStream(Serializer serializer)
        {
            serializer.Write(loadTime100ns);
            serializer.Write(unloadTime100ns);
            serializer.Write(managedModule);
            serializer.Write(process);
            serializer.Write(moduleFile);
            serializer.Write((long)key);
            serializer.Write(overlaps);
        }
        public void FromStream(Deserializer deserializer)
        {
            long address;

            deserializer.Read(out loadTime100ns);
            deserializer.Read(out unloadTime100ns);
            deserializer.Read(out managedModule);
            deserializer.Read(out process);
            deserializer.Read(out moduleFile);
            deserializer.Read(out address); key = (ulong)address;
            deserializer.Read(out overlaps);
        }

        internal ulong key;                          // Either the base address (for unmanaged) or moduleID (managed) 
        internal bool overlaps;                      // address range overlaps with other modules in the list.  
        internal long loadTime100ns;
        internal long unloadTime100ns;
        internal TraceManagedModule managedModule;
        private TraceProcess process;
        private TraceModuleFile moduleFile;         // Can be null (modules with files)

        internal int stackVisitedID;                // Used to determine if we have already visited this node or not.   
        #endregion
    }

    /// <summary>
    /// A code:TraceManagedModule is a .NET runtime loaded managedModule.  
    /// TODO explain more
    /// </summary>
    public sealed class TraceManagedModule : TraceLoadedModule, IFastSerializable
    {
        override public long ModuleID { get { return (long)key; } }
        public long AssmeblyID { get { return assemblyID; } }
        public bool IsAppDomainNeutral { get { return (flags & ModuleFlags.DomainNeutral) != 0; } }
        /// <summary>
        /// If the managed managedModule is an IL managedModule that has has an NGEN image, return it. 
        /// </summary>
        public TraceLoadedModule NativeModule { get { return nativeModule; } }
        public override string ToString()
        {
            string nativeInfo = "";
            if (NativeModule != null)
                nativeInfo = "<NativeModule>\r\n  " + NativeModule.ToString() + "\r\n</NativeModule>\r\n";

            return "<TraceManagedModule " +
                   "ModuleID=" + XmlUtilities.XmlQuoteHex((ulong)ModuleID) + " " +
                   "AssmeblyID=" + XmlUtilities.XmlQuoteHex((ulong)AssmeblyID) + ">\r\n" +
                   "  " + base.ToString() + "\r\n" +
                   nativeInfo +
                   "</TraceManagedModule>";
        }
        #region Private
        internal TraceManagedModule(TraceProcess process, TraceModuleFile moduleFile, long moduleID)
            : base(process, moduleFile, moduleID) { }

        // TODO use or remove
        internal TraceLoadedModule nativeModule;        // non-null for IL managed modules
        internal long assemblyID;
        internal ModuleFlags flags;

        void IFastSerializable.ToStream(Serializer serializer)
        {
            base.ToStream(serializer);
            serializer.Write(assemblyID);
            serializer.Write(nativeModule);
            serializer.Write((int)flags);
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            int flags;
            base.FromStream(deserializer);
            deserializer.Read(out assemblyID);
            deserializer.Read(out nativeModule);
            deserializer.Read(out flags); this.flags = (ModuleFlags)flags;
        }
        #endregion
    }

    /// <summary>
    /// code:CallStackIndex uniquely identifies a callstack within the log.  Valid values are between 0 and
    /// code:TraceCallStacks.CallStackIndexLimit, Thus an array can be used to 'attach' data to a callstack.   
    /// </summary>
    public enum CallStackIndex { Invalid = -1 };

    public class TraceCallStacks : IFastSerializable, IEnumerable<TraceCallStack>
    {
        public int MaxCallStackIndex { get { return callStacks.Count; } }
        public CodeAddressIndex CodeAddressIndex(CallStackIndex stackIndex) { return callStacks[(int)stackIndex].codeAddressIndex; }
        public CallStackIndex Caller(CallStackIndex stackIndex)
        {
            CallStackIndex ret = callStacks[(int)stackIndex].callerIndex;
            Debug.Assert(ret < stackIndex);         // Stacks should be getting 'smaller'
            if (ret < 0)                            // We encode the theads of the stack as the negative thread index.  
                ret = CallStackIndex.Invalid;
            return ret;
        }
        public int Depth(CallStackIndex stackIndex)
        {
            int ret = 0;
            while (stackIndex >= 0)
            {
                Debug.Assert(ret < 1000000);       // Catches infinite recursion 
                ret++;
                stackIndex = callStacks[(int)stackIndex].callerIndex;
            }
            return ret;
        }
        public TraceCallStack this[CallStackIndex callStackIndex]
        {
            get
            {
                // We don't bother interning. 
                if (callStackIndex == CallStackIndex.Invalid)
                    return null;
                return new TraceCallStack(this, callStackIndex);
            }
        }
        public TraceCodeAddresses CodeAddresses { get { return codeAddresses; } }
        public IEnumerator<TraceCallStack> GetEnumerator()
        {
            for (int i = 0; i < MaxCallStackIndex; i++)
                yield return this[(CallStackIndex)i];
        }
        public IEnumerable<CallStackIndex> GetAllIndexes
        {
            get
            {
                for (int i = 0; i < MaxCallStackIndex; i++)
                    yield return (CallStackIndex)i;
            }
        }
        public ThreadIndex ThreadIndex(CallStackIndex stackIndex)
        {
            // Go to the theads of the stack
            while (stackIndex >= 0)
            {
                Debug.Assert(callStacks[(int)stackIndex].callerIndex < stackIndex);
                stackIndex = callStacks[(int)stackIndex].callerIndex;
            }
            // The theads of the stack is marked by a negative number, which is the thread index -2
            ThreadIndex ret = (ThreadIndex)((-((int)stackIndex)) - 2);
            Debug.Assert(-1 <= (int)ret && (int)ret < log.Threads.MaxThreadIndex);
            return ret;
        }
        public TraceThread Thread(CallStackIndex stackIndex)
        {
            return log.Threads[ThreadIndex(stackIndex)];
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<TraceCallStacks Count=").Append(XmlUtilities.XmlQuote(callStacks.Count)).AppendLine(">");
            foreach (TraceCallStack callStack in this)
                sb.Append("  ").Append(callStack.ToString()).AppendLine();
            sb.AppendLine("</TraceCallStacks>");
            return sb.ToString();
        }
        #region private
        internal TraceCallStacks(TraceLog log, TraceCodeAddresses codeAddresses)
        {
            this.log = log;
            this.codeAddresses = codeAddresses;
        }

        /// <summary>
        /// Used to 'undo' the effects of adding a eventToStack that you no longer want.  This happens when we find
        /// out that a eventToStack is actually got more callers in it (when a eventToStack is split).  
        /// </summary>
        /// <param name="origSize"></param>
        internal void SetSize(int origSize)
        {
            callStacks.RemoveRange(origSize, callStacks.Count - origSize);
        }

        /// <summary>
        /// Returns an index that represents the 'theads' of the stack.  It encodes the thread which owns this stack into this. 
        /// We encode this as -ThreadIndex - 2 (since -1 is the Invalid node)
        /// </summary>
        private CallStackIndex GetRootForThread(ThreadIndex threadIndex)
        {
            return (CallStackIndex)(-((int)threadIndex) + (int)CallStackIndex.Invalid - 1);
        }
        private ThreadIndex GetThreadForRoot(CallStackIndex root)
        {
            ThreadIndex ret = (ThreadIndex)((-((int)root)) + (int)CallStackIndex.Invalid - 1);
            Debug.Assert(ret >= 0);
            return ret;
        }

        unsafe internal CallStackIndex GetStackIndexForStackEvent(long timeStamp100ns, void* addresses,
            int addressCount, bool is64BitAddresses, TraceThread thread, CallStackIndex start = CallStackIndex.Invalid)
        {
            var ret = start;
            if (ret == CallStackIndex.Invalid)
                ret = GetRootForThread(thread.ThreadIndex);
            for (int i = addressCount - 1; 0 <= i; --i)
            {
                Address address;
                if (is64BitAddresses)
                    address = ((ulong*)addresses)[i];
                else
                    address = ((uint*)addresses)[i];

                CodeAddressIndex codeAddress = codeAddresses.GetOrCreateCodeAddressIndex(thread.Process, timeStamp100ns, address);
                ret = InternCallStackIndex(codeAddress, ret);
            }
            return ret;
        }

        private CallStackIndex InternCallStackIndex(CodeAddressIndex codeAddressIndex, CallStackIndex callerIndex)
        {
            if (callStacks.Count == 0)
            {
                // allocate a resonable size for the interning tables. 
                callStacks = new GrowableArray<CallStackInfo>(10000);
                callees = new GrowableArray<List<CallStackIndex>>(10000);
            }

            List<CallStackIndex> frameCallees;
            if (callerIndex < 0)        // Hit the last stack as we unwind to the root.  We need to encode the thread.  
            {
                Debug.Assert(callerIndex != CallStackIndex.Invalid);        // We always end with the thread.  
                int threadIndex = (int)GetThreadForRoot(callerIndex);
                if (threadIndex >= threads.Count)
                    threads.Count = threadIndex + 1;
                frameCallees = threads[threadIndex];
                if (frameCallees == null)
                    threads[threadIndex] = frameCallees = new List<CallStackIndex>();
            }
            else
            {
                frameCallees = callees[(int)callerIndex];
                if (frameCallees == null)
                    callees[(int)callerIndex] = frameCallees = new List<CallStackIndex>(4);
            }

            // Search backwards, assuming that most reciently added is the most likely hit.  
            for (int i = frameCallees.Count - 1; i >= 0; --i)
            {
                CallStackIndex calleeIndex = frameCallees[i];
                if (callStacks[(int)calleeIndex].codeAddressIndex == codeAddressIndex)
                {
                    Debug.Assert(calleeIndex > callerIndex);
                    return calleeIndex;
                }
            }
            CallStackIndex ret = (CallStackIndex)callStacks.Count;
            callStacks.Add(new CallStackInfo(codeAddressIndex, callerIndex));
            frameCallees.Add(ret);
            callees.Add(null);
            Debug.Assert(callees.Count == callStacks.Count);
            return ret;
        }

        private struct CallStackInfo
        {
            internal CallStackInfo(CodeAddressIndex codeAddressIndex, CallStackIndex callerIndex)
            {
                this.codeAddressIndex = codeAddressIndex;
                this.callerIndex = callerIndex;
            }

            internal CodeAddressIndex codeAddressIndex;
            internal CallStackIndex callerIndex;
        }

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(log);
            serializer.Write(codeAddresses);
            lazyCallStacks.Write(serializer, delegate
            {
                serializer.Log("<WriteColection name=\"callStacks\" count=\"" + callStacks.Count + "\">\r\n");
                serializer.Write(callStacks.Count);
                for (int i = 0; i < callStacks.Count; i++)
                {
                    serializer.Write((int)callStacks[i].codeAddressIndex);
                    serializer.Write((int)callStacks[i].callerIndex);
                }
                serializer.Log("</WriteColection>\r\n");
            });
        }

        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            deserializer.Read(out log);
            deserializer.Read(out codeAddresses);

            lazyCallStacks.Read(deserializer, delegate
            {
                deserializer.Log("<Marker Name=\"callStacks\"/>");
                int count = deserializer.ReadInt();
                callStacks = new GrowableArray<CallStackInfo>(count + 1);
                CallStackInfo callStackInfo = new CallStackInfo();
                for (int i = 0; i < count; i++)
                {
                    callStackInfo.codeAddressIndex = (CodeAddressIndex)deserializer.ReadInt();
                    callStackInfo.callerIndex = (CallStackIndex)deserializer.ReadInt();
                    callStacks.Add(callStackInfo);
                }
            });
            lazyCallStacks.FinishRead();        // TODO REMOVE 
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException(); // GetEnumerator
        }

        // This is only used when converting maps.  Maps a call stack index to a list of call stack indexes that
        // were callees of it.    This is the list you need to search when interning.  There is also 'theads'
        // which is the list of call stack indexes where stack crawling stopped. 
        private GrowableArray<List<CallStackIndex>> callees;                // For each callstack, these are all the call stacks that it calls. 
        private GrowableArray<List<CallStackIndex>> threads;                 // callees for theads of stacks, one for each thread
        // a field on CallStackInfo
        private GrowableArray<CallStackInfo> callStacks;
        private DeferedRegion lazyCallStacks;
        private TraceCodeAddresses codeAddresses;
        private TraceLog log;
        #endregion
    }

    /// <summary>
    /// A TraceCallStack is a structure that represents a call eventToStack as a linked list.  It contains the
    /// Address in the current frame, and the pointer to the caller's eventToStack.  
    /// </summary>
    public class TraceCallStack
    {
        public CallStackIndex CallStackIndex { get { return stackIndex; } }
        public TraceCodeAddress CodeAddress { get { return callStacks.CodeAddresses[callStacks.CodeAddressIndex(stackIndex)]; } }
        public TraceCallStack Caller { get { return callStacks[callStacks.Caller(stackIndex)]; } }
        public int Depth { get { return callStacks.Depth(stackIndex); } }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(4096);
            return ToString(sb).ToString();
        }
        public StringBuilder ToString(StringBuilder sb)
        {
            TraceCallStack cur = this;
            while (cur != null)
            {
                cur.CodeAddress.ToString(sb).AppendLine();
                cur = cur.Caller;
            }
            return sb;
        }
        #region private
        internal TraceCallStack(TraceCallStacks stacks, CallStackIndex stackIndex)
        {
            this.callStacks = stacks;
            this.stackIndex = stackIndex;
        }

        private TraceCallStacks callStacks;
        private CallStackIndex stackIndex;
        #endregion
    }

    /// <summary>
    /// code:MethodIndex uniquely identifies a method within the log.  Valid values are between 0 and
    /// code:TraceMethods.MaxMethodIndex, Thus an array can be used to 'attach' data to a method.   
    /// </summary>
    public enum MethodIndex { Invalid = -1 };
    public class TraceMethods : IFastSerializable, IEnumerable<TraceMethod>
    {
        public int MaxMethodIndex { get { return methods.Count; } }
        public int MethodToken(MethodIndex methodIndex)
        {
            if (methodIndex == MethodIndex.Invalid)
                return 0;
            else
            {
                var value = methods[(int)methodIndex].methodDefOrRva;
                if (value < 0)
                    value = 0;      // unmanaged code, return 0
                return value;
            }
        }
        public int MethodRva(MethodIndex methodIndex)
        {
            if (methodIndex == MethodIndex.Invalid)
                return 0;
            else
            {
                var value = methods[(int)methodIndex].methodDefOrRva;
                if (value > 0)
                    value = 0;      // managed code, return 0
                return -value;
            }
        }
        public ModuleFileIndex MethodModuleFileIndex(MethodIndex methodIndex)
        {
            if (methodIndex == MethodIndex.Invalid)
                return ModuleFileIndex.Invalid;
            else
                return methods[(int)methodIndex].moduleIndex;
        }
        public string FullMethodName(MethodIndex methodIndex)
        {
            if (methodIndex == MethodIndex.Invalid)
                return "";
            else
                return methods[(int)methodIndex].fullMethodName;
        }

        public TraceMethod this[MethodIndex methodIndex]
        {
            get
            {
                if (methodObjects == null || (int)methodIndex >= methodObjects.Length)
                    methodObjects = new TraceMethod[(int)methodIndex + 16];

                if (methodIndex == MethodIndex.Invalid)
                    return null;

                TraceMethod ret = methodObjects[(int)methodIndex];
                if (ret == null)
                {
                    ret = new TraceMethod(this, methodIndex);
                    methodObjects[(int)methodIndex] = ret;
                }
                return ret;
            }
        }

        public IEnumerator<TraceMethod> GetEnumerator()
        {
            for (int i = 0; i < MaxMethodIndex; i++)
                yield return this[(MethodIndex)i];
        }
        public IEnumerable<MethodIndex> GetAllIndexes
        {
            get
            {
                for (int i = 0; i < MaxMethodIndex; i++)
                    yield return (MethodIndex)i;
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<TraceMethods Count=").Append(XmlUtilities.XmlQuote(methods.Count)).AppendLine(">");
            foreach (TraceMethod method in this)
                sb.Append("  ").Append(method.ToString()).AppendLine();
            sb.AppendLine("</TraceMethods>");
            return sb.ToString();
        }
        #region private
        internal TraceMethods(TraceCodeAddresses codeAddresses) { this.codeAddresses = codeAddresses; }

        internal MethodIndex NewMethod(string fullMethodName, ModuleFileIndex moduleIndex, int methodToken)
        {
            MethodIndex ret = (MethodIndex)methods.Count;
            methods.Add(new MethodInfo(fullMethodName, moduleIndex, methodToken));
            return ret;
        }

        void IFastSerializable.ToStream(Serializer serializer)
        {
            lazyMethods.Write(serializer, delegate
            {
                serializer.Write(codeAddresses);
                serializer.Write(methods.Count);
                serializer.Log("<WriteColection name=\"methods\" count=\"" + methods.Count + "\">\r\n");
                for (int i = 0; i < methods.Count; i++)
                {
                    serializer.Write(methods[i].fullMethodName);
                    serializer.Write(methods[i].methodDefOrRva);
                    serializer.Write((int)methods[i].moduleIndex);
                }
                serializer.Log("</WriteColection>\r\n");
            });
        }

        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            lazyMethods.Read(deserializer, delegate
            {
                deserializer.Read(out codeAddresses);
                int count = deserializer.ReadInt();
                deserializer.Log("<Marker name=\"methods\" count=\"" + count + "\"/>");
                MethodInfo methodInfo = new MethodInfo();
                methods = new GrowableArray<MethodInfo>(count + 1);

                for (int i = 0; i < count; i++)
                {
                    deserializer.Read(out methodInfo.fullMethodName);
                    deserializer.Read(out methodInfo.methodDefOrRva);
                    methodInfo.moduleIndex = (ModuleFileIndex)deserializer.ReadInt();
                    methods.Add(methodInfo);
                }
            });
            lazyMethods.FinishRead();        // TODO REMOVE 
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException(); // GetEnumerator
        }

        private struct MethodInfo
        {
            internal MethodInfo(string fullMethodName, ModuleFileIndex moduleIndex, int methodToken)
            {
                this.fullMethodName = fullMethodName;
                this.moduleIndex = moduleIndex;
                this.methodDefOrRva = methodToken;
            }
            internal string fullMethodName;
            internal ModuleFileIndex moduleIndex;
            internal int methodDefOrRva;               // For managed code, this is the token, for unmanged it is -rva (rvas have to be < 2Gig).  
        }

        private DeferedRegion lazyMethods;
        private GrowableArray<MethodInfo> methods;
        private TraceMethod[] methodObjects;
        internal TraceCodeAddresses codeAddresses;
        #endregion
    }

    /// <summary>
    /// A TraceMethod represents the symbolic information for a particular method.
    /// It does NOT know what process it lives in or what TraceLoadedModule it
    /// is loaded in, but DOES know what TraceModuleFile and source line that is
    /// associated with it
    /// </summary>
    public class TraceMethod
    {
        public MethodIndex MethodIndex { get { return methodIndex; } }
        public string FullMethodName { get { return methods.FullMethodName(methodIndex); } }
        /// <summary>
        /// returns 0 for unmanged code or method not found. 
        /// </summary>
        public int MethodToken { get { return methods.MethodToken(methodIndex); } }
        /// <summary>
        /// Returns 0 for managed code or method not found;
        /// </summary>
        public int MethodRva { get { return methods.MethodRva(methodIndex); } }
        public ModuleFileIndex MethodModuleFileIndex { get { return methods.MethodModuleFileIndex(methodIndex); } }
        public TraceModuleFile MethodModuleFile { get { return methods.codeAddresses.ModuleFiles[MethodModuleFileIndex]; } }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            return ToString(sb).ToString();
        }
        public StringBuilder ToString(StringBuilder sb)
        {
            sb.Append("  <TraceMethod ");
            if (FullMethodName.Length > 0)
                sb.Append(" FullMethodName=\"").Append(XmlUtilities.XmlEscape(FullMethodName, false)).Append("\"");
            sb.Append(" MethodIndex=\"").Append(XmlUtilities.XmlEscape(MethodIndex, false)).Append("\"");
            sb.Append(" MethodToken=\"").Append(XmlUtilities.XmlEscape(MethodToken, false)).Append("\"");
            sb.Append(" MethodRva=\"").Append(XmlUtilities.XmlEscape(MethodRva, false)).Append("\"");
            sb.Append("/>");
            return sb;
        }
        #region private
        internal TraceMethod(TraceMethods methods, MethodIndex methodIndex)
        {
            this.methods = methods;
            this.methodIndex = methodIndex;
        }

        TraceMethods methods;
        MethodIndex methodIndex;
        #endregion
    }

    /// <summary>
    /// code:CodeAddressIndex uniquely identifies a symbolic codeAddress within the log (note that the SAME
    /// physical addresses can have a different symbolic codeAddress because they are in different
    /// processes). Valid values are between 0 and code:TraceCodeAddresses.MaxCodeAddressIndex, Thus an array
    /// can be used to 'attach' data to a method.
    /// </summary>
    public enum CodeAddressIndex { Invalid = -1 };
    public class
        TraceCodeAddresses : IFastSerializable, IEnumerable<TraceCodeAddress>
    {
        public int MaxCodeAddressIndex { get { return codeAddresses.Count; } }
        public string Name(CodeAddressIndex codeAddressIndex)
        {
            if (names == null)
                names = new string[MaxCodeAddressIndex];
            string name = names[(int)codeAddressIndex];
            if (name == null)
            {
                string moduleName = "?";
                ModuleFileIndex moduleIdx = ModuleFileIndex(codeAddressIndex);
                if (moduleIdx != Diagnostics.Tracing.ModuleFileIndex.Invalid)
                    moduleName = moduleFiles[moduleIdx].Name;

                string methodName;
                MethodIndex methodIndex = MethodIndex(codeAddressIndex);
                if (methodIndex != Diagnostics.Tracing.MethodIndex.Invalid)
                    methodName = Methods.FullMethodName(methodIndex);
                else
                    methodName = "0x" + ((ulong)Address(codeAddressIndex)).ToString("x");
                name = moduleName + "!" + methodName;
            }
            return name;
        }
        public Address Address(CodeAddressIndex codeAddressIndex) { return codeAddresses[(int)codeAddressIndex].Address; }
        public ModuleFileIndex ModuleFileIndex(CodeAddressIndex codeAddressIndex)
        {
            var ret = codeAddresses[(int)codeAddressIndex].ModuleFileIndex;
            // If we have a method index, fetch the module file from the method. 
            if (ret == Diagnostics.Tracing.ModuleFileIndex.Invalid)
                ret = Methods.MethodModuleFileIndex(MethodIndex(codeAddressIndex));
            return ret;
        }
        public MethodIndex MethodIndex(CodeAddressIndex codeAddressIndex) { return codeAddresses[(int)codeAddressIndex].GetMethodIndex(this); }
        public TraceModuleFile ModuleFile(CodeAddressIndex codeAddressIndex) { return ModuleFiles[ModuleFileIndex(codeAddressIndex)]; }
        /// <summary>
        /// returns -1 if the code address is not managed (or unknown). 
        /// </summary>
        public int ILOffset(CodeAddressIndex codeAddressIndex)
        {
            ILToNativeMap ilMap = NativeMap(codeAddressIndex);
            if (ilMap == null)
                return -1;
            return ilMap.GetILOffsetForNativeAddress(Address(codeAddressIndex));
        }
        /// <summary>
        /// We expose ILToNativeMap internally so we can do diagnostics.   
        /// </summary>
        internal ILToNativeMap NativeMap(CodeAddressIndex codeAddressIndex)
        {
            var ilMapIdx = codeAddresses[(int)codeAddressIndex].GetILMapIndex(this);
            if (ilMapIdx == ILMapIndex.Invalid)
                return null;

            return ILToNativeMaps[(int)ilMapIdx];
        }

        public TraceCodeAddress this[CodeAddressIndex codeAddressIndex]
        {
            get
            {
                if (codeAddressObjects == null || (int)codeAddressIndex >= codeAddressObjects.Length)
                    codeAddressObjects = new TraceCodeAddress[(int)codeAddressIndex + 16];

                if (codeAddressIndex == CodeAddressIndex.Invalid)
                    return null;

                TraceCodeAddress ret = codeAddressObjects[(int)codeAddressIndex];
                if (ret == null)
                {
                    ret = new TraceCodeAddress(this, codeAddressIndex);
                    codeAddressObjects[(int)codeAddressIndex] = ret;
                }
                return ret;
            }
        }
        public IEnumerator<TraceCodeAddress> GetEnumerator()
        {
            for (int i = 0; i < MaxCodeAddressIndex; i++)
                yield return this[(CodeAddressIndex)i];
        }
        public IEnumerable<CodeAddressIndex> GetAllIndexes
        {
            get
            {
                for (int i = 0; i < MaxCodeAddressIndex; i++)
                    yield return (CodeAddressIndex)i;
            }
        }
        public TraceMethods Methods { get { return methods; } }
        public TraceModuleFiles ModuleFiles { get { return moduleFiles; } }
        /// <summary>
        /// Indicates the number of managed method records that were encountered.
        /// </summary>
        public int ManagedMethodRecordCount { get { return managedMethodRecordCount; } }
        public void LookupSymbolsForModule(SymbolReader reader, TraceModuleFile file)
        {
            var codeAddrs = new List<CodeAddressIndex>();
            for (int i = 0; i < MaxCodeAddressIndex; i++)
            {
                if (codeAddresses[i].ModuleFileIndex == file.ModuleFileIndex &&
                    codeAddresses[i].GetMethodIndex(this) == Diagnostics.Tracing.MethodIndex.Invalid)
                    codeAddrs.Add((CodeAddressIndex)i);
            }

            if (codeAddrs.Count == 0)
            {
                reader.m_log.WriteLine("No code addresses are in {0} that have not already been looked up.", file.Name);
                return;
            }

            // sort them.  TODO can we get away without this?
            codeAddrs.Sort(delegate(CodeAddressIndex x, CodeAddressIndex y)
            {
                ulong addrX = (ulong)Address(x);
                ulong addrY = (ulong)Address(y);
                if (addrX > addrY)
                    return 1;
                if (addrX < addrY)
                    return -1;
                return 0;
            });

            int totalAddressCount;

            // Skip to the addresses in this module 
            var codeAddrEnum = codeAddrs.GetEnumerator();
            for (; ; )
            {
                if (!codeAddrEnum.MoveNext())
                    return;
                if (Address(codeAddrEnum.Current) >= file.ImageBase)
                    break;
            }
            try
            {
                LookupSymbolsForModule(reader, file, codeAddrEnum, true, out totalAddressCount);
            }
            catch (OutOfMemoryException)
            {
                // TODO find out why this happens?   I think this is because we try to do a ReadRVA 
                // a managed-only module 
                reader.m_log.WriteLine("Error: Caught out of memory exception on file " + file.Name + ".   Skipping.");
            }
        }
        public SourceLocation GetSourceLine(SymbolReader reader, CodeAddressIndex codeAddressIndex)
        {
            reader.m_log.WriteLine("GetSourceLine: Getting source line for code address index {0:x}", codeAddressIndex);

            if (codeAddressIndex == CodeAddressIndex.Invalid)
            {
                reader.m_log.WriteLine("GetSourceLine: Invalid code address");
                return null;
            }

            var moduleFile = log.CodeAddresses.ModuleFile(codeAddressIndex);
            if (moduleFile == null)
            {
                reader.m_log.WriteLine("GetSourceLine: Could not find moduleFile {0:x}.", log.CodeAddresses.Address(codeAddressIndex));
                return null;
            }

            SymbolModule symbolReaderModule;
            // Is this address in the native code of the module (inside the bounds of module)
            var address = log.CodeAddresses.Address(codeAddressIndex);
            reader.m_log.WriteLine("GetSourceLine: address for code address is {0:x} module {1}", address, moduleFile.Name);
            if (moduleFile.ImageBase != 0 && moduleFile.ImageBase <= address && address < moduleFile.ImageEnd)
            {
                var methodRva = (uint)(address - moduleFile.ImageBase);
                reader.m_log.WriteLine("GetSourceLine: address within module: native case, RVA = {0:x}", methodRva);
                symbolReaderModule = GetSymbolReaderModule(reader, moduleFile);
                if (symbolReaderModule != null)
                {
                    var ret = symbolReaderModule.SourceLocationForRva(methodRva);
                    // TODO FIX NOW, deal with this rather than simply warn. 
                    if (ret == null && symbolReaderModule.PdbPath.EndsWith(".ni.pdb", StringComparison.OrdinalIgnoreCase))
                    {
                        reader.m_log.WriteLine("GetSourceLine: Warning could not find line information in {0}", symbolReaderModule.PdbPath);
                        reader.m_log.WriteLine("GetSourceLine: Maybe because the NGEN pdb was generated without being able to reach the IL PDB");
                        reader.m_log.WriteLine("GetSourceLine: If you are on the machine where the data was collected, deleting the file may help");
                    }

                    return ret;
                }
                reader.m_log.WriteLine("GetSourceLine: Failed to look up {0:x} in a PDB, checking for JIT", log.CodeAddresses.Address(codeAddressIndex));
            }

            // The address is not in the module, or we could not find the PDB, see if we have JIT information 
            var methodIndex = log.CodeAddresses.MethodIndex(codeAddressIndex);
            if (methodIndex == Diagnostics.Tracing.MethodIndex.Invalid)
            {
                reader.m_log.WriteLine("GetSourceLine: Could not find method for {0:x}", log.CodeAddresses.Address(codeAddressIndex));
                return null;
            }

            var methodToken = log.CodeAddresses.Methods.MethodToken(methodIndex);
            if (methodToken == 0)
            {
                reader.m_log.WriteLine("GetSourceLine: Could not find method for {0:x}", log.CodeAddresses.Address(codeAddressIndex));
                return null;
            }
            reader.m_log.WriteLine("GetSourceLine: Found JITTed method {0}, index {1:x} token {2:x}",
                log.CodeAddresses.Methods.FullMethodName(methodIndex), methodIndex, methodToken);

            // See if we have il offset information for the method. 
            // var ilOffset = log.CodeAddresses.ILOffset(codeAddressIndex);
            var ilMap = log.CodeAddresses.NativeMap(codeAddressIndex);
            int ilOffset = 0;
            if (ilMap != null)
            {
                reader.m_log.WriteLine("GetSourceLine: Found an il-to-native mapping MethodIdx {0:x} Start {1:x} Len {2:x}",
                    ilMap.MethodIndex, ilMap.MethodStart, ilMap.MethodLength);

                // TODO remove after we are happy that this works properly.   
                for (int i = 0; i < ilMap.Map.Count; i++)
                    reader.m_log.WriteLine("GetSourceLine:    {0,3} native {1,5:x} -> {2:x}",
                        i, ilMap.Map[i].NativeOffset, ilMap.Map[i].ILOffset);

                ilOffset = ilMap.GetILOffsetForNativeAddress(address);
                reader.m_log.WriteLine("GetSourceLine: NativeOffset {0:x} ILOffset = {1:x}", 
                    address - ilMap.MethodStart, ilOffset);

                if (ilOffset < 0)
                    ilOffset = 0;       // If we return the special ILProlog or ILEpilog values.  
            }

            // Get the IL file even if we are in an NGEN image.
            if (moduleFile.ManagedModule != null)
                moduleFile = moduleFile.ManagedModule;

            symbolReaderModule = GetSymbolReaderModule(reader, moduleFile);
            if (symbolReaderModule == null)
            {
                reader.m_log.WriteLine("GetSourceLine: Failed to look up PDB for {0}", moduleFile.FilePath);
                return null;
            }

            return symbolReaderModule.SourceLocationForManagedCode((uint)methodToken, ilOffset);
        }

        /// <summary>
        /// Calls OpenModuleFile and caches the last entry
        /// </summary>
        private unsafe SymbolModule GetSymbolReaderModule(SymbolReader reader, TraceModuleFile moduleFile)
        {
            SymbolModule symbolReaderModule;
            if (m_lastModuleFile == moduleFile)
                symbolReaderModule = m_lastSymbolModule;
            else
            {
                symbolReaderModule = OpenPdbForModuleFile(reader, moduleFile);
                if (symbolReaderModule == null)
                {
                    reader.m_log.WriteLine("GetSourceLine: Could not open PDB for {0}", moduleFile.FilePath);
                    return null;
                }
                m_lastModuleFile = moduleFile;
                m_lastSymbolModule = symbolReaderModule;
            }
            return symbolReaderModule;
        }

        SymbolModule m_lastSymbolModule;
        TraceModuleFile m_lastModuleFile;

        /// <summary>
        /// The number of times a code address appears in the log.   Unlike MaxCodeAddressIndex, TotalCodeAddresses counts the same address
        /// in different places (even if in the same stack) as distinct.   
        /// 
        /// The sum of ModuleFile.CodeAddressesInModule for all modules should sum to this number.
        /// </summary>
        public int TotalCodeAddresses { get { return totalCodeAddresses; } }
        /// <summary>
        /// Will look up PDBs without validating that the GUID is OK.   Pretty dangerous, don't use it if you avoid it.
        /// </summary>
        public bool UnsafePDBMatching;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<TraceCodeAddresses Count=").Append(XmlUtilities.XmlQuote(codeAddresses.Count)).AppendLine(">");
            foreach (TraceCodeAddress codeAddress in this)
                sb.Append("  ").Append(codeAddress.ToString()).AppendLine();
            sb.AppendLine("</TraceCodeAddresses>");
            return sb.ToString();
        }
        #region private
        internal TraceCodeAddresses(TraceLog log, TraceModuleFiles moduleFiles)
        {
            this.log = log;
            this.moduleFiles = moduleFiles;
            this.methods = new TraceMethods(this);
        }

        internal delegate void ForAllCodeAddrAction(TraceProcess process, ref CodeAddressInfo codeAddrInfo);
        /// <summary>
        /// Allows you to get a callback for each code address that is in the range from start to 
        /// start+length within the process 'process'.   It is legal to have process==0 if you supply 'context'
        /// (which is used to find the context).  This is useful because we only look up the process lazily
        /// </summary>
        internal void ForAllUnresolvedCodeAddressesInRange(Address start, int length,
            TraceProcess process, TraceEvent context, ForAllCodeAddrAction body)
        {
            // If there are no code address of interest, then we have nothing to do.  
            if (codeAddressBuckets == null)
                return;

            Address endAddressInclusive = (Address)((long)start + length - 1);
            long curBucket = RoundToBucket((long)start);
            long endBucket = RoundToBucket((long)endAddressInclusive);
            for (; ; )
            {
                if (curBucket > endBucket)
                    return;

                CodeAddressBucketEntry codeAddressEntry;
                if (codeAddressBuckets.TryGetValue(curBucket, out codeAddressEntry))
                {
                    do
                    {
                        Address entryAddress = codeAddressEntry.address;
                        // Does this address fall within the range of the method the 'data' event describes?
                        if (start <= entryAddress && entryAddress <= endAddressInclusive)
                        {
                            // If the entry already has symbolic info (a MethodIndex) then we are done 
                            CodeAddressInfo info = codeAddresses[(int)codeAddressEntry.codeAddressIndex];

                            // OK we need the process, to check that the processes match.  
                            // Get the moduleFileIndex and methodIndex while we are at it.    
                            if (process == null)
                            {
                                process = log.Processes.GetProcess(context.ProcessID, context.TimeStamp100ns);
                                if (process == null)
                                {
                                    log.DebugWarn(false, "Could not find process with ID " + context.ProcessID.ToString("x"), context);
                                    return;
                                }
                            }

                            // Do we match the process? then call the body.  
                            // The kernel (ProcessID 0 matches any process because these DLLs are mapped into every process).  
                            if (info.GetProcessIndex(this) == process.ProcessIndex || process.ProcessID == 0)
                                body(process, ref codeAddresses.UnderlyingArray[(int)codeAddressEntry.codeAddressIndex]);
                        }
                        codeAddressEntry = codeAddressEntry.next;
                    } while (codeAddressEntry != null);
                }
                curBucket += bucketSize;
            }
        }

        internal void AddNativeModule(ImageLoadTraceData data, ModuleFileIndex moduleFileIndex)
        {
            ForAllUnresolvedCodeAddressesInRange(data.ImageBase, data.ImageSize, null, data,
                delegate(TraceProcess process, ref CodeAddressInfo info)
                {
                    if (info.ModuleFileIndex == Diagnostics.Tracing.ModuleFileIndex.Invalid)
                        info.ModuleFileIndex = moduleFileIndex;
                });
        }

        /// <summary>
        /// Called when JIT CLR Rundown events are processed. It will look if there is any
        /// address that falls into the range of the JIT compiled method and if so log the
        /// symbolic information (otherwise we simply ignore it)
        /// </summary>
        internal void AddMethod(MethodLoadUnloadVerboseTraceData data)
        {
            managedMethodRecordCount++;
            MethodIndex methodIndex = Diagnostics.Tracing.MethodIndex.Invalid;
            ILMapIndex ilMap = ILMapIndex.Invalid;
            ModuleFileIndex moduleFileIndex = Diagnostics.Tracing.ModuleFileIndex.Invalid;
            TraceManagedModule module = null;
            ForAllUnresolvedCodeAddressesInRange(data.MethodStartAddress, data.MethodSize, null, data,
                delegate(TraceProcess process, ref CodeAddressInfo info)
                {
                    // If we already resolved, that means that the address was reused, so only add something if it does not already have 
                    // information associated with it.  
                    if (info.GetMethodIndex(this) == Diagnostics.Tracing.MethodIndex.Invalid)
                    {
                        // Lazily create the method since many methods never have code samples in them. 
                        if (module == null)
                        {
                            module = process.LoadedModules.GetOrCreateManagedModule(data.ModuleID, data.TimeStamp100ns);
                            moduleFileIndex = module.ModuleFile.ModuleFileIndex;
                            methodIndex = methods.NewMethod(TraceLog.GetFullName(data), moduleFileIndex, data.MethodToken);
                            if (data.IsJitted)
                                ilMap = UnloadILMapForMethod(methodIndex, data);
                        }
                        // Set the info 
                        info.SetMethodIndex(this, methodIndex);
                        if (ilMap != ILMapIndex.Invalid)
                            info.SetILMapIndex(this, ilMap);
                    }
                });
        }

        /// <summary>
        /// Adss a JScript method 
        /// </summary>
        internal void AddMethod(MethodLoadUnloadJSTraceData data, Dictionary<JavaScriptSourceKey, string> sourceById)
        {
            MethodIndex methodIndex = Diagnostics.Tracing.MethodIndex.Invalid;

            ForAllUnresolvedCodeAddressesInRange(data.MethodStartAddress, (int)data.MethodSize, null, data,
                delegate(TraceProcess process, ref CodeAddressInfo info)
                {
                    // If we already resolved, that means that the address was reused, so only add something if it does not already have 
                    // information associated with it.  
                    if (info.GetMethodIndex(this) == Diagnostics.Tracing.MethodIndex.Invalid)
                    {
                        // Lazily create the method since many methods never have code samples in them. 
                        if (methodIndex == Diagnostics.Tracing.MethodIndex.Invalid)
                        {
                            string sourceName = null;
                            /* TODO FIX NOW decide what to do here */
                            if (sourceById.TryGetValue(new JavaScriptSourceKey(data.SourceID, data.ScriptContextID), out sourceName))
                            {
                                var lastSlashIdx = sourceName.LastIndexOf('/');
                                if (0 < lastSlashIdx)
                                    sourceName = sourceName.Substring(lastSlashIdx + 1);
                            }
                            if (sourceName == null)
                                sourceName = "JAVASCRIPT";

                            var methodName = data.MethodName;
                            if (data.Line != 0)
                                methodName = methodName + " Line: " + data.Line.ToString();

                            var moduleFile = log.ModuleFiles.GetOrCreateModuleFile(sourceName, 0);
                            methodIndex = methods.NewMethod(methodName, moduleFile.ModuleFileIndex, data.MethodID);
                        }
                        // Set the info 
                        info.SetMethodIndex(this, methodIndex);
                    }
                });
        }

        // This supports the lookup of CodeAddresses by range.  
        const long bucketSize = 64;
        static long RoundToBucket(long value)
        {
            Debug.Assert((bucketSize & (bucketSize - 1)) == 0);       // bucketSize must be a power of 2
            return value & (~(bucketSize - 1));
        }

        /// <summary>
        /// Gets the symbolic information entry for 'address' which can be any address.  If it falls in the
        /// range of a symbol, then that symbolic information is returned.  Regardless of whether symbolic
        /// information is found, however, an entry is created for it, so every unique address has an entry
        /// in this table.  
        /// </summary>
        internal CodeAddressIndex GetOrCreateCodeAddressIndex(TraceProcess process, long time100ns, Address address)
        {
            CodeAddressBucketEntry codeAddressInfo;
            long roundedAddress = RoundToBucket((long)address);
            if (codeAddressBuckets == null)
            {
                codeAddressBuckets = new Dictionary<long, CodeAddressBucketEntry>(5000);
                if (codeAddresses.Count == 0)
                    codeAddresses = new GrowableArray<CodeAddressInfo>(10000);
            }
            if (!codeAddressBuckets.TryGetValue(roundedAddress, out codeAddressInfo))
            {
                codeAddressInfo = NewCodeAddressEntry(process, address, null);
                codeAddressBuckets.Add(roundedAddress, codeAddressInfo);
            }
            else if (address < codeAddressInfo.address)
            {
                codeAddressInfo = NewCodeAddressEntry(process, address, codeAddressInfo);
                codeAddressBuckets[roundedAddress] = codeAddressInfo;
            }
            else
            {
                for (; ; )
                {
                    // have we found an existing entry?, first the addresses have to match.
                    if (codeAddressInfo.address == address)
                    {
                        // We only match 'empty' entries.   This is because we add method or modulefile entries when a DLL or method
                        // unloads, which means that if the entry is non-empty, it means that the code address has actually been
                        // unloaded, and the address used for a new method.  Thus we don't want to match this entry but let a new
                        var codeAddressMethodIndex = MethodIndex(codeAddressInfo.codeAddressIndex);
                        if (codeAddressMethodIndex == Diagnostics.Tracing.MethodIndex.Invalid)
                        {
                            // Is the module empty?  
                            var codeAddressModuleFileIndex = ModuleFileIndex(codeAddressInfo.codeAddressIndex);
                            if (codeAddressModuleFileIndex == Diagnostics.Tracing.ModuleFileIndex.Invalid)
                            {
                                // We have an empty entry and the the process matches we have found an existing entry to return!
                                var codeAddressProcessIndex = codeAddresses[(int)codeAddressInfo.codeAddressIndex].GetProcessIndex(this);
                                if (codeAddressProcessIndex == process.ProcessIndex)
                                    break;
                            }
                            else
                            {
                                // Optimization.   If the code address has a ModuleFile and that matches what we would have set this entry
                                // to be, then even though the processes don't match we can reuse the entry because all interesting infromation
                                // in the CodeAddress is the same.   This allows us to share code addresses across process (eg for all Kernel32, 
                                // and other OS dlls which can be significant in multi-process scenarios). 

                                // This optimziation does not work for the circular buffer case, because we may not know that a module is loaded
                                // but, hey, this is just an optimization. 
                                TraceLoadedModule module = process.LoadedModules.GetModuleContainingAddress(address, time100ns);
                                if (module != null && module.ModuleFile.ModuleFileIndex == codeAddressModuleFileIndex)
                                    break;
                            }
                        }
                    }

                    CodeAddressBucketEntry nextEntry = codeAddressInfo.next;
                    if (nextEntry == null || address < nextEntry.address)
                    {
                        codeAddressInfo.next = NewCodeAddressEntry(process, address, nextEntry);
                        codeAddressInfo = codeAddressInfo.next;
                        break;
                    }
                    codeAddressInfo = codeAddressInfo.next;
                }
            }

            this.codeAddresses.UnderlyingArray[(int)codeAddressInfo.codeAddressIndex].UpdateStats();
            return codeAddressInfo.codeAddressIndex;
        }

        private CodeAddressBucketEntry NewCodeAddressEntry(TraceProcess process, Address address, CodeAddressBucketEntry next)
        {
            CodeAddressIndex codeAddressIndex = (CodeAddressIndex)codeAddresses.Count;
            codeAddresses.Add(new CodeAddressInfo(address, process.ProcessIndex));

            CodeAddressBucketEntry codeAddressInfo = new CodeAddressBucketEntry(address, codeAddressIndex, next);
            return codeAddressInfo;
        }

        /// <summary>
        /// Code ranges need to be looked up by arbitrary address. There are two basic ways of doing this
        /// efficiently. First a binary search, second create 'buckets' (fixed sized ranges, see
        /// code:bucketSize and code:RoundToBucket) and round any address to these buckets and look them up
        /// in a hash table. This latter option is what we use. What this means is that when a entry is added
        /// to the table (see code:AddMethod) it must be added to every bucket over its range. Each entry in
        /// the table is a code:CodeAddressBucketEntry which is simply a linked list.
        /// </summary>
        class CodeAddressBucketEntry
        {
            public CodeAddressBucketEntry(Address address, CodeAddressIndex codeAddress, CodeAddressBucketEntry next)
            {
                this.address = address;
                this.codeAddressIndex = codeAddress;
                this.next = next;
            }
            public Address address;
            public CodeAddressIndex codeAddressIndex;
            public CodeAddressBucketEntry next;
        }

        // TODO do we need this?
        /// <summary>
        /// Sort from lowest address to highest address. 
        /// </summary>
        IEnumerable<CodeAddressIndex> GetSortedCodeAddressIndexes()
        {
            List<CodeAddressIndex> list = new List<CodeAddressIndex>(GetAllIndexes);
            list.Sort(delegate(CodeAddressIndex x, CodeAddressIndex y)
            {
                ulong addrX = (ulong)Address(x);
                ulong addrY = (ulong)Address(y);
                if (addrX > addrY)
                    return 1;
                if (addrX < addrY)
                    return -1;
                return 0;
            });
            return list;
        }

        /// <summary>
        /// Do symbol resolution for all addresses in the log file. 
        /// </summary>
        internal void LookupSymbols(TraceLogOptions options)
        {
            if (options.SourceLineNumbers)
                options.ConversionLog.WriteLine("Looking up symbolic information for line number and methods.");
            else
                options.ConversionLog.WriteLine("Looking up symbolic information for methods (no line numbers).");

            SymbolReader reader = null;
            int totalAddressCount = 0;
            int noModuleAddressCount = 0;
            IEnumerator<CodeAddressIndex> codeAddressIndexCursor = GetSortedCodeAddressIndexes().GetEnumerator();
            bool notDone = codeAddressIndexCursor.MoveNext();
            while (notDone)
            {
                TraceModuleFile moduleFile = moduleFiles[ModuleFileIndex(codeAddressIndexCursor.Current)];
                if (moduleFile != null)
                {
                    if (options.ShouldResolveSymbols(moduleFile.FilePath))
                    {
                        if (reader == null)
                        {
                            var symPath = SymPath.CleanSymbolPath();
                            if (options.LocalSymbolsOnly)
                                symPath = symPath.LocalOnly();
                            var path = symPath.ToString();
                            options.ConversionLog.WriteLine("_NT_SYMBOL_PATH={0}", path);
                            reader = new SymbolReader(options.ConversionLog, path);
                        }
                        int moduleAddressCount = 0;
                        try
                        {
                            notDone = true;
                            LookupSymbolsForModule(reader, moduleFile, codeAddressIndexCursor, false, out moduleAddressCount);
                        }
                        catch (Exception e)
                        {
                            // TODO too strong. 
                            options.ConversionLog.WriteLine("An exception occurred during symbol lookup.  Continuing...");
                            options.ConversionLog.WriteLine("Exception: " + e.Message);
                        }
                        totalAddressCount += moduleAddressCount;
                    }

                    // Skip the rest of the addresses for that module.  
                    while ((moduleFiles[ModuleFileIndex(codeAddressIndexCursor.Current)] == moduleFile))
                    {
                        notDone = codeAddressIndexCursor.MoveNext();
                        if (!notDone)
                            break;
                        totalAddressCount++;
                    }
                }
                else
                {
                    // TraceLog.DebugWarn("Could not find a module for address " + ("0x" + Address(codeAddressIndexCursor.Current).ToString("x")).PadLeft(10));
                    notDone = codeAddressIndexCursor.MoveNext();
                    noModuleAddressCount++;
                    totalAddressCount++;
                }
            }

            if (reader != null)
                reader.Dispose();

            double noModulePercent = 0;
            if (totalAddressCount > 0)
                noModulePercent = noModuleAddressCount * 100.0 / totalAddressCount;
            options.ConversionLog.WriteLine("A total of " + totalAddressCount + " symbolic addresses were looked up.");
            options.ConversionLog.WriteLine("Addresses outside any module: " + noModuleAddressCount + " out of " + totalAddressCount + " (" + noModulePercent.ToString("f1") + "%)");
            options.ConversionLog.WriteLine("Done with symbolic lookup.");
        }

        // TODO number of args is getting messy.
        private void LookupSymbolsForModule(SymbolReader reader, TraceModuleFile moduleFile,
            IEnumerator<CodeAddressIndex> codeAddressIndexCursor, bool enumerateAll, out int totalAddressCount)
        {
            // TODO getSourceLineNumbers is not implemented.  
            totalAddressCount = 0;
            int existingSymbols = 0;
            int distinctSymbols = 0;
            int unmatchedSymbols = 0;
            int repeats = 0;

            // We can get the same name for different addresses, which makes us for distinct methods
            // which in turn cause the treeview to have multiple children with the same name.   This
            // is confusing, so we intern the symobls, insuring that code address with the same name
            // always use the same method.   This dictionary does that.  
            var methodIntern = new Dictionary<string, MethodIndex>();

            reader.m_log.WriteLine("[Loading symbols for " + moduleFile.FilePath + "]");

            SymbolModule moduleReader = OpenPdbForModuleFile(reader, moduleFile);
            if (moduleReader == null)
            {
                reader.m_log.WriteLine("Could not find PDB file.");
                return;
            }

            reader.m_log.WriteLine("Loaded, resolving symbols");
            string currentMethodName = "";
            MethodIndex currentMethodIndex = Diagnostics.Tracing.MethodIndex.Invalid;
            Address currentMethodEnd = 0;
            Address endModule = moduleFile.ImageEnd;
            for (; ; )
            {
                // options.ConversionLog.WriteLine("Code address = " + Address(codeAddressIndexCursor.Current).ToString("x"));
                totalAddressCount++;
                Address address = Address(codeAddressIndexCursor.Current);
                if (!enumerateAll && address >= endModule)
                    break;

                MethodIndex methodIndex = MethodIndex(codeAddressIndexCursor.Current);
                if (methodIndex == Diagnostics.Tracing.MethodIndex.Invalid)
                {
                    if (address < currentMethodEnd)
                    {
                        repeats++;
                        // options.ConversionLog.WriteLine("Repeat of " + currentMethodName + " at " + address.ToString("x"));  
                    }
                    else
                    {
                        var newMethodName = moduleReader.FindNameForRva((uint)(address - moduleFile.ImageBase));
                        if (newMethodName.Length > 0)
                        {
                            // TODO FIX NOW 
                            // Debug.WriteLine(string.Format("Info: address  0x{0:x} in sym {1}", address, newMethodName));
                            // TODO FIX NOW 
                            currentMethodEnd = address + 1;     // Look up each unique address.  

                            // TODO FIX NOW remove 
                            // newMethodName = newMethodName +  " 0X" + address.ToString("x");

                            // If we get the exact same method name, then again we have a repeat
                            // In theory this should not happen, but in it seems to happen in
                            // practice.  
                            if (newMethodName == currentMethodName)
                                repeats++;
                            else
                            {
                                currentMethodName = newMethodName;
                                if (!methodIntern.TryGetValue(newMethodName, out currentMethodIndex))
                                {
                                    currentMethodIndex = methods.NewMethod(newMethodName, moduleFile.ModuleFileIndex, 0);
                                    methodIntern[newMethodName] = currentMethodIndex;
                                    distinctSymbols++;
                                }
                            }
                        }
                        else
                        {
                            unmatchedSymbols++;
                            currentMethodName = "";
                            currentMethodIndex = Diagnostics.Tracing.MethodIndex.Invalid;
                        }
                    }

                    if (currentMethodIndex != Diagnostics.Tracing.MethodIndex.Invalid)
                    {
                        CodeAddressInfo codeAddressInfo = codeAddresses[(int)codeAddressIndexCursor.Current];
                        codeAddressInfo.SetMethodIndex(this, currentMethodIndex);
                        Debug.Assert(codeAddressInfo.ModuleFileIndex == moduleFile.ModuleFileIndex);
                        Debug.Assert(moduleFile.ModuleFileIndex != Diagnostics.Tracing.ModuleFileIndex.Invalid);
                        codeAddresses[(int)codeAddressIndexCursor.Current] = codeAddressInfo;
                    }
                }
                else
                {
                    // options.ConversionLog.WriteLine("Found existing method " + Methods[methodIndex].FullMethodName);
                    existingSymbols++;
                }

                if (!codeAddressIndexCursor.MoveNext())
                    break;
            }
            reader.m_log.WriteLine("    Addresses to look up       " + totalAddressCount);
            if (existingSymbols != 0)
                reader.m_log.WriteLine("        Existing Symbols       " + existingSymbols);
            reader.m_log.WriteLine("        Found Symbols          " + (distinctSymbols + repeats));
            reader.m_log.WriteLine("        Distinct Found Symbols " + distinctSymbols);
            reader.m_log.WriteLine("        Unmatched Symbols " + (totalAddressCount - (distinctSymbols + repeats)));
        }

        // These are a private cache for OpenModuleFile
        SymbolReader m_symbolReader;
        SymbolModule m_symbolReaderModule;
        TraceModuleFile m_moduleFileForSymbolReaderModule;

        /// <summary>
        /// Tries to open the PDB for 'moduleFile'.  Returns null if unsuccessful. 
        /// TODO FIX NOW this belongs on a TraceModuleFile.  
        /// </summary>
        public unsafe SymbolModule OpenPdbForModuleFile(SymbolReader symReader, TraceModuleFile moduleFile)
        {
            Debug.Assert(!symReader.IsDisposed);
            if (m_symbolReaderModule != null && m_moduleFileForSymbolReaderModule == moduleFile && symReader == m_symbolReader)
                return m_symbolReaderModule;

            // Is it an NGEN image, in that case try to generate it
            string pdbFileName = null;
            if (moduleFile.FilePath.EndsWith(".ni.dll", StringComparison.OrdinalIgnoreCase) ||
                moduleFile.FilePath.EndsWith(".ni.exe", StringComparison.OrdinalIgnoreCase))
            {
                bool shouldGenerateNGenPdb = log.CurrentMachineIsCollectionMachine();
                if (!shouldGenerateNGenPdb && moduleFile.TimeDateStamp != 0)
                {
                    // If we happen to have the same NGEN image on this machine, we can generate it here.
                    if (File.Exists(moduleFile.FilePath))
                    {
                        using (var peFile = new PEFile.PEFile(moduleFile.FilePath))
                        {
                            // Note that we NGEN the image if it is the same path (which is a pretty strong indication)
                            // and the same size.   This is not perfect but we will confirm it for sure later.  At worst we
                            // generate a NGEN pdb that we did not need. 
                            if (peFile.Header.SizeOfImage == moduleFile.ImageSize)
                                shouldGenerateNGenPdb = true;
                        }
                    }
                }
                if (shouldGenerateNGenPdb)
                {
                    if ((symReader.Flags & SymbolReaderFlags.NoNGenPDB) == 0)
                        pdbFileName = GenerateNGenPdb(symReader, moduleFile.FilePath);
                    else
                        symReader.m_log.WriteLine("No NGenPDB set on symbol reader, if not found, will skip generation of {0}", moduleFile.FilePath);
                }
                else
                {
                    symReader.m_log.WriteLine("Found NGen image, but we are not on the Collection machine");
                    symReader.m_log.WriteLine("Skipping NGen PDB creation for {0}", moduleFile.FilePath);
                }
            }

            if (pdbFileName == null)
            {
                if (moduleFile.PdbSignature != Guid.Empty)
                {
                    pdbFileName = symReader.FindSymbolFilePath(moduleFile.PdbName, moduleFile.PdbSignature, moduleFile.PdbAge, moduleFile.FilePath, moduleFile.FileVersion);

                    // We have cases where people rename symbol files from the name at build time to match the DLL name see if this is the case
                    // THis is not that important of a case and can be ripped out.  
                    if (pdbFileName == null)
                    {
                        var simplePdbName = Path.GetFileNameWithoutExtension(moduleFile.PdbName);
                        var simpleDllName = Path.GetFileNameWithoutExtension(moduleFile.FilePath);
                        if (string.Compare(simplePdbName, simpleDllName, StringComparison.OrdinalIgnoreCase) != 0)
                        {
                            simplePdbName = simpleDllName + ".pdb";
                            symReader.m_log.WriteLine("The DLL name at build time differs from run time, trying a DLL based PDB name {0}", simplePdbName);
                            pdbFileName = symReader.FindSymbolFilePath(simplePdbName, moduleFile.PdbSignature, moduleFile.PdbAge, moduleFile.FilePath, moduleFile.FileVersion);
                        }
                    }
                }
                else
                {
                    // TODO FIX NOW, is it OK that I simply remove this?
                    // if (moduleFile.ImageBase == 0)
                    //     throw new ApplicationException("No image load location for module " + moduleFile.FileName + " (managed code?).  Can't load syms.");

                    // See if we are viewing on the machine where we collected the data.  If we can still be able to find the PDB.
                    if (!log.CurrentMachineIsCollectionMachine())
                    {
                        if (UnsafePDBMatching)
                        {
                            symReader.m_log.WriteLine("WARNING: The log file does not contain exact PDB signature information for {0}",
                                moduleFile.FilePath);
                            symReader.m_log.WriteLine("and the collection machine != current machine.");
                            symReader.m_log.WriteLine("The symbols for {0} may be wildly incorrect.", moduleFile.FilePath);
                            symReader.m_log.WriteLine("Did you merge the ETL file before transfering it off the collection machine?");

                            // TODO maybe we should rip this Unsafe PDB stuff out? 
                            var pdbSimpleName = Path.GetFileNameWithoutExtension(moduleFile.FilePath) + ".pdb";
                            pdbFileName = symReader.FindSymbolFilePath(pdbSimpleName, Guid.Empty, 0);
                        }
                        else
                        {
                            if (log.PointerSize == 8 && moduleFile.FilePath.IndexOf(@"\windows\System32", StringComparison.OrdinalIgnoreCase) >= 0)
                                symReader.m_log.WriteLine(
                                    "Warning: could not find PDB signature of a 64 bit OS DLL.  Did you collect with a 32 bit version of XPERF?");
                            throw new ApplicationException(
                                "Trace does not contain PDB signature and resolution is not on the collection machine.\r\n" +
                                "You must use the merge command when generating the ETL file to avoid this problem.\r\n" +
                                "The /UnsafePDBMatch will force PDB lookup without a signature, but can result in incorrect symbols\r\n");
                        }
                    }
                    else
                    {
                        symReader.m_log.WriteLine("No PDB signature for {0} in trace.", moduleFile.FilePath);
                        symReader.m_log.WriteLine("Data collected on current machine, looking up PDB by EXE");
                        pdbFileName = symReader.FindSymbolFilePathForModule(moduleFile.FilePath);
                    }
                }
            }
            if (pdbFileName == null)
                return null;

            // At this point pdbFileName is set!
            m_symbolReaderModule = symReader.OpenSymbolFile(pdbFileName);
            if (m_symbolReaderModule != null)
            {
                m_symbolReaderModule.ExePath = moduleFile.FilePath;

                // Currently NGEN pdbs do not have source server information, but the managed version does.
                // Thus we remember the lookup info for the managed PDB too so we have it if we need source server info 
                var managed = moduleFile.ManagedModule;
                if (managed != null)
                {
                    m_symbolReaderModule.LogManagedInfo(managed.PdbName, managed.PdbSignature, managed.pdbAge);
                }
            }

            symReader.m_log.WriteLine("Opened Pdb file {0}", pdbFileName);
            // TODO FIX NOW turn on cache
            // m_symbolReader = reader;
            // m_moduleFileForSymbolReaderModule = moduleFile;
            return m_symbolReaderModule;
        }

        public static string GenerateNGenPdb(SymbolReader symReader, string ngenImagePath)
        {
            var log = symReader.m_log;
            if (!File.Exists(ngenImagePath))
            {
                log.WriteLine("Warning, NGEN image does not exist: {0}", ngenImagePath);
                return null;
            }


            // When V4.5 shipped, NGEN CreatePdb did not support looking up the IL pdb using symbol servers.  
            // We work around by explicitly fetching the IL PDB and pointing NGEN CreatePdb at that.  
            string ilPdbName = null;
            Guid ilPdbGuid = Guid.Empty;
            int ilPdbAge = 0;

            string pdbName;
            Guid pdbGuid;
            int pdbAge;
            using (var peFile = new PEFile.PEFile(ngenImagePath))
            {
                if (!peFile.GetPdbSignature(out pdbName, out pdbGuid, out pdbAge, true))
                {
                    log.WriteLine("Could not get PDB signature for {0}", ngenImagePath);
                    return null;
                }

                // Also get the IL pdb information
                peFile.GetPdbSignature(out ilPdbName, out ilPdbGuid, out ilPdbAge, false);
            }

            // Fast path, the file already exists.
            pdbName = Path.GetFileName(pdbName);
            var relPath = pdbName + "\\" + pdbGuid.ToString("N") + pdbAge.ToString() + "\\" + pdbName;
            var pdbPath = Path.Combine(symReader.SymbolCacheDirectory, relPath);
            if (File.Exists(pdbPath))
                return pdbPath;

            var clrDir = GetClrDirectoryForNGenImage(ngenImagePath, log);
            if (clrDir == null)
                return null;

            // See if this is a V4.5 CLR, if so we can do line numbers too.l  
            var lineNumberArg = "";
            var ngenexe = Path.Combine(clrDir, "ngen.exe");
            log.WriteLine("Checking for V4.5 for NGEN image {0}", ngenexe);
            if (!File.Exists(ngenexe))
                return null;
            var isV4_5Runtime = false;

            Match m;
            using (var peFile = new PEFile.PEFile(ngenexe))
            {
                var fileVersionInfo = peFile.GetFileVersionInfo();
                if (fileVersionInfo != null)
                {
                    var clrFileVersion = fileVersionInfo.FileVersion;
                    m = Regex.Match(clrFileVersion, @"^[\d.]+\.(\d+) ");       // Fetch the build number (last number)
                    if (m.Success)
                    {
                        // Is this a V4.5 runtime?
                        var buildNumber = int.Parse(m.Groups[1].Value);
                        log.WriteLine("Got NGEN.exe Build number: {0}", buildNumber);
                        if (buildNumber > 16000)
                        {
                            if (ilPdbName != null)
                            {
                                var ilPdbPath = symReader.FindSymbolFilePath(ilPdbName, ilPdbGuid, ilPdbAge);
                                if (ilPdbPath != null)
                                    lineNumberArg = "/lines " + Command.Quote(Path.GetDirectoryName(ilPdbPath));
                                else
                                    log.WriteLine("Could not find IL PDB {0} Guid {1} Age {2}.", ilPdbName, ilPdbGuid, ilPdbAge);
                            }
                            else
                                log.WriteLine("NGEN image did not have IL PDB information, giving up on line number info.");
                            isV4_5Runtime = true;
                        }
                    }
                }
            }

            var options = new CommandOptions();
            options.AddEnvironmentVariable("COMPLUS_NGenEnableCreatePdb", "1");

            // NGenLocalWorker is needed for V4.0 runtims but interferes on V4.5 runtimes.  
            if (!isV4_5Runtime)
                options.AddEnvironmentVariable("COMPLUS_NGenLocalWorker", "1");
            options.AddEnvironmentVariable("_NT_SYMBOL_PATH", symReader.SymbolPath);
            var newPath = "%PATH%;" + clrDir;
            options.AddEnvironmentVariable("PATH", newPath);
            options.AddOutputStream(log);
            options.AddNoThrow();

            // For Windows Store apps Auto-NGEN images we need to use a location where the app can write the PDB file (ELFIX)
            var outputDirectory = symReader.SymbolCacheDirectory;
            var outputPdbPath = pdbPath;

            // Find the tempDir where we can write.  
            string tempDir = null;
            m = Regex.Match(ngenImagePath, @"(.*)\\Microsoft\\CLR_v(\d+)\.\d+(_(\d\d))?\\NativeImages", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                tempDir = Path.Combine(m.Groups[1].Value, @"Temp\NGenPdb");
                DirectoryUtilities.Clean(tempDir);
                Directory.CreateDirectory(tempDir);
                outputDirectory = tempDir;
                outputPdbPath = Path.Combine(tempDir, relPath);
                log.WriteLine("Updating NGEN createPdb output file to {0}", outputPdbPath); // TODO FIX NOW REMOVE (for debugging)
            }
            try
            {

                for (; ; ) // Loop for retrying without /lines 
                {
                    // TODO FIX NOW: there is a and ugly problem with persistance of suboptimial PDB files
                    // This is made pretty bad because the not finding the IL pdbs is enough to make it fail.  

                    // TODO we need to figure out a convention show we know that we have fallen back to no-lines
                    // and we should regenerate it if we ultimately get the PDB information 
                    var cmdLine = string.Format(@"{0}\ngen.exe createpdb {1} {2} {3}",
                        clrDir, Command.Quote(ngenImagePath), Command.Quote(outputDirectory), lineNumberArg);
                    // TODO FIX NOW REMOVE after V4.5 is out a while
                    log.WriteLine("set COMPLUS_NGenEnableCreatePdb=1");
                    if (!isV4_5Runtime)
                        log.WriteLine("set COMPLUS_NGenLocalWorker=1");
                    log.WriteLine("set PATH=" + newPath);
                    log.WriteLine("set _NT_SYMBOL_PATH={0}", symReader.SymbolPath);
                    log.WriteLine("*** NGEN  CREATEPDB cmdline: {0}\r\n", cmdLine);
                    options.AddOutputStream(log);
                    var cmd = Command.Run(cmdLine, options);
                    log.WriteLine("*** NGEN CREATEPDB returns: {0}", cmd.ExitCode);

                    if (cmd.ExitCode != 0)
                    {
                        // ngen might make a bad PDB, so if it returns failure delete it.  
                        if (File.Exists(outputPdbPath))
                            File.Delete(outputPdbPath);

                        // We may have failed because we could not get the PDB.  
                        if (lineNumberArg.Length != 0)
                        {
                            log.WriteLine("Ngen failed to generate pdb for {0}, trying again without /lines", ngenImagePath);
                            lineNumberArg = "";
                            continue;
                        }
                    }

                    if (cmd.ExitCode != 0 || !File.Exists(outputPdbPath))
                    {
                        log.WriteLine("ngen failed to generate pdb for {0} at expected location {1}", ngenImagePath, outputPdbPath);
                        return null;
                    }

                    // Copy the file to where we want the PDB to live.  
                    if (outputPdbPath != pdbPath)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(pdbPath));        // Make sure the destination directory exists.
                        File.Copy(outputPdbPath, pdbPath);
                    }
                    return pdbPath;
                }
            }
            finally
            {
                // Insure we have cleaned up any temporary files.  
                if (tempDir != null)
                    DirectoryUtilities.Clean(tempDir);
            }
        }

        /// <summary>
        /// Deduce the path to where CLR.dll (and in particular NGEN.exe live for the NGEN image 'ngenImagepath')
        /// Returns null if it can't be found
        /// </summary>
        private static string GetClrDirectoryForNGenImage(string ngenImagePath, TextWriter log)
        {
            string majorVersion;
            // Set the default bitness
            string bitness = "";
            var m = Regex.Match(ngenImagePath, @"^(.*)\\assembly\\NativeImages_(v(\d+)[\dA-Za-z.]*)_(\d\d)\\", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var basePath = m.Groups[1].Value;
                var version = m.Groups[2].Value;
                majorVersion = m.Groups[3].Value;
                bitness = m.Groups[4].Value;

                // See if this NGEN image was in a NIC associated with a private runtime.  
                if (basePath.EndsWith(version))
                {
                    if (Directory.Exists(basePath))
                        return basePath;
                }
            }
            else
            {
                m = Regex.Match(ngenImagePath, @"\\Microsoft\\CLR_v(\d+)\.\d+(_(\d\d))?\\NativeImages", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    majorVersion = m.Groups[1].Value;
                    bitness = m.Groups[3].Value;
                }
                else
                {
                    log.WriteLine("Warning: Could not deduce CLR version from path of NGEN image, skipping {0}", ngenImagePath);
                    return null;
                }
            }

            // Only version 4.0 of the runtime has NGEN PDB support 
            if (int.Parse(majorVersion) < 4)
            {
                log.WriteLine("Pre V4.0 native image, skipping: {0}", ngenImagePath);
                return null;
            }

            var winDir = Environment.GetEnvironmentVariable("winDir");

            // If not set, 64 bit OS means we default to 64 bit.  
            if (bitness == "" && Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") != null)
                bitness = "64";

            if (bitness != "64")
                bitness = "";

            var frameworkDir = Path.Combine(winDir, @"Microsoft.NET\Framework" + bitness);
            var candidates = Directory.GetDirectories(frameworkDir, "v" + majorVersion + ".*");
            if (candidates.Length != 1)
            {
                log.WriteLine("Warning: Could not find Version {0} of the .NET Framework in {1}", majorVersion, frameworkDir);
                return null;
            }
            return candidates[0];
        }

        void IFastSerializable.ToStream(Serializer serializer)
        {
            lazyCodeAddresses.Write(serializer, delegate
            {
                serializer.Write(log);
                serializer.Write(moduleFiles);
                serializer.Write(methods);

                serializer.Write(codeAddresses.Count);
                serializer.Log("<WriteColection name=\"codeAddresses\" count=\"" + codeAddresses.Count + "\">\r\n");
                for (int i = 0; i < codeAddresses.Count; i++)
                {
                    serializer.WriteAddress(codeAddresses[i].Address);
                    serializer.Write((int)codeAddresses[i].ModuleFileIndex);
                    serializer.Write((int)codeAddresses[i].methodOrProcessOrIlMapIndex);
                    serializer.Write(codeAddresses[i].InclusiveCount);
                }
                serializer.Write(totalCodeAddresses);
                serializer.Log("</WriteColection>\r\n");

                serializer.Write(ILToNativeMaps.Count);
                for (int i = 0; i < ILToNativeMaps.Count; i++)
                    serializer.Write(ILToNativeMaps[i]);
            });
        }

        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            lazyCodeAddresses.Read(deserializer, delegate
            {
                deserializer.Read(out log);
                deserializer.Read(out moduleFiles);
                deserializer.Read(out methods);

                int count = deserializer.ReadInt();
                deserializer.Log("<Marker name=\"codeAddresses\" count=\"" + count + "\"/>");
                CodeAddressInfo codeAddressInfo = new CodeAddressInfo();
                codeAddresses = new GrowableArray<CodeAddressInfo>(count + 1);
                for (int i = 0; i < count; i++)
                {
                    deserializer.ReadAddress(out codeAddressInfo.Address);
                    codeAddressInfo.ModuleFileIndex = (ModuleFileIndex)deserializer.ReadInt();
                    codeAddressInfo.methodOrProcessOrIlMapIndex = deserializer.ReadInt();
                    deserializer.Read(out codeAddressInfo.InclusiveCount);
                    codeAddresses.Add(codeAddressInfo);
                }
                deserializer.Read(out totalCodeAddresses);

                ILToNativeMaps.Count = deserializer.ReadInt();
                for (int i = 0; i < ILToNativeMaps.Count; i++)
                    deserializer.Read(out ILToNativeMaps.UnderlyingArray[i]);
            });
            lazyCodeAddresses.FinishRead();        // TODO REMOVE 
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException(); // GetEnumerator
        }

        internal struct CodeAddressInfo
        {
            internal CodeAddressInfo(Address address, ProcessIndex processIndex)
            {
                this.Address = address;
                this.ModuleFileIndex = ModuleFileIndex.Invalid;
                this.methodOrProcessOrIlMapIndex = -2 - ((int)processIndex);      // Encode process index to make it unambiguous with a method index.
                this.InclusiveCount = 0;
                Debug.Assert(GetProcessIndex(null) == processIndex);
            }
            internal Address Address;
            internal int InclusiveCount;

            internal ILMapIndex GetILMapIndex(TraceCodeAddresses codeAddresses)
            {
                if (methodOrProcessOrIlMapIndex < 0 || (methodOrProcessOrIlMapIndex & 1) == 0)
                    return ILMapIndex.Invalid;
                return (ILMapIndex)(methodOrProcessOrIlMapIndex >> 1);
            }
            internal void SetILMapIndex(TraceCodeAddresses codeAddresses, ILMapIndex value)
            {
                Debug.Assert(value != ILMapIndex.Invalid);

                // We may be overwriting other values, insure that they actually don't change.  
                Debug.Assert(GetMethodIndex(codeAddresses) == Diagnostics.Tracing.MethodIndex.Invalid ||
                    GetMethodIndex(codeAddresses) == codeAddresses.ILToNativeMaps[(int)value].MethodIndex);
                Debug.Assert(methodOrProcessOrIlMapIndex >= 0 ||
                    GetProcessIndex(codeAddresses) == codeAddresses.ILToNativeMaps[(int)value].ProcessIndex);

                methodOrProcessOrIlMapIndex = ((int)value << 1) + 1;

                Debug.Assert(GetILMapIndex(codeAddresses) == value);
            }


            /// <summary>
            /// This is only valid until MethodIndex or ModuleFileIndex is set.   
            /// </summary>
            internal ProcessIndex GetProcessIndex(TraceCodeAddresses codeAddresses)
            {
                if (methodOrProcessOrIlMapIndex < -1)
                    return (Diagnostics.Tracing.ProcessIndex)(-(methodOrProcessOrIlMapIndex + 2));

                var ilMapIdx = GetILMapIndex(codeAddresses);
                if (ilMapIdx != ILMapIndex.Invalid)
                    return codeAddresses.ILToNativeMaps[(int)ilMapIdx].ProcessIndex;
                // Can't assert because we get here if we have NGEN rundown on an NGEN image 
                // Debug.Assert(false, "Asking for Process after Method has been set is illegal (to save space)");
                return Diagnostics.Tracing.ProcessIndex.Invalid;
            }
            /// <summary>
            /// Only for managed code.  
            /// </summary>
            internal MethodIndex GetMethodIndex(TraceCodeAddresses codeAddresses)
            {
                if (methodOrProcessOrIlMapIndex < 0)
                    return Diagnostics.Tracing.MethodIndex.Invalid;

                if ((methodOrProcessOrIlMapIndex & 1) == 0)
                    return (Diagnostics.Tracing.MethodIndex)(methodOrProcessOrIlMapIndex >> 1);

                return codeAddresses.ILToNativeMaps[(int)GetILMapIndex(codeAddresses)].MethodIndex;
            }
            internal void SetMethodIndex(TraceCodeAddresses codeAddresses, MethodIndex value)
            {
                Debug.Assert(value != Diagnostics.Tracing.MethodIndex.Invalid);

                if (GetILMapIndex(codeAddresses) == TraceCodeAddresses.ILMapIndex.Invalid)
                    methodOrProcessOrIlMapIndex = (int)(value) << 1;
                else
                    Debug.Assert(GetMethodIndex(codeAddresses) == value, "Setting method index when ILMap already set (ignored)");

                Debug.Assert(GetMethodIndex(codeAddresses) == value);
            }
            /// <summary>
            /// Only for unmanaged code.  
            /// </summary>
            internal ModuleFileIndex ModuleFileIndex;

            // keep track of how populare each code stack is. 
            internal void UpdateStats()
            {
                InclusiveCount++;
            }

            // To save space, we reuse this slot during data collection 
            // If x < -1 it is ProcessIndex, if > -1 and odd, it is an ILMapIndex if > -1 and even it is a MethodIndex.  
            internal int methodOrProcessOrIlMapIndex;
        }

        private ILMapIndex UnloadILMapForMethod(MethodIndex methodIndex, MethodLoadUnloadVerboseTraceData data)
        {
            var process = log.Processes.GetProcess(data.ProcessID, data.TimeStamp100ns);
            if (process == null)
                return ILMapIndex.Invalid;

            ILMapIndex ilMapIdx;
            var ilMap = FindAndRemove(data.MethodID, process.ProcessIndex, out ilMapIdx);
            if (ilMap == null)
                return ilMapIdx;

            Debug.Assert(ilMap.MethodStart == 0 || ilMap.MethodStart == data.MethodStartAddress);
            Debug.Assert(ilMap.MethodLength == 0 || ilMap.MethodLength == data.MethodSize);

            ilMap.MethodStart = data.MethodStartAddress;
            ilMap.MethodLength = data.MethodSize;
            Debug.Assert(ilMap.MethodIndex == 0 || ilMap.MethodIndex == methodIndex);
            ilMap.MethodIndex = methodIndex;
            return ilMapIdx;
        }

        /// <summary>
        /// Find the ILToNativeMap for 'methodId' in process associated with 'processIndex' 
        /// and then remove it from the table (this is what you want to do when the method is unloaded)
        /// </summary>
        private ILToNativeMap FindAndRemove(long methodID, ProcessIndex processIndex, out ILMapIndex mapIdxRet)
        {
            ILMapIndex mapIdx;
            if (methodIDToILToNativeMap != null && methodIDToILToNativeMap.TryGetValue(methodID, out mapIdx))
            {
                ILToNativeMap prev = null;
                while (mapIdx != ILMapIndex.Invalid)
                {
                    ILToNativeMap ret = ILToNativeMaps[(int)mapIdx];
                    if (ret.ProcessIndex == processIndex)
                    {
                        if (prev != null)
                            prev.Next = ret.Next;
                        else if (ret.Next == ILMapIndex.Invalid)
                            methodIDToILToNativeMap.Remove(methodID);
                        else
                            methodIDToILToNativeMap[methodID] = ret.Next;
                        mapIdxRet = mapIdx;
                        return ret;
                    }
                    mapIdx = ret.Next;
                }
            }
            mapIdxRet = ILMapIndex.Invalid;
            return null;
        }

        internal void AddILMapping(MethodILToNativeMapTraceData data)
        {
            var ilMap = new ILToNativeMap();
            ilMap.Next = ILMapIndex.Invalid;
            var process = log.Processes.GetProcess(data.ProcessID, data.TimeStamp100ns);
            if (process == null)
                return;

            ilMap.ProcessIndex = process.ProcessIndex;
            ILToNativeMapTuple tuple;
            for (int i = 0; i < data.CountOfMapEntries; i++)
            {
                // There are sepcial prolog and epilog offsets, but the best line approximation 
                // happens if we simply ignore them, so this is what we do here.  
                var ilOffset = data.ILOffset(i);
                if (ilOffset < 0)
                    continue;
                tuple.ILOffset = ilOffset;
                tuple.NativeOffset = data.NativeOffset(i);
                ilMap.Map.Add(tuple);
            }

            // They may not come back sorted, but we want to binary search so sort them by native offset (assending)
            ilMap.Map.Sort((x, y) => x.NativeOffset - y.NativeOffset);

            ILMapIndex mapIdx = (ILMapIndex)ILToNativeMaps.Count;
            ILToNativeMaps.Add(ilMap);
            if (methodIDToILToNativeMap == null)
                methodIDToILToNativeMap = new Dictionary<long, ILMapIndex>(101);
            ILMapIndex prevIndex;
            if (methodIDToILToNativeMap.TryGetValue(data.MethodID, out prevIndex))
                ilMap.Next = prevIndex;
            methodIDToILToNativeMap[data.MethodID] = mapIdx;
        }

        internal enum ILMapIndex { Invalid = -1 };
        internal struct ILToNativeMapTuple
        {
            public int ILOffset;
            public int NativeOffset;

            internal void Deserialize(Deserializer deserializer)
            {
                deserializer.Read(out ILOffset);
                deserializer.Read(out NativeOffset);
            }
            internal void Serialize(Serializer serializer)
            {
                serializer.Write(ILOffset);
                serializer.Write(NativeOffset);
            }
        }

        internal class ILToNativeMap : IFastSerializable
        {
            public ILMapIndex Next;             // We keep a link list of maps with the same start address 
            // (can only be from different processes);
            public ProcessIndex ProcessIndex;   // This is not serialized.  
            public MethodIndex MethodIndex;
            public Address MethodStart;
            public int MethodLength;
            internal GrowableArray<ILToNativeMapTuple> Map;

            public int GetILOffsetForNativeAddress(Address nativeAddress)
            {
                int idx;
                if (nativeAddress < MethodStart || MethodStart + (uint)MethodLength < nativeAddress)
                    return -1;
                int nativeOffset = (int)(nativeAddress - MethodStart);
                Map.BinarySearch(nativeOffset, out idx,
                    delegate(int key, ILToNativeMapTuple elem) { return key - elem.NativeOffset; });
                if (idx < 0)
                    return -1;

                // After looking at the empirical results, it does seem that linear interpolation 
                // Gives a significantly better approximation of the IL address.  
                int retIL = Map[idx].ILOffset;
                int nativeDelta = nativeOffset - Map[idx].NativeOffset;
                int nextIdx = idx + 1;
                if (nextIdx < Map.Count && nativeDelta != 0)
                {
                    int ILDeltaToNext = Map[nextIdx].ILOffset - Map[idx].ILOffset;
                    // If the IL deltas are going down don't interpolate.  
                    if (ILDeltaToNext > 0)
                    {
                        int nativeDeltaToNext = Map[nextIdx].NativeOffset - Map[idx].NativeOffset;
                        retIL += (int)(((double)nativeDelta) / nativeDeltaToNext * ILDeltaToNext + .5);
                    }
                    else
                        return retIL;
                }
                // For our use in sampling the EIP is the instruction that COMPLETED, so we actually want to
                // attribute the time to the line BEFORE this one if we are exactly on the boundary.  
                // TODO This probably does not belong here, but I only want to this if the IL deltas are going up.  
                if (retIL > 0)
                    --retIL;
                return retIL;
            }

            void IFastSerializable.ToStream(Serializer serializer)
            {
                serializer.Write((int)MethodIndex);
                serializer.Write((long)MethodStart);
                serializer.Write(MethodLength);

                serializer.Write(Map.Count);
                for (int i = 0; i < Map.Count; i++)
                    Map[i].Serialize(serializer);
            }
            void IFastSerializable.FromStream(Deserializer deserializer)
            {
                MethodIndex = (MethodIndex)deserializer.ReadInt();
                deserializer.ReadAddress(out MethodStart);
                deserializer.Read(out MethodLength);

                Map.Count = deserializer.ReadInt();
                for (int i = 0; i < Map.Count; i++)
                    Map.UnderlyingArray[i].Deserialize(deserializer);
            }
        }
        GrowableArray<ILToNativeMap> ILToNativeMaps;                    // only Jitted code has these, indexed by ILMapIndex 
        Dictionary<long, ILMapIndex> methodIDToILToNativeMap;

        /// <summary>
        /// Initialially we only have addresses and we need to group them into methods.   We do this by creating 64 byte 'buckets'
        /// see code:bucketSize which allow us to create a hash table.   Once we have grouped all the code addresses together into
        /// the smallest interesting units (e.g. methods), we don't need this table anymore.  
        /// </summary>
        private Dictionary<long, CodeAddressBucketEntry> codeAddressBuckets;

        private TraceCodeAddress[] codeAddressObjects;  // If we were asked for TraceCodeAddresses (instead of indexes) we cache them
        private string[] names;                         // A cache (one per code address) of the string name of the address
        private int managedMethodRecordCount;           // Remembers how many code addresses are managed methods (currently not serialized)
        internal int totalCodeAddresses;                 // Count of the number of times a code address appears in the log.

        // These are actually serialized.  
        private TraceLog log;
        private TraceModuleFiles moduleFiles;
        private TraceMethods methods;
        private DeferedRegion lazyCodeAddresses;
        internal GrowableArray<CodeAddressInfo> codeAddresses;

        #endregion
    }

    /// <summary>
    /// A TraceCodeAddress represents a address of code (where an instruction pointer might point). Unlike a
    /// raw pointer, TraceCodeAddresses will be distinct if they come from different ModuleFiles (thus at
    /// different times (or different processes) different modules were loaded and had the same virtual
    /// address they would NOT have the same TraceCodeAddress because the load file (and thus the symbolic
    /// information) would be different.
    /// 
    /// TraceCodeAddresses hold the symbolic information associated with the address.
    /// 
    /// TraceCodeAddress point at TraceMethod and TraceModuleFile.  None of these types know about the
    /// TraceLoadedModule (whic does keep track of when it gets loaded and unloaded), however they
    /// DO make certain that there is no confusion about the symbolic information (thus the same address
    /// in memory might point at completely different TraceMethod and TraceModuleFile because the
    /// code was unloaded and other code loaded between the two references.  
    /// </summary>
    public class TraceCodeAddress
    {
        public CodeAddressIndex CodeAddressIndex { get { return codeAddressIndex; } }
        public Address Address { get { return codeAddresses.Address(codeAddressIndex); } }

        public string FullMethodName
        {
            get
            {
                MethodIndex methodIndex = codeAddresses.MethodIndex(codeAddressIndex);
                if (methodIndex == MethodIndex.Invalid)
                    return "";
                return codeAddresses.Methods.FullMethodName(methodIndex);
            }
        }
        public TraceMethod Method
        {
            get
            {
                MethodIndex methodIndex = codeAddresses.MethodIndex(codeAddressIndex);
                if (methodIndex == MethodIndex.Invalid)
                    return null;
                else
                    return codeAddresses.Methods[methodIndex];
            }
        }
        /// <summary>
        /// This is only valid for managed methods returns -1 if invalid / unavailable
        /// </summary>
        public int ILOffset { get { return codeAddresses.ILOffset(codeAddressIndex); } }

        public TraceModuleFile ModuleFile
        {
            get
            {
                ModuleFileIndex moduleFileIndex = codeAddresses.ModuleFileIndex(codeAddressIndex);
                if (moduleFileIndex == ModuleFileIndex.Invalid)
                    return null;
                else
                    return codeAddresses.ModuleFiles[moduleFileIndex];
            }
        }
        /// <summary>
        /// ModuleName is the name of the file without path or extension. 
        /// </summary>
        public string ModuleName
        {
            get
            {
                TraceModuleFile moduleFile = ModuleFile;
                if (moduleFile == null)
                    return "";
                return moduleFile.Name;
            }
        }
        public string ModuleFileName
        {
            get
            {
                TraceModuleFile moduleFile = ModuleFile;
                if (moduleFile == null)
                    return "";
                return moduleFile.FilePath;
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            return ToString(sb).ToString();
        }
        public StringBuilder ToString(StringBuilder sb)
        {
            sb.Append("  <CodeAddress Address=\"0x").Append(((long)Address).ToString("x")).Append("\"");
            sb.Append(" CodeAddressIndex=\"").Append(XmlUtilities.XmlEscape(CodeAddressIndex, false)).Append("\"");
            if (FullMethodName.Length > 0)
                sb.Append(" FullMethodName=\"").Append(XmlUtilities.XmlEscape(FullMethodName, false)).Append("\"");
            if (ModuleName.Length != 0)
                sb.Append(" ModuleName=\"").Append(XmlUtilities.XmlEscape(ModuleName, false)).Append("\"");
            sb.Append("/>");
            return sb;
        }
        #region private
        internal TraceCodeAddress(TraceCodeAddresses codeAddresses, CodeAddressIndex codeAddressIndex)
        {
            this.codeAddresses = codeAddresses;
            this.codeAddressIndex = codeAddressIndex;
        }

        TraceCodeAddresses codeAddresses;
        CodeAddressIndex codeAddressIndex;
        #endregion

    }

    public enum ModuleFileIndex { Invalid = -1 };

    /// <summary>
    /// Represents a collection of code:TraceModuleFile
    /// </summary>
    public class TraceModuleFiles : IFastSerializable, IEnumerable<TraceModuleFile>
    {
        /// <summary>
        /// Enumerate all the files that occured in the trace log.  
        /// </summary> 
        IEnumerator<TraceModuleFile> IEnumerable<TraceModuleFile>.GetEnumerator()
        {
            for (int i = 0; i < moduleFiles.Count; i++)
                yield return moduleFiles[i];
        }
        /// <summary>
        /// Each file is given an index for quick lookup.   MaxModuleFileIndex is the
        /// maximum such index (thus you can create an array that is 1-1 with the
        /// files easily).  
        /// </summary>
        public int MaxModuleFileIndex { get { return moduleFiles.Count; } }
        public TraceModuleFile this[ModuleFileIndex moduleFileIndex]
        {
            get
            {
                if (moduleFileIndex == ModuleFileIndex.Invalid)
                    return null;
                return moduleFiles[(int)moduleFileIndex];
            }
        }
        public TraceLog Log { get { return log; } }
        /// <summary>
        /// For a given file name, get the code:TraceModuleFile associated with it.  
        /// </summary>
        public TraceModuleFile GetModuleFile(string fileName, Address imageBase)
        {
            TraceModuleFile moduleFile;
            if (moduleFilesByName == null)
            {
                moduleFilesByName = new Dictionary<string, TraceModuleFile>(Math.Max(256, moduleFiles.Count + 4), StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < moduleFiles.Count; i++)
                {
                    moduleFile = moduleFiles[i];
                    Debug.Assert(moduleFile.next == null);
                    TraceModuleFile collision;
                    if (moduleFilesByName.TryGetValue(moduleFile.FilePath, out collision))
                        moduleFile.next = collision;
                    else
                        moduleFilesByName.Add(moduleFile.FilePath, moduleFile);
                }
            }
            if (moduleFilesByName.TryGetValue(fileName, out moduleFile))
            {
                do
                {
                    // TODO review the imageBase == 0 condition.  Needed to get PDB signature on managed IL.  
                    if (moduleFile.ImageBase == imageBase || imageBase == 0)        // imagebase == 0 is managed case, we allow it to match anything.  
                        return moduleFile;
                    //                    options.ConversionLog.WriteLine("WARNING: " + fileName + " loaded with two base addresses 0x" + moduleImageBase.ToString("x") + " and 0x" + moduleFile.moduleImageBase.ToString("x"));
                    moduleFile = moduleFile.next;
                } while (moduleFile != null);
            }
            return moduleFile;
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<TraceModuleFiles Count=").Append(XmlUtilities.XmlQuote(moduleFiles.Count)).AppendLine(">");
            foreach (TraceModuleFile moduleFile in this)
                sb.Append("  ").Append(moduleFile.ToString()).AppendLine();
            sb.AppendLine("</TraceModuleFiles>");
            return sb.ToString();
        }
        #region private
        internal void SetModuleFileName(TraceModuleFile moduleFile, string fileName)
        {
            Debug.Assert(moduleFile.fileName == null);
            moduleFile.fileName = fileName;
            if (moduleFilesByName != null)
                moduleFilesByName[fileName] = moduleFile;
        }
        /// <summary>
        /// We cache information about a native image load in a code:TraceModuleFile.  Retrieve or create a new
        /// cache entry associated with 'nativePath' and 'moduleImageBase'.  'moduleImageBase' can be 0 for managed assemblies
        /// that were not loaded with LoadLibrary.  
        /// </summary>
        internal TraceModuleFile GetOrCreateModuleFile(string nativePath, Address imageBase)
        {
            TraceModuleFile moduleFile = null;
            if (nativePath != null)
                moduleFile = GetModuleFile(nativePath, imageBase);
            if (moduleFile == null)
            {
                moduleFile = new TraceModuleFile(nativePath, imageBase, (ModuleFileIndex)moduleFiles.Count);
                moduleFiles.Add(moduleFile);
                if (nativePath != null)
                {
                    TraceModuleFile prevValue;
                    if (moduleFilesByName.TryGetValue(nativePath, out prevValue))
                        moduleFile.next = prevValue;
                    moduleFilesByName[nativePath] = moduleFile;
                }
            }

            Debug.Assert(moduleFilesByName == null || moduleFiles.Count >= moduleFilesByName.Count);
            return moduleFile;
        }
        internal TraceModuleFiles(TraceLog log)
        {
            this.log = log;
        }
        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(log);
            serializer.Write(moduleFiles.Count);
            for (int i = 0; i < moduleFiles.Count; i++)
                serializer.Write(moduleFiles[i]);
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            deserializer.Read(out log);
            int count = deserializer.ReadInt();
            moduleFiles = new GrowableArray<TraceModuleFile>(count + 1);
            for (int i = 0; i < count; i++)
            {
                TraceModuleFile elem;
                deserializer.Read(out elem);
                moduleFiles.Add(elem);
            }
            moduleFilesByName = null;
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException(); // GetEnumerator
        }

        private TraceLog log;
        private Dictionary<string, TraceModuleFile> moduleFilesByName;
        private GrowableArray<TraceModuleFile> moduleFiles;
        #endregion
    }

    /// <summary>
    /// The TraceModuleFile represents a executable file that can be loaded into memory (either an EXE or a
    /// DLL).  It only represents the data file as well as the location in memory where it was loaded (or
    /// its ModuleID if it is a managed module), but NOT the load or unload time or the process.  Thus 
    /// it is good for shared symbolic information.    Also note that TraceModuleFiles are NOT guarenteed
    /// to be interned (that is there could be two entries that have the same exact file name).  
    /// </summary>
    public sealed class TraceModuleFile : IFastSerializable
    {
        public ModuleFileIndex ModuleFileIndex { get { return moduleFileIndex; } }
        /// <summary>
        /// The moduleFile name associted with the moduleFile.  May be the empty string if the moduleFile has no moduleFile
        /// (dynamically generated).  For managed code, this is the IL moduleFile name.  
        /// </summary>
        public string FilePath
        {
            get
            {
                if (fileName == null)
                    return "ManagedModule";
                return fileName;
            }
        }
        /// <summary>
        /// This is the short name of the moduleFile (moduleFile name without exention). 
        /// </summary>
        public string Name
        {
            get
            {
                if (name == null)
                {
                    var filePath = FilePath;
                    if (filePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                        filePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        name = Path.GetFileNameWithoutExtension(filePath);
                    else
                        name = Path.GetFileName(filePath);
                }
                return name;
            }
        }
        public Address DefaultBase { get { return defaultBase; } }
        public Address ImageBase { get { return imageBase; } }
        public int ImageSize { get { return imageSize; } }
        public Address ImageEnd { get { return (Address)((ulong)imageBase + (uint)imageSize); } }

        public string PdbName { get { return pdbName; } }
        public Guid PdbSignature { get { return pdbSignature; } }
        public int PdbAge { get { return pdbAge; } }

        public string FileVersion { get { return fileVersion; } }
        public int TimeDateStamp { get { return timeDateStamp; } }
        /// <summary>
        /// The number of code addresses in this module.  This is useful for determining if 
        /// this module is worth having its symbolic information looked up or not.   
        /// 
        /// This number is defined as the number of appearances this module has in any stack 
        /// or any event with a code addresss (If the modules appears 5 times in a stack that
        /// counts as 5 even though it is just one event's stack).  
        /// </summary>
        public int CodeAddressesInModule { get { return codeAddressesInModule; } }
        /// <summary>
        /// If this is an NGEN image, return the TraceModuleFile of the IL image.  
        /// </summary>
        public TraceModuleFile ManagedModule { get { return managedModule; } }

        // If the module file was a managed module, this is the moduleID that the CLR associates with it.  
        public override string ToString()
        {
            return "<TraceModuleFile " +
                    "Name=" + XmlUtilities.XmlQuote(Name) + " " +
                    "ModuleFileIndex=" + XmlUtilities.XmlQuote(ModuleFileIndex) + " " +
                    "ImageSize=" + XmlUtilities.XmlQuoteHex(ImageSize) + " " +
                    "FileName=" + XmlUtilities.XmlQuote(FilePath) + " " +
                    "ImageBase=" + XmlUtilities.XmlQuoteHex((ulong)ImageBase) + " " +
                    "TimeDateStamp=" + XmlUtilities.XmlQuote(TimeDateStamp) + " " +
                    "PdbName=" + XmlUtilities.XmlQuote(PdbName) + " " +
                    "PdbSignature=" + XmlUtilities.XmlQuote(PdbSignature) + " " +
                    "PdbAge=" + XmlUtilities.XmlQuote(PdbAge) + " " +
                    "FileVersion=" + XmlUtilities.XmlQuote(FileVersion) + " " +
                   "/>";
        }

        #region Private
        internal TraceModuleFile(string fileName, Address imageBase, ModuleFileIndex moduleFileIndex)
        {
            this.fileName = fileName;
            this.imageBase = imageBase;
            this.moduleFileIndex = moduleFileIndex;
            this.fileVersion = "";
            this.pdbName = "";
        }

        internal string fileName;
        internal int imageSize;
        internal Address imageBase;
        internal Address defaultBase;
        internal string name;
        private ModuleFileIndex moduleFileIndex;
        internal TraceModuleFile next;          // Chain of modules that have the same path (But different image bases)

        internal string pdbName;
        internal Guid pdbSignature;
        internal int pdbAge;
        internal string fileVersion;
        internal int timeDateStamp;
        internal int codeAddressesInModule;
        internal TraceModuleFile managedModule;

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(fileName);
            serializer.Write(imageSize);
            serializer.WriteAddress(imageBase);
            serializer.WriteAddress(defaultBase);

            serializer.Write(pdbName);
            serializer.Write(pdbSignature);
            serializer.Write(pdbAge);
            serializer.Write(fileVersion);
            serializer.Write(timeDateStamp);
            serializer.Write((int)moduleFileIndex);
            serializer.Write(codeAddressesInModule);
            serializer.Write(managedModule);
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            deserializer.Read(out fileName);
            deserializer.Read(out imageSize);
            deserializer.ReadAddress(out imageBase);
            deserializer.ReadAddress(out defaultBase);

            deserializer.Read(out pdbName);
            deserializer.Read(out pdbSignature);
            deserializer.Read(out pdbAge);
            deserializer.Read(out fileVersion);
            deserializer.Read(out timeDateStamp);
            moduleFileIndex = (ModuleFileIndex)deserializer.ReadInt();
            deserializer.Read(out codeAddressesInModule);
            deserializer.Read(out managedModule);
        }
        #endregion
    }

    /// <summary>
    /// TraceLogOptions control the generation of a TraceLog.  
    /// </summary>
    public class TraceLogOptions
    {
        public TraceLogOptions()
        {
            // These are the default modules to look up symbolically.  
            ShouldResolveSymbols = delegate(string moduleFilePath)
            {
                string moduleName = Path.GetFileNameWithoutExtension(moduleFilePath);
                if (string.Compare(moduleName, "ntdll") == 0 ||
                    string.Compare(moduleName, "kernel32") == 0 ||
                    string.Compare(moduleName, "ntkrnlpa") == 0 ||
                    string.Compare(moduleName, "ntoskrnl") == 0)
                    return true;
                return false;
            };
        }
        public Predicate<string> ShouldResolveSymbols;
        /// <summary>
        /// Resolving symbols from a symbol server can take a long time. If
        /// there is a DLL that always fails, it can be quite anoying because
        /// it will always cause delays, By specifying only local symbols it
        /// will only resolve the symbols if it can do so without delay.
        /// Symbols that have been reviously locally cached from a symbol
        /// server count as local symobls.
        ///    
        /// TODO NOT IMPLEMENTED.
        /// </summary>
        public bool LocalSymbolsOnly;
        /// <summary>
        /// If set, will resolve addresses to line numbers, not just names.  Default is not to have line
        /// numbers.  
        /// </summary>
        public bool SourceLineNumbers;
        /// <summary>
        /// print detailed symbolic information (TODO where?)
        /// </summary>
        public bool SymbolDebug;
        /// <summary>
        /// By default symbols are only resolve if there are stacks assocated with the trace. 
        /// Setting this option forces resolution even if there are no stacks. 
        /// </summary>
        public bool AlwaysResolveSymbols;
        /// <summary>
        /// Writes status to this log.  Useful for debugging symbol issues.
        /// </summary>
        public TextWriter ConversionLog
        {
            get
            {
                if (m_ConversionLog == null)
                {
                    if (ConversionLogName != null)
                        m_ConversionLog = File.CreateText(ConversionLogName);
                    else
                        m_ConversionLog = new StringWriter();

                }
                return m_ConversionLog;
            }
            set
            {
                m_ConversionLog = value;
            }
        }
        public string ConversionLogName;
        /// <summary>
        /// Normally events that are only to convey non-temporal information (like DCStart end Ends, MethodName events, File name evens
        /// are removed from the stream.   However sometimes it is useful to keep these events (typically for deubgging TraceEvent itslef)
        /// </summary>
        public bool KeepAllEvents;
        /// <summary>
        /// Sometimes you collect too much data, and you just want to look at a fraction of it to speed things up
        /// (or to keep file size under control).  This allows that.   10M will produce about 3-4GB 
        /// 1M is a good value to keep things under control.  Note that we still scan the entire original ETL file
        /// because we look for rundown events, however we don't transerfer them to the ETLX file.  
        /// The default is 10M because ETLX has a restriction of 4GB in size.  
        /// </summary>
        public int MaxEventCount;
        /// <summary>
        /// If you have too much data and wish to skip the first part of the trace Set this to the number of msec to skip.
        /// The idea is that you might first look at the first 1M events, then set this to a number near the end of that first
        /// block to get the next block etc.  
        /// </summary>
        public double SkipMSec;
        /// <summary>
        /// If this delegate is non-null, it is called if there are any lost events.  
        /// It is passed the number of lost events.  You can throw if you want to abort.  
        /// </summary>
        public Action<int> OnLostEvents;
        /// <summary>
        /// The directory to read files of the form *.manifest.xml to allow you to read unregistered providers.  
        /// </summary>
        public string ExplicitManifestDir;

        #region private
        private TextWriter m_ConversionLog;
        #endregion
    }

    public static class TraceLogExtensions
    {
        public static TraceProcess Process(this TraceEvent anEvent)
        {
            TraceLog log = anEvent.Source as TraceLog;
            return log.Processes.GetProcess(anEvent.ProcessID, anEvent.TimeStamp100ns);
        }
        public static TraceThread Thread(this TraceEvent anEvent)
        {
            TraceLog log = anEvent.Source as TraceLog;
            return log.Threads.GetThread(anEvent.ThreadID, anEvent.TimeStamp100ns);
        }
        public static TraceLog Log(this TraceEvent anEvent)
        {
            return anEvent.Source as TraceLog;
        }
        public static TraceCallStack CallStack(this TraceEvent anEvent)
        {
            TraceLog log = anEvent.Source as TraceLog;
            return log.GetCallStackForEvent(anEvent);
        }
        public static CallStackIndex CallStackIndex(this TraceEvent anEvent)
        {
            TraceLog log = anEvent.Source as TraceLog;
            if (log == null)
                return Diagnostics.Tracing.CallStackIndex.Invalid;
            return log.GetCallStackIndexForEvent(anEvent);
        }
        public static TraceCallStacks CallStacks(this TraceEvent anEvent)
        {
            TraceLog log = anEvent.Source as TraceLog;
            return log.CallStacks;
        }

        public static string ProgramCounterAddressString(this PageFaultTraceData anEvent)
        {
            TraceCodeAddress codeAddress = anEvent.ProgramCounterAddress();
            if (codeAddress != null)
                return codeAddress.ToString();
            return "";
        }
        public static TraceCodeAddress ProgramCounterAddress(this PageFaultTraceData anEvent)
        {
            TraceLog log = anEvent.Source as TraceLog;
            return log.GetCodeAddressAtEvent(anEvent.ProgramCounter, anEvent);
        }
        public static CodeAddressIndex ProgramCounterAddressIndex(this PageFaultTraceData anEvent)
        {
            TraceLog log = anEvent.Source as TraceLog;
            return log.GetCodeAddressIndexAtEvent(anEvent.ProgramCounter, anEvent);
        }

        public static string IntructionPointerCodeAddressString(this SampledProfileTraceData anEvent)
        {
            TraceCodeAddress codeAddress = anEvent.IntructionPointerCodeAddress();
            if (codeAddress != null)
                return codeAddress.ToString();
            return "";
        }
        // Only really useful when SampleProfile does not have callStacks turned on, since it is in the eventToStack. 
        public static TraceCodeAddress IntructionPointerCodeAddress(this SampledProfileTraceData anEvent)
        {
            TraceLog log = anEvent.Source as TraceLog;
            return log.GetCodeAddressAtEvent(anEvent.InstructionPointer, anEvent);
        }
        public static CodeAddressIndex IntructionPointerCodeAddressIndex(this SampledProfileTraceData anEvent)
        {
            TraceLog log = anEvent.Source as TraceLog;
            return log.GetCodeAddressIndexAtEvent(anEvent.InstructionPointer, anEvent);
        }
        public static TraceLoadedModule ModuleForAddress(this TraceEvent anEvent, Address address)
        {
            return Process(anEvent).LoadedModules.GetModuleContainingAddress(address, anEvent.TimeStamp100ns);
        }
    }

    #region Private Classes


    internal struct JavaScriptSourceKey : IEquatable<JavaScriptSourceKey>
    {
        public JavaScriptSourceKey(long sourceID, Address scriptContextID)
        {
            SourceID = sourceID;
            ScriptContextID = scriptContextID;
        }
        public override bool Equals(object obj)
        {
            throw new NotImplementedException();        // you shoudl not be calling this!
        }
        public override int GetHashCode()
        {
            return (int)SourceID + (int)ScriptContextID;
        }
        public bool Equals(JavaScriptSourceKey other)
        {
            return SourceID == other.SourceID && ScriptContextID == other.ScriptContextID;
        }
        public long SourceID;
        public Address ScriptContextID;
    }

    internal static class SerializerExtentions
    {
        public static void WriteAddress(this Serializer serializer, Address address)
        {
            serializer.Write((long)address);
        }
        public static void ReadAddress(this Deserializer deserializer, out Address address)
        {
            long longAddress;
            deserializer.Read(out longAddress);
            address = (Address)longAddress;
        }
    }

    /// <summary>
    /// Represents a source for an ETLX file.  This is the class returned by the code:TraceEvents.GetSource
    /// methodIndex 
    /// </summary>
    class ETLXTraceEventSource : TraceEventDispatcher
    {
        public override bool Process()
        {
            // This basically a foreach loop, however we cheat and substitute our own dispatcher 
            // to do the lookup.  TODO: is there a better way?
            IEnumerator<TraceEvent> enumerator = ((IEnumerable<TraceEvent>)events).GetEnumerator();
            TraceEvents.EventEnumeratorBase asBase = (TraceEvents.EventEnumeratorBase)enumerator;
            this.currentID = asBase.lookup.currentID;
            events.log.FreeLookup(asBase.lookup);
            asBase.lookup = this;
            try
            {
                while (enumerator.MoveNext())
                {
                    Dispatch(enumerator.Current);
                    if (stopProcessing)
                        return false;
                }
            }
            finally
            {
                events.log.FreeReader(asBase.reader);
            }
            return true;
        }
        public TraceLog Log { get { return events.log; } }

        public unsafe ETLXTraceEventSource Clone()
        {
            ETLXTraceEventSource ret = new ETLXTraceEventSource(events);
            foreach (TraceEvent template in Templates)
            {
                // TODO: it would be better if we cleaned up this potentially dangling pointer
                template.eventRecord = null;
                template.userData = IntPtr.Zero;
                ((ITraceParserServices)ret).RegisterEventTemplate(template.Clone());
            }
            return ret;
        }
        public override void Dispose()
        {
        }
        #region private
        protected internal override string ProcessName(int processID, long time100ns)
        {
            return Log.ProcessName(processID, time100ns);
        }
        protected override void RegisterEventTemplateImpl(TraceEvent template)
        {
            template.source = Log;
            base.RegisterEventTemplateImpl(template);
        }

        internal override unsafe Guid GetRelatedActivityID(TraceEventNativeMethods.EVENT_RECORD* eventRecord)
        {
            return Log.GetRelatedActivityID(eventRecord);
        }

        internal ETLXTraceEventSource(TraceEvents events)
        {
            this.events = events;
            this.unhandledEventTemplate.source = Log;
            this.userData = Log.UserData;
        }

        TraceEvents events;
        #endregion
    }

    #endregion
}
