//     Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using FastSerialization;
using System.Security;
using Diagnostics.Tracing;
using Address = System.UInt64;

/* This file was generated with the command */
// traceParserGen /merge CLREtwAll.man CLRTraceEventParser.cs
/* And then modified by hand to add functionality (handle to name lookup, fixup of evenMethodLoadUnloadTraceDatats ...) */
// The version before any hand modifications is kept as KernelTraceEventParser.base.cs, and a 3
// way diff is done when traceParserGen is rerun.  This allows the 'by-hand' modifications to be
// applied again if the mof or the traceParserGen transformation changes. 
// 
// See traceParserGen /usersGuide for more on the /merge option 
namespace Diagnostics.Tracing.Parsers
{

    /* Parsers defined in this file */
    // code:ClrTraceEventParser, code:ClrRundownTraceEventParser, code:ClrStressTraceEventParser 
    /* code:ClrPrivateTraceEventParser  code:#ClrPrivateProvider */
    // [SecuritySafeCritical]
    public class ClrTraceEventParser : TraceEventParser
    {
        public static string ProviderName = "Microsoft-Windows-DotNETRuntime";
        public static Guid ProviderGuid = new Guid(unchecked((int)0xe13c0d23), unchecked((short)0xccbc), unchecked((short)0x4e12), 0x93, 0x1b, 0xd9, 0xcc, 0x2e, 0xee, 0x27, 0xe4);
        /// <summary>
        ///  Keywords are passed to code:TraceEventSession.EnableProvider to enable particular sets of
        /// </summary>
        [Flags]
        public enum Keywords : long
        {
            None = 0,
            All = ~StartEnumeration,        // All does not include start-enumeration.  It just is not that useful.  
            /// <summary>
            /// Logging when garbage collections and finalization happen. 
            /// </summary>
            GC = 0x1,
            Binder = 0x4,
            /// <summary>
            /// Logging when modules actually get loaded and unloaded. 
            /// </summary>
            Loader = 0x8,
            /// <summary>
            /// Logging when Just in time (JIT) compilation occurs. 
            /// </summary>
            Jit = 0x10,
            /// <summary>
            /// Logging when precompiled native (NGEN) images are loaded.
            /// </summary>
            NGen = 0x20,
            /// <summary>
            /// Indicates that on attach or module load , a rundown of all existing methods should be done
            /// </summary>
            StartEnumeration = 0x40,
            /// <summary>
            /// Indicates that on detach or process shutdown, a rundown of all existing methods should be done
            /// </summary>
            StopEnumeration = 0x80,
            /// <summary>
            /// Events associted with validating security restrictions.
            /// </summary>
            Security = 0x400,
            /// <summary>
            /// Events for logging resource consumption on an app-domain level granularity
            /// </summary>
            AppDomainResourceManagement = 0x800,
            /// <summary>
            /// Logging of the internal workings of the Just In Time compiler.  This is fairly verbose.  
            /// It details decidions about interesting optimization (like inlining and tail call) 
            /// </summary>
            JitTracing = 0x1000,
            /// <summary>
            /// Log information about code thunks that transition between managed and unmanaged code. 
            /// </summary>
            Interop = 0x2000,
            /// <summary>
            /// Log when lock conentions occurs.  (Monitor.Enters actually blocks)
            /// </summary>
            Contention = 0x4000,
            /// <summary>
            /// Log exception processing.  
            /// </summary>
            Exception = 0x8000,
            /// <summary>
            /// Log events associated with the threadpool, and other threading events.  
            /// </summary>
            Threading = 0x10000,
            /// <summary>
            /// Dump the native to IL mapping of any method that is JIT compiled.  (V4.5 runtimes and above).  
            /// </summary>
            JittedMethodILToNativeMap = 0x20000,
            /// <summary>
            /// This supresses NGEN events on V4.0 (where you have NGEN PDBs), but not on V2.0 (which does not know about this 
            /// bit and also does not have NGEN PDBS).  
            /// </summary>
            SupressNGen = 0x40000,
            /// <summary>
            /// TODO document
            /// </summary>
            PerfTrack = 0x20000000,
            /// <summary>
            /// Also log the stack trace of events for which this is valuable.
            /// </summary>
            Stack = 0x40000000,
            /// <summary>
            /// Recommend default flags (good compromise on verbosity).  
            /// </summary>
            Default = GC | Binder | Loader | Jit | NGen | SupressNGen | StopEnumeration | Security | AppDomainResourceManagement | Exception | Threading | Contention | Stack | JittedMethodILToNativeMap,
        };
        public ClrTraceEventParser(TraceEventSource source) : base(source) { }

        public event Action<GCStartTraceData> GCStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new GCStartTraceData(value, 1, 1, "GC", GCTaskGuid, 1, "Start", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<GCEndTraceData> GCStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new GCEndTraceData(value, 2, 1, "GC", GCTaskGuid, 2, "Stop", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<GCNoUserDataTraceData> GCRestartEEStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new GCNoUserDataTraceData(value, 3, 1, "GC", GCTaskGuid, 132, "RestartEEStop", ProviderGuid, ProviderName));
                // Added for V2 Runtime compatibilty (Classic ETW only)
                source.RegisterEventTemplate(new GCNoUserDataTraceData(value, 0xFFFF, 1, "GC", GCTaskGuid, 8, "RestartEEStop", Guid.Empty, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<GCHeapStatsTraceData> GCHeapStats
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new GCHeapStatsTraceData(value, 4, 1, "GC", GCTaskGuid, 133, "HeapStats", ProviderGuid, ProviderName));
                // Added for V2 Runtime compatibilty (Classic ETW only)
                source.RegisterEventTemplate(new GCHeapStatsTraceData(value, 0xFFFF, 1, "GC", GCTaskGuid, 5, "HeapStats", Guid.Empty, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<GCCreateSegmentTraceData> GCCreateSegment
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new GCCreateSegmentTraceData(value, 5, 1, "GC", GCTaskGuid, 134, "CreateSegment", ProviderGuid, ProviderName));
                // Added for V2 Runtime compatibilty (Classic ETW only)
                source.RegisterEventTemplate(new GCCreateSegmentTraceData(value, 0xFFFF, 1, "GC", GCTaskGuid, 6, "CreateSegment", Guid.Empty, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<GCFreeSegmentTraceData> GCFreeSegment
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new GCFreeSegmentTraceData(value, 6, 1, "GC", GCTaskGuid, 135, "FreeSegment", ProviderGuid, ProviderName));
                // Added for V2 Runtime compatibilty (Classic ETW only)
                source.RegisterEventTemplate(new GCFreeSegmentTraceData(value, 0xFFFF, 1, "GC", GCTaskGuid, 7, "FreeSegment", Guid.Empty, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<GCNoUserDataTraceData> GCRestartEEStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new GCNoUserDataTraceData(value, 7, 1, "GC", GCTaskGuid, 136, "RestartEEStart", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<GCNoUserDataTraceData> GCSuspendEEStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new GCNoUserDataTraceData(value, 8, 1, "GC", GCTaskGuid, 137, "SuspendEEStop", ProviderGuid, ProviderName));
             }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<GCSuspendEETraceData> GCSuspendEEStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new GCSuspendEETraceData(value, 9, 1, "GC", GCTaskGuid, 10, "SuspendEEStart", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<GCAllocationTickTraceData> GCAllocationTick
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new GCAllocationTickTraceData(value, 10, 1, "GC", GCTaskGuid, 11, "AllocationTick", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<GCCreateConcurrentThreadTraceData> GCCreateConcurrentThread
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new GCCreateConcurrentThreadTraceData(value, 11, 1, "GC", GCTaskGuid, 12, "CreateConcurrentThread", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<GCTerminateConcurrentThreadTraceData> GCTerminateConcurrentThread
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new GCTerminateConcurrentThreadTraceData(value, 12, 1, "GC", GCTaskGuid, 13, "TerminateConcurrentThread", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<GCFinalizersEndTraceData> GCFinalizersStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new GCFinalizersEndTraceData(value, 13, 1, "GC", GCTaskGuid, 15, "FinalizersStop", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<GCNoUserDataTraceData> GCFinalizersStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new GCNoUserDataTraceData(value, 14, 1, "GC", GCTaskGuid, 19, "FinalizersStart", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<IOThreadTraceData> IOThreadCreationStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new IOThreadTraceData(value, 44, 3, "IOThreadCreation", IOThreadCreationTaskGuid, 1, "Start", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<IOThreadTraceData> IOThreadCreationStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new IOThreadTraceData(value, 45, 3, "IOThreadCreation", IOThreadCreationTaskGuid, 2, "Stop", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<IOThreadTraceData> IOThreadRetirementStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new IOThreadTraceData(value, 46, 5, "IOThreadRetirement", IOThreadRetirementTaskGuid, 1, "Start", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<IOThreadTraceData> IOThreadRetirementStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new IOThreadTraceData(value, 47, 5, "IOThreadRetirement", IOThreadRetirementTaskGuid, 2, "Stop", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ThreadPoolWorkerThreadTraceData> ThreadPoolWorkerThreadStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ThreadPoolWorkerThreadTraceData(value, 50, 16, "ThreadPoolWorkerThread", ThreadPoolWorkerThreadTaskGuid, 1, "Start", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ThreadPoolWorkerThreadTraceData> ThreadPoolWorkerThreadStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ThreadPoolWorkerThreadTraceData(value, 51, 16, "ThreadPoolWorkerThread", ThreadPoolWorkerThreadTaskGuid, 2, "Stop", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ThreadPoolWorkerThreadTraceData> ThreadPoolWorkerThreadRetirementStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ThreadPoolWorkerThreadTraceData(value, 52, 17, "ThreadPoolWorkerThreadRetirement", ThreadPoolWorkerThreadRetirementTaskGuid, 1, "Start", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ThreadPoolWorkerThreadTraceData> ThreadPoolWorkerThreadRetirementStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ThreadPoolWorkerThreadTraceData(value, 53, 17, "ThreadPoolWorkerThreadRetirement", ThreadPoolWorkerThreadRetirementTaskGuid, 2, "Stop", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ThreadPoolWorkerThreadAdjustmentSampleTraceData> ThreadPoolWorkerThreadAdjustmentSample
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ThreadPoolWorkerThreadAdjustmentSampleTraceData(value, 54, 18, "ThreadPoolWorkerThreadAdjustment", ThreadPoolWorkerThreadAdjustmentTaskGuid, 100, "Sample", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ThreadPoolWorkerThreadAdjustmentAdjustmentTraceData> ThreadPoolWorkerThreadAdjustmentAdjustment
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ThreadPoolWorkerThreadAdjustmentAdjustmentTraceData(value, 55, 18, "ThreadPoolWorkerThreadAdjustment", ThreadPoolWorkerThreadAdjustmentTaskGuid, 101, "Adjustment", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ThreadPoolWorkerThreadAdjustmentStatsTraceData> ThreadPoolWorkerThreadAdjustmentStats
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ThreadPoolWorkerThreadAdjustmentStatsTraceData(value, 56, 18, "ThreadPoolWorkerThreadAdjustment", ThreadPoolWorkerThreadAdjustmentTaskGuid, 102, "Stats", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ExceptionTraceData> ExceptionStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ExceptionTraceData(value, 80, 7, "Exception", ExceptionTaskGuid, 1, "Start", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ContentionTraceData> ContentionStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ContentionTraceData(value, 81, 8, "Contention", ContentionTaskGuid, 1, "Start", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodILToNativeMapTraceData> MethodILToNativeMap
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodILToNativeMapTraceData(value, 190, 9, "Method", MethodTaskGuid, 87, "ILToNativeMap", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ClrStackWalkTraceData> ClrStackWalk
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ClrStackWalkTraceData(value, 82, 11, "ClrStack", ClrStackTaskGuid, 82, "Walk", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<AppDomainMemAllocatedTraceData> AppDomainResourceManagementMemAllocated
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new AppDomainMemAllocatedTraceData(value, 83, 14, "AppDomainResourceManagement", AppDomainResourceManagementTaskGuid, 48, "MemAllocated", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<AppDomainMemSurvivedTraceData> AppDomainResourceManagementMemSurvived
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new AppDomainMemSurvivedTraceData(value, 84, 14, "AppDomainResourceManagement", AppDomainResourceManagementTaskGuid, 49, "MemSurvived", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ThreadCreatedTraceData> AppDomainResourceManagementThreadCreated
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ThreadCreatedTraceData(value, 85, 14, "AppDomainResourceManagement", AppDomainResourceManagementTaskGuid, 50, "ThreadCreated", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ThreadTerminatedOrTransitionTraceData> AppDomainResourceManagementThreadTerminated
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ThreadTerminatedOrTransitionTraceData(value, 86, 14, "AppDomainResourceManagement", AppDomainResourceManagementTaskGuid, 51, "ThreadTerminated", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ThreadTerminatedOrTransitionTraceData> AppDomainResourceManagementDomainEnter
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ThreadTerminatedOrTransitionTraceData(value, 87, 14, "AppDomainResourceManagement", AppDomainResourceManagementTaskGuid, 52, "DomainEnter", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ILStubGeneratedTraceData> ILStubStubGenerated
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ILStubGeneratedTraceData(value, 88, 15, "ILStub", ILStubTaskGuid, 88, "StubGenerated", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ILStubCacheHitTraceData> ILStubStubCacheHit
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ILStubCacheHitTraceData(value, 89, 15, "ILStub", ILStubTaskGuid, 89, "StubCacheHit", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ContentionTraceData> ContentionStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ContentionTraceData(value, 91, 8, "Contention", ContentionTaskGuid, 2, "Stop", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<EmptyTraceData> MethodDCStartCompleteV2
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, 135, 9, "Method", MethodTaskGuid, 14, "DCStartCompleteV2", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<EmptyTraceData> MethodDCEndCompleteV2
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, 136, 9, "Method", MethodTaskGuid, 15, "DCEndCompleteV2", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodLoadUnloadTraceData> MethodDCStartV2
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodLoadUnloadTraceData(value, 137, 9, "Method", MethodTaskGuid, 35, "DCStartV2", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodLoadUnloadTraceData> MethodDCStopV2
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodLoadUnloadTraceData(value, 138, 9, "Method", MethodTaskGuid, 36, "DCStopV2", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodLoadUnloadVerboseTraceData> MethodDCStartVerboseV2
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodLoadUnloadVerboseTraceData(value, 139, 9, "Method", MethodTaskGuid, 39, "DCStartVerboseV2", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodLoadUnloadVerboseTraceData> MethodDCStopVerboseV2
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodLoadUnloadVerboseTraceData(value, 140, 9, "Method", MethodTaskGuid, 40, "DCStopVerboseV2", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodLoadUnloadTraceData> MethodLoad
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodLoadUnloadTraceData(value, 141, 9, "Method", MethodTaskGuid, 33, "Load", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodLoadUnloadTraceData> MethodUnload
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodLoadUnloadTraceData(value, 142, 9, "Method", MethodTaskGuid, 34, "Unload", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodLoadUnloadVerboseTraceData> MethodLoadVerbose
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodLoadUnloadVerboseTraceData(value, 143, 9, "Method", MethodTaskGuid, 37, "LoadVerbose", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodLoadUnloadVerboseTraceData> MethodUnloadVerbose
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodLoadUnloadVerboseTraceData(value, 144, 9, "Method", MethodTaskGuid, 38, "UnloadVerbose", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodJittingStartedTraceData> MethodJittingStarted
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodJittingStartedTraceData(value, 145, 9, "Method", MethodTaskGuid, 42, "JittingStarted", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ModuleLoadUnloadTraceData> LoaderModuleDCStartV2
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ModuleLoadUnloadTraceData(value, 149, 10, "Loader", LoaderTaskGuid, 35, "ModuleDCStartV2", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ModuleLoadUnloadTraceData> LoaderModuleDCStopV2
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ModuleLoadUnloadTraceData(value, 150, 10, "Loader", LoaderTaskGuid, 36, "ModuleDCStopV2", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DomainModuleLoadUnloadTraceData> LoaderDomainModuleLoad
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DomainModuleLoadUnloadTraceData(value, 151, 10, "Loader", LoaderTaskGuid, 45, "DomainModuleLoad", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ModuleLoadUnloadTraceData> LoaderModuleLoad
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ModuleLoadUnloadTraceData(value, 152, 10, "Loader", LoaderTaskGuid, 33, "ModuleLoad", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ModuleLoadUnloadTraceData> LoaderModuleUnload
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ModuleLoadUnloadTraceData(value, 153, 10, "Loader", LoaderTaskGuid, 34, "ModuleUnload", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<AssemblyLoadUnloadTraceData> LoaderAssemblyLoad
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new AssemblyLoadUnloadTraceData(value, 154, 10, "Loader", LoaderTaskGuid, 37, "AssemblyLoad", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<AssemblyLoadUnloadTraceData> LoaderAssemblyUnload
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new AssemblyLoadUnloadTraceData(value, 155, 10, "Loader", LoaderTaskGuid, 38, "AssemblyUnload", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<AppDomainLoadUnloadTraceData> LoaderAppDomainLoad
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new AppDomainLoadUnloadTraceData(value, 156, 10, "Loader", LoaderTaskGuid, 41, "AppDomainLoad", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<AppDomainLoadUnloadTraceData> LoaderAppDomainUnload
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new AppDomainLoadUnloadTraceData(value, 157, 10, "Loader", LoaderTaskGuid, 42, "AppDomainUnload", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<StrongNameVerificationTraceData> StrongNameVerificationStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new StrongNameVerificationTraceData(value, 181, 12, "StrongNameVerification", StrongNameVerificationTaskGuid, 1, "Start", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<StrongNameVerificationTraceData> StrongNameVerificationStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new StrongNameVerificationTraceData(value, 182, 12, "StrongNameVerification", StrongNameVerificationTaskGuid, 2, "Stop", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<AuthenticodeVerificationTraceData> AuthenticodeVerificationStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new AuthenticodeVerificationTraceData(value, 183, 13, "AuthenticodeVerification", AuthenticodeVerificationTaskGuid, 1, "Start", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<AuthenticodeVerificationTraceData> AuthenticodeVerificationStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new AuthenticodeVerificationTraceData(value, 184, 13, "AuthenticodeVerification", AuthenticodeVerificationTaskGuid, 2, "Stop", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodJitInliningSucceededTraceData> MethodInliningSucceeded
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodJitInliningSucceededTraceData(value, 185, 9, "Method", MethodTaskGuid, 83, "InliningSucceeded", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodJitInliningFailedTraceData> MethodInliningFailed
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodJitInliningFailedTraceData(value, 186, 9, "Method", MethodTaskGuid, 84, "InliningFailed", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RuntimeInformationTraceData> RuntimeStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RuntimeInformationTraceData(value, 187, 19, "Runtime", RuntimeTaskGuid, 1, "Start", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodJitTailCallSucceededTraceData> MethodTailCallSucceeded
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodJitTailCallSucceededTraceData(value, 188, 9, "Method", MethodTaskGuid, 85, "TailCallSucceeded", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodJitTailCallFailedTraceData> MethodTailCallFailed
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodJitTailCallFailedTraceData(value, 189, 9, "Method", MethodTaskGuid, 86, "TailCallFailed", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }

        #region Event ID Definitions
        public const TraceEventID GCStartEventID = (TraceEventID)1;
        public const TraceEventID GCStopEventID = (TraceEventID)2;
        public const TraceEventID GCRestartEEStopEventID = (TraceEventID)3;
        public const TraceEventID GCHeapStatsEventID = (TraceEventID)4;
        public const TraceEventID GCCreateSegmentEventID = (TraceEventID)5;
        public const TraceEventID GCFreeSegmentEventID = (TraceEventID)6;
        public const TraceEventID GCRestartEEStartEventID = (TraceEventID)7;
        public const TraceEventID GCSuspendEEStopEventID = (TraceEventID)8;
        public const TraceEventID GCSuspendEEStartEventID = (TraceEventID)9;
        public const TraceEventID GCAllocationTickEventID = (TraceEventID)10;
        public const TraceEventID GCCreateConcurrentThreadEventID = (TraceEventID)11;
        public const TraceEventID GCTerminateConcurrentThreadEventID = (TraceEventID)12;
        public const TraceEventID GCFinalizersStopEventID = (TraceEventID)13;
        public const TraceEventID GCFinalizersStartEventID = (TraceEventID)14;
        public const TraceEventID WorkerThreadCreationV2StartEventID = (TraceEventID)40;
        public const TraceEventID WorkerThreadCreationV2StopEventID = (TraceEventID)41;
        public const TraceEventID WorkerThreadRetirementV2StartEventID = (TraceEventID)42;
        public const TraceEventID WorkerThreadRetirementV2StopEventID = (TraceEventID)43;
        public const TraceEventID IOThreadCreationStartEventID = (TraceEventID)44;
        public const TraceEventID IOThreadCreationStopEventID = (TraceEventID)45;
        public const TraceEventID IOThreadRetirementStartEventID = (TraceEventID)46;
        public const TraceEventID IOThreadRetirementStopEventID = (TraceEventID)47;
        public const TraceEventID ThreadpoolSuspensionV2StartEventID = (TraceEventID)48;
        public const TraceEventID ThreadpoolSuspensionV2StopEventID = (TraceEventID)49;
        public const TraceEventID ThreadPoolWorkerThreadStartEventID = (TraceEventID)50;
        public const TraceEventID ThreadPoolWorkerThreadStopEventID = (TraceEventID)51;
        public const TraceEventID ThreadPoolWorkerThreadRetirementStartEventID = (TraceEventID)52;
        public const TraceEventID ThreadPoolWorkerThreadRetirementStopEventID = (TraceEventID)53;
        public const TraceEventID ThreadPoolWorkerThreadAdjustmentSampleEventID = (TraceEventID)54;
        public const TraceEventID ThreadPoolWorkerThreadAdjustmentAdjustmentEventID = (TraceEventID)55;
        public const TraceEventID ThreadPoolWorkerThreadAdjustmentStatsEventID = (TraceEventID)56;
        public const TraceEventID ExceptionStartEventID = (TraceEventID)80;
        public const TraceEventID ContentionStartEventID = (TraceEventID)81;
        public const TraceEventID ClrStackWalkEventID = (TraceEventID)82;
        public const TraceEventID AppDomainResourceManagementMemAllocatedEventID = (TraceEventID)83;
        public const TraceEventID AppDomainResourceManagementMemSurvivedEventID = (TraceEventID)84;
        public const TraceEventID AppDomainResourceManagementThreadCreatedEventID = (TraceEventID)85;
        public const TraceEventID AppDomainResourceManagementThreadTerminatedEventID = (TraceEventID)86;
        public const TraceEventID AppDomainResourceManagementDomainEnterEventID = (TraceEventID)87;
        public const TraceEventID ILStubStubGeneratedEventID = (TraceEventID)88;
        public const TraceEventID ILStubStubCacheHitEventID = (TraceEventID)89;
        public const TraceEventID ContentionStopEventID = (TraceEventID)91;
        public const TraceEventID MethodDCStartCompleteV2EventID = (TraceEventID)135;
        public const TraceEventID MethodDCEndCompleteV2EventID = (TraceEventID)136;
        public const TraceEventID MethodDCStartV2EventID = (TraceEventID)137;
        public const TraceEventID MethodDCStopV2EventID = (TraceEventID)138;
        public const TraceEventID MethodDCStartVerboseV2EventID = (TraceEventID)139;
        public const TraceEventID MethodDCStopVerboseV2EventID = (TraceEventID)140;
        public const TraceEventID MethodLoadEventID = (TraceEventID)141;
        public const TraceEventID MethodUnloadEventID = (TraceEventID)142;
        public const TraceEventID MethodLoadVerboseEventID = (TraceEventID)143;
        public const TraceEventID MethodUnloadVerboseEventID = (TraceEventID)144;
        public const TraceEventID MethodJittingStartedEventID = (TraceEventID)145;
        public const TraceEventID LoaderModuleDCStartV2EventID = (TraceEventID)149;
        public const TraceEventID LoaderModuleDCStopV2EventID = (TraceEventID)150;
        public const TraceEventID LoaderDomainModuleLoadEventID = (TraceEventID)151;
        public const TraceEventID LoaderModuleLoadEventID = (TraceEventID)152;
        public const TraceEventID LoaderModuleUnloadEventID = (TraceEventID)153;
        public const TraceEventID LoaderAssemblyLoadEventID = (TraceEventID)154;
        public const TraceEventID LoaderAssemblyUnloadEventID = (TraceEventID)155;
        public const TraceEventID LoaderAppDomainLoadEventID = (TraceEventID)156;
        public const TraceEventID LoaderAppDomainUnloadEventID = (TraceEventID)157;
        public const TraceEventID StrongNameVerificationStartEventID = (TraceEventID)181;
        public const TraceEventID StrongNameVerificationStopEventID = (TraceEventID)182;
        public const TraceEventID AuthenticodeVerificationStartEventID = (TraceEventID)183;
        public const TraceEventID AuthenticodeVerificationStopEventID = (TraceEventID)184;
        public const TraceEventID MethodInliningSucceededEventID = (TraceEventID)185;
        public const TraceEventID MethodInliningFailedEventID = (TraceEventID)186;
        public const TraceEventID RuntimeStartEventID = (TraceEventID)187;
        public const TraceEventID MethodTailCallSucceededEventID = (TraceEventID)188;
        public const TraceEventID MethodTailCallFailedEventID = (TraceEventID)189;
        #endregion

        #region private
        private static Guid GCTaskGuid = new Guid(unchecked((int)0x044973cd), unchecked((short)0x251f), unchecked((short)0x4dff), 0xa3, 0xe9, 0x9d, 0x63, 0x07, 0x28, 0x6b, 0x05);
        private static Guid WorkerThreadCreationV2TaskGuid = new Guid(unchecked((int)0xcfc4ba53), unchecked((short)0xfb42), unchecked((short)0x4757), 0x8b, 0x70, 0x5f, 0x5d, 0x51, 0xfe, 0xe2, 0xf4);
        private static Guid IOThreadCreationTaskGuid = new Guid(unchecked((int)0xc71408de), unchecked((short)0x42cc), unchecked((short)0x4f81), 0x9c, 0x93, 0xb8, 0x91, 0x2a, 0xbf, 0x2a, 0x0f);
        private static Guid WorkerThreadRetirementV2TaskGuid = new Guid(unchecked((int)0xefdf1eac), unchecked((short)0x1d5d), unchecked((short)0x4e84), 0x89, 0x3a, 0x19, 0xb8, 0x0f, 0x69, 0x21, 0x76);
        private static Guid IOThreadRetirementTaskGuid = new Guid(unchecked((int)0x840c8456), unchecked((short)0x6457), unchecked((short)0x4eb7), 0x9c, 0xd0, 0xd2, 0x8f, 0x01, 0xc6, 0x4f, 0x5e);
        private static Guid ThreadpoolSuspensionV2TaskGuid = new Guid(unchecked((int)0xc424b3e3), unchecked((short)0x2ae0), unchecked((short)0x416e), 0xa0, 0x39, 0x41, 0x0c, 0x5d, 0x8e, 0x5f, 0x14);
        private static Guid ExceptionTaskGuid = new Guid(unchecked((int)0x300ce105), unchecked((short)0x86d1), unchecked((short)0x41f8), 0xb9, 0xd2, 0x83, 0xfc, 0xbf, 0xf3, 0x2d, 0x99);
        private static Guid ContentionTaskGuid = new Guid(unchecked((int)0x561410f5), unchecked((short)0xa138), unchecked((short)0x4ab3), 0x94, 0x5e, 0x51, 0x64, 0x83, 0xcd, 0xdf, 0xbc);
        private static Guid MethodTaskGuid = new Guid(unchecked((int)0x3044f61a), unchecked((short)0x99b0), unchecked((short)0x4c21), 0xb2, 0x03, 0xd3, 0x94, 0x23, 0xc7, 0x3b, 0x00);
        private static Guid LoaderTaskGuid = new Guid(unchecked((int)0xd00792da), unchecked((short)0x07b7), unchecked((short)0x40f5), 0x97, 0xeb, 0x5d, 0x97, 0x4e, 0x05, 0x47, 0x40);
        private static Guid ClrStackTaskGuid = new Guid(unchecked((int)0xd3363dc0), unchecked((short)0x243a), unchecked((short)0x4620), 0xa4, 0xd0, 0x8a, 0x07, 0xd7, 0x72, 0xf5, 0x33);
        private static Guid StrongNameVerificationTaskGuid = new Guid(unchecked((int)0x15447a14), unchecked((short)0xb523), unchecked((short)0x46ae), 0xb7, 0x5b, 0x02, 0x3f, 0x90, 0x0b, 0x43, 0x93);
        private static Guid AuthenticodeVerificationTaskGuid = new Guid(unchecked((int)0xb17304d9), unchecked((short)0x5afa), unchecked((short)0x4da6), 0x9f, 0x7b, 0x5a, 0x4f, 0xa7, 0x31, 0x29, 0xb6);
        private static Guid AppDomainResourceManagementTaskGuid = new Guid(unchecked((int)0x88e83959), unchecked((short)0x6185), unchecked((short)0x4e0b), 0x95, 0xb8, 0x0e, 0x4a, 0x35, 0xdf, 0x61, 0x22);
        private static Guid ILStubTaskGuid = new Guid(unchecked((int)0xd00792da), unchecked((short)0x07b7), unchecked((short)0x40f5), 0x00, 0x00, 0x5d, 0x97, 0x4e, 0x05, 0x47, 0x40);
        private static Guid ThreadPoolWorkerThreadTaskGuid = new Guid(unchecked((int)0x8a9a44ab), unchecked((short)0xf681), unchecked((short)0x4271), 0x88, 0x10, 0x83, 0x0d, 0xab, 0x9f, 0x56, 0x21);
        private static Guid ThreadPoolWorkerThreadRetirementTaskGuid = new Guid(unchecked((int)0x402ee399), unchecked((short)0xc137), unchecked((short)0x4dc0), 0xa5, 0xab, 0x3c, 0x2d, 0xea, 0x64, 0xac, 0x9c);
        private static Guid ThreadPoolWorkerThreadAdjustmentTaskGuid = new Guid(unchecked((int)0x94179831), unchecked((short)0xe99a), unchecked((short)0x4625), 0x88, 0x24, 0x23, 0xca, 0x5e, 0x00, 0xca, 0x7d);
        private static Guid RuntimeTaskGuid = new Guid(unchecked((int)0xcd7d3e32), unchecked((short)0x65fe), unchecked((short)0x40cd), 0x92, 0x25, 0xa2, 0x57, 0x7d, 0x20, 0x3f, 0xc3);
        #endregion
    }

    public sealed class GCStartTraceData : TraceEvent
    {
        public int Count { get { return GetInt32At(0); } }
        public GCReason Reason { get { if (EventDataLength >= 16) return (GCReason)GetInt32At(8); return (GCReason)GetInt32At(4); } }
        public int Depth { get { if (EventDataLength >= 16) return GetInt32At(4); return 0; } }
        public GCType Type { get { if (EventDataLength >= 16) return (GCType)GetInt32At(12); return (GCType)0; } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(16); return 0; } }

        #region Private
        internal GCStartTraceData(Action<GCStartTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength < 8));       // FIXed manually to be < 8 
            Debug.Assert(!(Version == 1 && EventDataLength != 18));
            Debug.Assert(!(Version > 1 && EventDataLength < 18));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("Count", Count);
            sb.XmlAttrib("Reason", Reason);
            sb.XmlAttrib("Depth", Depth);
            sb.XmlAttrib("Type", Type);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Count", "Reason", "Depth", "Type", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Count;
                case 1:
                    return Reason;
                case 2:
                    return Depth;
                case 3:
                    return Type;
                case 4:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<GCStartTraceData> Action;
        #endregion
    }
    public sealed class GCEndTraceData : TraceEvent
    {
        public int Count { get { return GetInt32At(0); } }
        public int Depth { get { if (Version >= 1) return GetInt32At(4); return GetInt16At(4); } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(8); return 0; } }

        #region Private
        internal GCEndTraceData(Action<GCEndTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength < 6));           // HAND_MODIFIED <
            Debug.Assert(!(Version == 1 && EventDataLength != 10));
            Debug.Assert(!(Version > 1 && EventDataLength < 10));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("Count", Count);
            sb.XmlAttrib("Depth", Depth);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Count", "Depth", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Count;
                case 1:
                    return Depth;
                case 2:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<GCEndTraceData> Action;
        #endregion
    }
    public sealed class GCNoUserDataTraceData : TraceEvent
    {
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(0); return 0; } }

        #region Private
        internal GCNoUserDataTraceData(Action<GCNoUserDataTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 1 && EventDataLength != 2));
            Debug.Assert(!(Version > 1 && EventDataLength < 2));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<GCNoUserDataTraceData> Action;
        #endregion
    }

    public sealed class GCHeapStatsTraceData : TraceEvent
    {
        // GCHeap stats are reported AFTER the GC has completed.  Thus these number are the 'After' heap size for each generation
        // The sizes INCLUDE fragmentation (holes in the segement)

        // The TotalPromotedSize0 is the amount that SURVIVED Gen0 (thus it is now in Gen1, thus TotalPromoted0 <= GenerationSize1)
        public long GenerationSize0 { get { return GetInt64At(0); } }
        public long TotalPromotedSize0 { get { return GetInt64At(8); } }
        public long GenerationSize1 { get { return GetInt64At(16); } }
        public long TotalPromotedSize1 { get { return GetInt64At(24); } }
        public long GenerationSize2 { get { return GetInt64At(32); } }
        public long TotalPromotedSize2 { get { return GetInt64At(40); } }
        public long GenerationSize3 { get { return GetInt64At(48); } }
        public long TotalPromotedSize3 { get { return GetInt64At(56); } }
        public long FinalizationPromotedSize { get { return GetInt64At(64); } }
        public long FinalizationPromotedCount { get { return GetInt64At(72); } }
        public int PinnedObjectCount { get { return GetInt32At(80); } }
        public int SinkBlockCount { get { return GetInt32At(84); } }
        public int GCHandleCount { get { return GetInt32At(88); } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(92); return 0; } }

        #region Private
        internal GCHeapStatsTraceData(Action<GCHeapStatsTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 96));          // HAND_MODIFIED C++ pads to 96
            Debug.Assert(!(Version == 1 && EventDataLength != 94));
            Debug.Assert(!(Version > 1 && EventDataLength < 94));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("GenerationSize0", GenerationSize0);
            sb.XmlAttribHex("TotalPromotedSize0", TotalPromotedSize0);
            sb.XmlAttribHex("GenerationSize1", GenerationSize1);
            sb.XmlAttribHex("TotalPromotedSize1", TotalPromotedSize1);
            sb.XmlAttribHex("GenerationSize2", GenerationSize2);
            sb.XmlAttribHex("TotalPromotedSize2", TotalPromotedSize2);
            sb.XmlAttribHex("GenerationSize3", GenerationSize3);
            sb.XmlAttribHex("TotalPromotedSize3", TotalPromotedSize3);
            sb.XmlAttribHex("FinalizationPromotedSize", FinalizationPromotedSize);
            sb.XmlAttrib("FinalizationPromotedCount", FinalizationPromotedCount);
            sb.XmlAttrib("PinnedObjectCount", PinnedObjectCount);
            sb.XmlAttrib("SinkBlockCount", SinkBlockCount);
            sb.XmlAttrib("GCHandleCount", GCHandleCount);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "GenerationSize0", "TotalPromotedSize0", "GenerationSize1", "TotalPromotedSize1", "GenerationSize2", "TotalPromotedSize2", "GenerationSize3", "TotalPromotedSize3", "FinalizationPromotedSize", "FinalizationPromotedCount", "PinnedObjectCount", "SinkBlockCount", "GCHandleCount", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return GenerationSize0;
                case 1:
                    return TotalPromotedSize0;
                case 2:
                    return GenerationSize1;
                case 3:
                    return TotalPromotedSize1;
                case 4:
                    return GenerationSize2;
                case 5:
                    return TotalPromotedSize2;
                case 6:
                    return GenerationSize3;
                case 7:
                    return TotalPromotedSize3;
                case 8:
                    return FinalizationPromotedSize;
                case 9:
                    return FinalizationPromotedCount;
                case 10:
                    return PinnedObjectCount;
                case 11:
                    return SinkBlockCount;
                case 12:
                    return GCHandleCount;
                case 13:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<GCHeapStatsTraceData> Action;
        #endregion
    }
    public sealed class GCCreateSegmentTraceData : TraceEvent
    {
        public long Address { get { return GetInt64At(0); } }
        public long Size { get { return GetInt64At(8); } }
        public GCSegmentType Type { get { return (GCSegmentType)GetInt32At(16); } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(20); return 0; } }

        #region Private
        internal GCCreateSegmentTraceData(Action<GCCreateSegmentTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength < 20));      // HAND_MODIFIED V0 has 24  because of C++ rounding
            Debug.Assert(!(Version == 1 && EventDataLength != 22));
            Debug.Assert(!(Version > 1 && EventDataLength < 22));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("Address", Address);
            sb.XmlAttribHex("Size", Size);
            sb.XmlAttrib("Type", Type);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Address", "Size", "Type", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Address;
                case 1:
                    return Size;
                case 2:
                    return Type;
                case 3:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<GCCreateSegmentTraceData> Action;
        #endregion
    }
    public sealed class GCFreeSegmentTraceData : TraceEvent
    {
        public long Address { get { return GetInt64At(0); } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(8); return 0; } }

        #region Private
        internal GCFreeSegmentTraceData(Action<GCFreeSegmentTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 8));
            Debug.Assert(!(Version == 1 && EventDataLength != 10));
            Debug.Assert(!(Version > 1 && EventDataLength < 10));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("Address", Address);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Address", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Address;
                case 1:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<GCFreeSegmentTraceData> Action;
        #endregion
    }
    public sealed class GCSuspendEETraceData : TraceEvent
    {
        public GCSuspendEEReason Reason { get { if (Version >= 1) return (GCSuspendEEReason)GetInt32At(0); return (GCSuspendEEReason)GetInt16At(0); } }
        public int Count { get { if (Version >= 1) return GetInt32At(4); return 0; } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(8); return 0; } }

        #region Private
        internal GCSuspendEETraceData(Action<GCSuspendEETraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength < 2));       // HAND_MODIFIED 
            Debug.Assert(!(Version == 1 && EventDataLength != 10));
            Debug.Assert(!(Version > 1 && EventDataLength < 10));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("Reason", Reason);
            sb.XmlAttrib("Count", Count);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Reason", "Count", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Reason;
                case 1:
                    return Count;
                case 2:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<GCSuspendEETraceData> Action;
        #endregion
    }
    public sealed class GCAllocationTickTraceData : TraceEvent
    {
        public int AllocationAmount { get { return GetInt32At(0); } }
        public GCAllocationKind AllocationKind { get { return (GCAllocationKind)GetInt32At(4); } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(8); return 0; } }
        public long AllocationAmount64 { get { if (Version >= 2) return GetInt64At(10); return 0; } }
        public Address TypeID { get { if (Version >= 2) return GetHostPointer(18); return 0; } }
        public string TypeName { get { if (Version >= 2) return GetUnicodeStringAt(18 + PointerSize); return ""; } }
        public int HeapIndex { get { if (Version >= 2) return GetInt32At(SkipUnicodeString(18 + PointerSize)); return 0; } }
        #region Private
        internal GCAllocationTickTraceData(Action<GCAllocationTickTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 8));
            Debug.Assert(!(Version == 1 && EventDataLength != 10));
            Debug.Assert(!(Version > 1 && EventDataLength < 10));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("AllocationAmount", AllocationAmount);
            sb.XmlAttrib("AllocationKind", AllocationKind);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            if (Version >= 2)
            {
                sb.XmlAttrib("AllocationAmount64", AllocationAmount64);
                sb.XmlAttrib("TypeID", TypeID);
                sb.XmlAttrib("TypeName", TypeName);
                sb.XmlAttrib("HeapIndex", HeapIndex);
            }
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "AllocationAmount", "AllocationKind", "ClrInstanceID", "AllocationAmount64", "TypeID", "TypeName", "HeapIndex"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return AllocationAmount;
                case 1:
                    return AllocationKind;
                case 2:
                    return ClrInstanceID;
                case 3:
                    return AllocationAmount64;
                case 4:
                    return TypeID;
                case 5:
                    return TypeName;
                case 6:
                    return HeapIndex;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        const int OneKB = 1024;
        const int OneMB = OneKB * OneKB;

        public long GetAllocAmount(ref bool seenBadAllocTick)
        {
            // We get bad values in old runtimes.   once we see a bad value 'fix' all values. 
            // TODO warn the user...
            long amount = AllocationAmount64; // AllocationAmount is truncated for allocation larger than 2Gb, use 64-bit value if available.

            if (amount == 0)
            {
                amount = AllocationAmount;
            }

            if (amount < 0)
            {
                seenBadAllocTick = true;
            }

            if (seenBadAllocTick)
            {
                // Clap this between 90K and 110K (for small objs) and 90K to 2Meg (for large obects).  
                amount = Math.Max(amount, 90 * OneKB);
                amount = Math.Min(amount, (AllocationKind == GCAllocationKind.Small) ? 110 * OneKB : 2 * OneMB);
            }

            return amount;
        }

        private event Action<GCAllocationTickTraceData> Action;
        #endregion
    }
    public sealed class GCCreateConcurrentThreadTraceData : TraceEvent
    {
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(0); return 0; } }

        #region Private
        internal GCCreateConcurrentThreadTraceData(Action<GCCreateConcurrentThreadTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 1 && EventDataLength != 2));
            Debug.Assert(!(Version > 1 && EventDataLength < 2));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<GCCreateConcurrentThreadTraceData> Action;
        #endregion
    }
    public sealed class GCTerminateConcurrentThreadTraceData : TraceEvent
    {
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(0); return 0; } }

        #region Private
        internal GCTerminateConcurrentThreadTraceData(Action<GCTerminateConcurrentThreadTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 1 && EventDataLength != 2));
            Debug.Assert(!(Version > 1 && EventDataLength < 2));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<GCTerminateConcurrentThreadTraceData> Action;
        #endregion
    }
    public sealed class GCFinalizersEndTraceData : TraceEvent
    {
        public int Count { get { return GetInt32At(0); } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(4); return 0; } }

        #region Private
        internal GCFinalizersEndTraceData(Action<GCFinalizersEndTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 4));
            Debug.Assert(!(Version == 1 && EventDataLength != 6));
            Debug.Assert(!(Version > 1 && EventDataLength < 6));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("Count", Count);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Count", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Count;
                case 1:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<GCFinalizersEndTraceData> Action;
        #endregion
    }
    public sealed class ClrWorkerThreadTraceData : TraceEvent
    {
        public int WorkerThreadCount { get { return GetInt32At(0); } }
        public int RetiredWorkerThreads { get { return GetInt32At(4); } }

        #region Private
        internal ClrWorkerThreadTraceData(Action<ClrWorkerThreadTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 8));
            Debug.Assert(!(Version > 0 && EventDataLength < 8));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("WorkerThreadCount", WorkerThreadCount);
            sb.XmlAttrib("RetiredWorkerThreads", RetiredWorkerThreads);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "WorkerThreadCount", "RetiredWorkerThreads" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return WorkerThreadCount;
                case 1:
                    return RetiredWorkerThreads;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ClrWorkerThreadTraceData> Action;
        #endregion
    }
    public sealed class IOThreadTraceData : TraceEvent
    {
        public int IOThreadCount { get { return GetInt32At(0); } }
        public int RetiredIOThreads { get { return GetInt32At(4); } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(8); return 0; } }

        #region Private
        internal IOThreadTraceData(Action<IOThreadTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 8));
            Debug.Assert(!(Version == 1 && EventDataLength != 10));
            Debug.Assert(!(Version > 1 && EventDataLength < 10));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("IOThreadCount", IOThreadCount);
            sb.XmlAttrib("RetiredIOThreads", RetiredIOThreads);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "IOThreadCount", "RetiredIOThreads", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return IOThreadCount;
                case 1:
                    return RetiredIOThreads;
                case 2:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<IOThreadTraceData> Action;
        #endregion
    }
    public sealed class ClrThreadPoolSuspendTraceData : TraceEvent
    {
        public int ClrThreadID { get { return GetInt32At(0); } }
        public int CpuUtilization { get { return GetInt32At(4); } }

        #region Private
        internal ClrThreadPoolSuspendTraceData(Action<ClrThreadPoolSuspendTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 8));
            Debug.Assert(!(Version > 0 && EventDataLength < 8));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("ClrThreadID", ClrThreadID);
            sb.XmlAttrib("CpuUtilization", CpuUtilization);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ClrThreadID", "CpuUtilization" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ClrThreadID;
                case 1:
                    return CpuUtilization;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ClrThreadPoolSuspendTraceData> Action;
        #endregion
    }
    public sealed class ThreadPoolWorkerThreadTraceData : TraceEvent
    {
        public int ActiveWorkerThreadCount { get { return GetInt32At(0); } }
        public int RetiredWorkerThreadCount { get { return GetInt32At(4); } }
        public int ClrInstanceID { get { return GetInt16At(8); } }

        #region Private
        internal ThreadPoolWorkerThreadTraceData(Action<ThreadPoolWorkerThreadTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 10));
            Debug.Assert(!(Version > 0 && EventDataLength < 10));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("ActiveWorkerThreadCount", ActiveWorkerThreadCount);
            sb.XmlAttrib("RetiredWorkerThreadCount", RetiredWorkerThreadCount);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ActiveWorkerThreadCount", "RetiredWorkerThreadCount", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ActiveWorkerThreadCount;
                case 1:
                    return RetiredWorkerThreadCount;
                case 2:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ThreadPoolWorkerThreadTraceData> Action;
        #endregion
    }
    public sealed class ThreadPoolWorkerThreadAdjustmentSampleTraceData : TraceEvent
    {
        public double Throughput { get { return GetDoubleAt(0); } }
        public int ClrInstanceID { get { return GetInt16At(8); } }

        #region Private
        internal ThreadPoolWorkerThreadAdjustmentSampleTraceData(Action<ThreadPoolWorkerThreadAdjustmentSampleTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 10));
            Debug.Assert(!(Version > 0 && EventDataLength < 10));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("Throughput", Throughput);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Throughput", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Throughput;
                case 1:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ThreadPoolWorkerThreadAdjustmentSampleTraceData> Action;
        #endregion
    }
    public sealed class ThreadPoolWorkerThreadAdjustmentAdjustmentTraceData : TraceEvent
    {
        public double AverageThroughput { get { return GetDoubleAt(0); } }
        public int NewWorkerThreadCount { get { return GetInt32At(8); } }
        public ThreadAdjustmentReason Reason { get { return (ThreadAdjustmentReason)GetInt32At(12); } }
        public int ClrInstanceID { get { return GetInt16At(16); } }

        #region Private
        internal ThreadPoolWorkerThreadAdjustmentAdjustmentTraceData(Action<ThreadPoolWorkerThreadAdjustmentAdjustmentTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 18));
            Debug.Assert(!(Version > 0 && EventDataLength < 18));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("AverageThroughput", AverageThroughput);
            sb.XmlAttrib("NewWorkerThreadCount", NewWorkerThreadCount);
            sb.XmlAttrib("Reason", Reason);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "AverageThroughput", "NewWorkerThreadCount", "Reason", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return AverageThroughput;
                case 1:
                    return NewWorkerThreadCount;
                case 2:
                    return Reason;
                case 3:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ThreadPoolWorkerThreadAdjustmentAdjustmentTraceData> Action;
        #endregion
    }
    public sealed class ThreadPoolWorkerThreadAdjustmentStatsTraceData : TraceEvent
    {
        public double Duration { get { return GetDoubleAt(0); } }
        public double Throughput { get { return GetDoubleAt(8); } }
        public double ThreadWave { get { return GetDoubleAt(16); } }
        public double ThroughputWave { get { return GetDoubleAt(24); } }
        public double ThroughputErrorEstimate { get { return GetDoubleAt(32); } }
        public double AverageThroughputErrorEstimate { get { return GetDoubleAt(40); } }
        public double ThroughputRatio { get { return GetDoubleAt(48); } }
        public double Confidence { get { return GetDoubleAt(56); } }
        public double NewControlSetting { get { return GetDoubleAt(64); } }
        public int NewThreadWaveMagnitude { get { return GetInt16At(72); } }
        public int ClrInstanceID { get { return GetInt16At(74); } }

        #region Private
        internal ThreadPoolWorkerThreadAdjustmentStatsTraceData(Action<ThreadPoolWorkerThreadAdjustmentStatsTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 76));
            Debug.Assert(!(Version > 0 && EventDataLength < 76));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("Duration", Duration);
            sb.XmlAttrib("Throughput", Throughput);
            sb.XmlAttrib("ThreadWave", ThreadWave);
            sb.XmlAttrib("ThroughputWave", ThroughputWave);
            sb.XmlAttrib("ThroughputErrorEstimate", ThroughputErrorEstimate);
            sb.XmlAttrib("AverageThroughputErrorEstimate", AverageThroughputErrorEstimate);
            sb.XmlAttrib("ThroughputRatio", ThroughputRatio);
            sb.XmlAttrib("Confidence", Confidence);
            sb.XmlAttrib("NewControlSetting", NewControlSetting);
            sb.XmlAttrib("NewThreadWaveMagnitude", NewThreadWaveMagnitude);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Duration", "Throughput", "ThreadWave", "ThroughputWave", "ThroughputErrorEstimate", "AverageThroughputErrorEstimate", "ThroughputRatio", "Confidence", "NewControlSetting", "NewThreadWaveMagnitude", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Duration;
                case 1:
                    return Throughput;
                case 2:
                    return ThreadWave;
                case 3:
                    return ThroughputWave;
                case 4:
                    return ThroughputErrorEstimate;
                case 5:
                    return AverageThroughputErrorEstimate;
                case 6:
                    return ThroughputRatio;
                case 7:
                    return Confidence;
                case 8:
                    return NewControlSetting;
                case 9:
                    return NewThreadWaveMagnitude;
                case 10:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ThreadPoolWorkerThreadAdjustmentStatsTraceData> Action;
        #endregion
    }
    public sealed class ExceptionTraceData : TraceEvent
    {
        public string ExceptionType { get { if (Version >= 1) return GetUnicodeStringAt(0); return ""; } }
        public string ExceptionMessage { get { if (Version >= 1) return GetUnicodeStringAt(SkipUnicodeString(0)); return ""; } }
        public Address ExceptionEIP { get { if (Version >= 1) return GetHostPointer(SkipUnicodeString(SkipUnicodeString(0))); return 0; } }
        public int ExceptionHRESULT { get { if (Version >= 1) return GetInt32At(HostOffset(SkipUnicodeString(SkipUnicodeString(0)) + 4, 1)); return 0; } }
        public ExceptionThrownFlags ExceptionFlags { get { if (Version >= 1) return (ExceptionThrownFlags)GetInt16At(HostOffset(SkipUnicodeString(SkipUnicodeString(0)) + 8, 1)); return (ExceptionThrownFlags)0; } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(HostOffset(SkipUnicodeString(SkipUnicodeString(0)) + 10, 1)); return 0; } }

        #region Private
        internal ExceptionTraceData(Action<ExceptionTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 1 && EventDataLength != HostOffset(SkipUnicodeString(SkipUnicodeString(0)) + 12, 1)));
            Debug.Assert(!(Version > 1 && EventDataLength < HostOffset(SkipUnicodeString(SkipUnicodeString(0)) + 12, 1)));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("ExceptionType", ExceptionType);
            sb.XmlAttrib("ExceptionMessage", ExceptionMessage);
            sb.XmlAttribHex("ExceptionEIP", ExceptionEIP);
            sb.XmlAttribHex("ExceptionHRESULT", ExceptionHRESULT);
            sb.XmlAttrib("ExceptionFlags", ExceptionFlags);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ExceptionType", "ExceptionMessage", "ExceptionEIP", "ExceptionHRESULT", "ExceptionFlags", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ExceptionType;
                case 1:
                    return ExceptionMessage;
                case 2:
                    return ExceptionEIP;
                case 3:
                    return ExceptionHRESULT;
                case 4:
                    return ExceptionFlags;
                case 5:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ExceptionTraceData> Action;
        #endregion
    }
    public sealed class ContentionTraceData : TraceEvent
    {
        public ContentionFlags ContentionFlags { get { if (Version >= 1) return (ContentionFlags)GetByteAt(0); return (ContentionFlags)0; } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(1); return 0; } }

        #region Private
        internal ContentionTraceData(Action<ContentionTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 1 && EventDataLength != 3));
            Debug.Assert(!(Version > 1 && EventDataLength < 3));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("ContentionFlags", ContentionFlags);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ContentionFlags", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ContentionFlags;
                case 1:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ContentionTraceData> Action;
        #endregion
    }

    public sealed class MethodILToNativeMapTraceData : TraceEvent
    {
        const int ILProlog = -2;    // Returned by ILOffset to represent the prolog of the method
        const int ILEpilog = -3;    // Returned by ILOffset to represent the epilog of the method

        public long MethodID { get { return GetInt64At(0); } }
        public long ReJITID { get { return GetInt64At(8); } }
        public int MethodExtent { get { return GetByteAt(16); } }
        public int CountOfMapEntries { get { return GetInt16At(17); } }
        // May also return the special values ILProlog (-2) and ILEpilog (-3) 
        public int ILOffset(int i) { return GetInt32At(i * 4 + 19); }
        unsafe internal int* ILOffsets { get { return (int*)(((byte*)DataStart) + 19); } }

        public int NativeOffset(int i) { return GetInt32At((CountOfMapEntries + i) * 4 + 19); }
        unsafe internal int* NativeOffsets { get { return (int*)(((byte*)DataStart) + CountOfMapEntries * 4 + 19); } }
        public int ClrInstanceID { get { return GetInt16At(CountOfMapEntries * 8 + 19); } }

        #region Private
        internal MethodILToNativeMapTraceData(Action<MethodILToNativeMapTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(Version != 0 || EventDataLength == CountOfMapEntries * 8 + 21);
            Debug.Assert(Version > 0 || EventDataLength >= CountOfMapEntries * 8 + 21);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("MethodID", MethodID);
            sb.XmlAttrib("ReJITID", ReJITID);
            sb.XmlAttrib("MethodExtent", MethodExtent);
            sb.XmlAttrib("CountOfMapEntries", CountOfMapEntries);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.AppendLine(">");
            for (int i = 0; i < CountOfMapEntries; i++)
                sb.Append("  ").Append(ILOffset(i)).Append("->").Append(NativeOffset(i)).AppendLine();
            sb.Append("</Event>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "MethodID", "ReJITID", "MethodExtent", "CountOfMapEntries", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return MethodID;
                case 1:
                    return ReJITID;
                case 2:
                    return MethodExtent;
                case 3:
                    return CountOfMapEntries;
                case 4:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<MethodILToNativeMapTraceData> Action;
        #endregion
    }

    public sealed class ClrStackWalkTraceData : TraceEvent
    {
        public int ClrInstanceID { get { return GetInt16At(0); } }
        // Skipping Reserved1
        // Skipping Reserved2
        public int FrameCount { get { return GetInt32At(4); } }
        /// <summary>
        /// Fetches the instruction pointer of a eventToStack frame 0 is the deepest frame, and the maximum should
        /// be a thread offset routine (if you get a complete eventToStack).  
        /// </summary>
        /// <param name="i">The index of the frame to fetch.  0 is the CPU EIP, 1 is the Caller of that
        /// routine ...</param>
        /// <returns>The instruction pointer of the specified frame.</returns>
        public Address InstructionPointer(int i)
        {
            Debug.Assert(0 <= i && i < FrameCount);
            return GetHostPointer(8 + i * PointerSize);
        }

        /// <summary>
        /// Access to the instruction pointers as a unsafe memory blob
        /// </summary>
        unsafe internal void* InstructionPointers { get { return ((byte*)DataStart) + 8; } }

        #region Private
        internal ClrStackWalkTraceData(Action<ClrStackWalkTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(EventDataLength < 6));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb).XmlAttrib("ClrInstanceID", ClrInstanceID).XmlAttrib("FrameCount", FrameCount).AppendLine(">");
            for (int i = 0; i < FrameCount; i++)
            {
                sb.Append("  ");
                sb.Append("0x").Append(((ulong)InstructionPointer(i)).ToString("x"));
            }
            sb.AppendLine();
            sb.Append("</Event>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ClrInstanceID", "FrameCount" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ClrInstanceID;
                case 1:
                    return FrameCount;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ClrStackWalkTraceData> Action;
        #endregion
    }
    public sealed class AppDomainMemAllocatedTraceData : TraceEvent
    {
        public long AppDomainID { get { return GetInt64At(0); } }
        public long Allocated { get { return GetInt64At(8); } }
        public int ClrInstanceID { get { return GetInt16At(16); } }

        #region Private
        internal AppDomainMemAllocatedTraceData(Action<AppDomainMemAllocatedTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 18));
            Debug.Assert(!(Version > 0 && EventDataLength < 18));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("AppDomainID", AppDomainID);
            sb.XmlAttribHex("Allocated", Allocated);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "AppDomainID", "Allocated", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return AppDomainID;
                case 1:
                    return Allocated;
                case 2:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<AppDomainMemAllocatedTraceData> Action;
        #endregion
    }
    public sealed class AppDomainMemSurvivedTraceData : TraceEvent
    {
        public long AppDomainID { get { return GetInt64At(0); } }
        public long Survived { get { return GetInt64At(8); } }
        public long ProcessSurvived { get { return GetInt64At(16); } }
        public int ClrInstanceID { get { return GetInt16At(24); } }

        #region Private
        internal AppDomainMemSurvivedTraceData(Action<AppDomainMemSurvivedTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 26));
            Debug.Assert(!(Version > 0 && EventDataLength < 26));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("AppDomainID", AppDomainID);
            sb.XmlAttribHex("Survived", Survived);
            sb.XmlAttribHex("ProcessSurvived", ProcessSurvived);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "AppDomainID", "Survived", "ProcessSurvived", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return AppDomainID;
                case 1:
                    return Survived;
                case 2:
                    return ProcessSurvived;
                case 3:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<AppDomainMemSurvivedTraceData> Action;
        #endregion
    }
    public sealed class ThreadCreatedTraceData : TraceEvent
    {
        public long ManagedThreadID { get { return GetInt64At(0); } }
        public long AppDomainID { get { return GetInt64At(8); } }
        public int Flags { get { return GetInt32At(16); } }
        public int ManagedThreadIndex { get { return GetInt32At(20); } }
        public int OSThreadID { get { return GetInt32At(24); } }
        public int ClrInstanceID { get { return GetInt16At(28); } }

        #region Private
        internal ThreadCreatedTraceData(Action<ThreadCreatedTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 30));
            Debug.Assert(!(Version > 0 && EventDataLength < 30));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("ManagedThreadID", ManagedThreadID);
            sb.XmlAttribHex("AppDomainID", AppDomainID);
            sb.XmlAttribHex("Flags", Flags);
            sb.XmlAttrib("ManagedThreadIndex", ManagedThreadIndex);
            sb.XmlAttrib("OSThreadID", OSThreadID);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ManagedThreadID", "AppDomainID", "Flags", "ManagedThreadIndex", "OSThreadID", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ManagedThreadID;
                case 1:
                    return AppDomainID;
                case 2:
                    return Flags;
                case 3:
                    return ManagedThreadIndex;
                case 4:
                    return OSThreadID;
                case 5:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ThreadCreatedTraceData> Action;
        #endregion
    }
    public sealed class ThreadTerminatedOrTransitionTraceData : TraceEvent
    {
        public long ManagedThreadID { get { return GetInt64At(0); } }
        public long AppDomainID { get { return GetInt64At(8); } }
        public int ClrInstanceID { get { return GetInt16At(16); } }

        #region Private
        internal ThreadTerminatedOrTransitionTraceData(Action<ThreadTerminatedOrTransitionTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 18));
            Debug.Assert(!(Version > 0 && EventDataLength < 18));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("ManagedThreadID", ManagedThreadID);
            sb.XmlAttribHex("AppDomainID", AppDomainID);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ManagedThreadID", "AppDomainID", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ManagedThreadID;
                case 1:
                    return AppDomainID;
                case 2:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ThreadTerminatedOrTransitionTraceData> Action;
        #endregion
    }
    public sealed class ILStubGeneratedTraceData : TraceEvent
    {
        public int ClrInstanceID { get { return GetInt16At(0); } }
        public long ModuleID { get { return GetInt64At(2); } }
        public long StubMethodID { get { return GetInt64At(10); } }
        public ILStubGeneratedFlags StubFlags { get { return (ILStubGeneratedFlags)GetInt32At(18); } }
        public int ManagedInteropMethodToken { get { return GetInt32At(22); } }
        public string ManagedInteropMethodNamespace { get { return GetUnicodeStringAt(26); } }
        public string ManagedInteropMethodName { get { return GetUnicodeStringAt(SkipUnicodeString(26)); } }
        public string ManagedInteropMethodSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(26))); } }
        public string NativeMethodSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(26)))); } }
        public string StubMethodSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(26))))); } }
        public string StubMethodILCode { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(26)))))); } }

        #region Private
        internal ILStubGeneratedTraceData(Action<ILStubGeneratedTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(26))))))));
            Debug.Assert(!(Version > 0 && EventDataLength < SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(26))))))));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.XmlAttribHex("ModuleID", ModuleID);
            sb.XmlAttribHex("StubMethodID", StubMethodID);
            sb.XmlAttrib("StubFlags", StubFlags);
            sb.XmlAttribHex("ManagedInteropMethodToken", ManagedInteropMethodToken);
            sb.XmlAttrib("ManagedInteropMethodNamespace", ManagedInteropMethodNamespace);
            sb.XmlAttrib("ManagedInteropMethodName", ManagedInteropMethodName);
            sb.XmlAttrib("ManagedInteropMethodSignature", ManagedInteropMethodSignature);
            sb.XmlAttrib("NativeMethodSignature", NativeMethodSignature);
            sb.XmlAttrib("StubMethodSignature", StubMethodSignature);
            sb.XmlAttrib("StubMethodILCode", StubMethodILCode);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ClrInstanceID", "ModuleID", "StubMethodID", "StubFlags", "ManagedInteropMethodToken", "ManagedInteropMethodNamespace", "ManagedInteropMethodName", "ManagedInteropMethodSignature", "NativeMethodSignature", "StubMethodSignature", "StubMethodILCode" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ClrInstanceID;
                case 1:
                    return ModuleID;
                case 2:
                    return StubMethodID;
                case 3:
                    return StubFlags;
                case 4:
                    return ManagedInteropMethodToken;
                case 5:
                    return ManagedInteropMethodNamespace;
                case 6:
                    return ManagedInteropMethodName;
                case 7:
                    return ManagedInteropMethodSignature;
                case 8:
                    return NativeMethodSignature;
                case 9:
                    return StubMethodSignature;
                case 10:
                    return StubMethodILCode;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ILStubGeneratedTraceData> Action;
        #endregion
    }
    public sealed class ILStubCacheHitTraceData : TraceEvent
    {
        public int ClrInstanceID { get { return GetInt16At(0); } }
        public long ModuleID { get { return GetInt64At(2); } }
        public long StubMethodID { get { return GetInt64At(10); } }
        public int ManagedInteropMethodToken { get { return GetInt32At(18); } }
        public string ManagedInteropMethodNamespace { get { return GetUnicodeStringAt(22); } }
        public string ManagedInteropMethodName { get { return GetUnicodeStringAt(SkipUnicodeString(22)); } }
        public string ManagedInteropMethodSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(22))); } }

        #region Private
        internal ILStubCacheHitTraceData(Action<ILStubCacheHitTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(22)))));
            Debug.Assert(!(Version > 0 && EventDataLength < SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(22)))));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.XmlAttribHex("ModuleID", ModuleID);
            sb.XmlAttribHex("StubMethodID", StubMethodID);
            sb.XmlAttribHex("ManagedInteropMethodToken", ManagedInteropMethodToken);
            sb.XmlAttrib("ManagedInteropMethodNamespace", ManagedInteropMethodNamespace);
            sb.XmlAttrib("ManagedInteropMethodName", ManagedInteropMethodName);
            sb.XmlAttrib("ManagedInteropMethodSignature", ManagedInteropMethodSignature);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ClrInstanceID", "ModuleID", "StubMethodID", "ManagedInteropMethodToken", "ManagedInteropMethodNamespace", "ManagedInteropMethodName", "ManagedInteropMethodSignature" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ClrInstanceID;
                case 1:
                    return ModuleID;
                case 2:
                    return StubMethodID;
                case 3:
                    return ManagedInteropMethodToken;
                case 4:
                    return ManagedInteropMethodNamespace;
                case 5:
                    return ManagedInteropMethodName;
                case 6:
                    return ManagedInteropMethodSignature;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ILStubCacheHitTraceData> Action;
        #endregion
    }

    public sealed class MethodLoadUnloadTraceData : TraceEvent
    {
        public long MethodID { get { return GetInt64At(0); } }
        public long ModuleID { get { return GetInt64At(8); } }
        public Address MethodStartAddress { get { return (Address)GetInt64At(16); } }
        public int MethodSize { get { return GetInt32At(24); } }
        public int MethodToken { get { return GetInt32At(28); } }
        public MethodFlags MethodFlags { get { return (MethodFlags)(GetInt32At(32) & 0xFFFFFFF); } }
        public bool IsDynamic { get { return (MethodFlags & MethodFlags.Dynamic) != 0; } }
        public bool IsGeneric { get { return (MethodFlags & MethodFlags.Generic) != 0; } }
        public bool IsJitted { get { return (MethodFlags & MethodFlags.Jitted) != 0; } }
        public int MethodExtent { get { return GetInt32At(32) >> 28; } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(36); return 0; } }

        #region Private
        internal MethodLoadUnloadTraceData(Action<MethodLoadUnloadTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 36));
            Debug.Assert(!(Version == 1 && EventDataLength != 38));
            Debug.Assert(!(Version > 1 && EventDataLength < 38));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("MethodID", MethodID);
            sb.XmlAttribHex("ModuleID", ModuleID);
            sb.XmlAttribHex("MethodStartAddress", MethodStartAddress);
            sb.XmlAttribHex("MethodSize", MethodSize);
            sb.XmlAttribHex("MethodToken", MethodToken);
            sb.XmlAttrib("MethodFlags", MethodFlags);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "MethodID", "ModuleID", "MethodStartAddress", "MethodSize", "MethodToken", "MethodFlags", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return MethodID;
                case 1:
                    return ModuleID;
                case 2:
                    return MethodStartAddress;
                case 3:
                    return MethodSize;
                case 4:
                    return MethodToken;
                case 5:
                    return MethodFlags;
                case 6:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<MethodLoadUnloadTraceData> Action;
        #endregion
    }
    public sealed class MethodLoadUnloadVerboseTraceData : TraceEvent
    {
        public long MethodID { get { return GetInt64At(0); } }
        public long ModuleID { get { return GetInt64At(8); } }
        public Address MethodStartAddress { get { return (Address)GetInt64At(16); } }
        public int MethodSize { get { return GetInt32At(24); } }
        public int MethodToken { get { return GetInt32At(28); } }
        public MethodFlags MethodFlags { get { return (MethodFlags)(GetInt32At(32) & 0xFFFFFFF); } }
        public bool IsDynamic { get { return (MethodFlags & MethodFlags.Dynamic) != 0; } }
        public bool IsGeneric { get { return (MethodFlags & MethodFlags.Generic) != 0; } }
        public bool IsJitted { get { return (MethodFlags & MethodFlags.Jitted) != 0; } }
        public int MethodExtent { get { return GetInt32At(32) >> 28; } }
        public string MethodNamespace { get { return GetUnicodeStringAt(36); } }
        public string MethodName { get { return GetUnicodeStringAt(SkipUnicodeString(36)); } }
        public string MethodSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(36))); } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(36)))); return 0; } }

        #region Private
        internal MethodLoadUnloadVerboseTraceData(Action<MethodLoadUnloadVerboseTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(36)))));
            Debug.Assert(!(Version == 1 && EventDataLength != SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(36))) + 2));
            Debug.Assert(!(Version > 1 && EventDataLength < SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(36))) + 2));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("MethodID", MethodID);
            sb.XmlAttribHex("ModuleID", ModuleID);
            sb.XmlAttribHex("MethodStartAddress", MethodStartAddress);
            sb.XmlAttribHex("MethodSize", MethodSize);
            sb.XmlAttribHex("MethodToken", MethodToken);
            sb.XmlAttrib("MethodFlags", MethodFlags);
            sb.XmlAttrib("MethodNamespace", MethodNamespace);
            sb.XmlAttrib("MethodName", MethodName);
            sb.XmlAttrib("MethodSignature", MethodSignature);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "MethodID", "ModuleID", "MethodStartAddress", "MethodSize", "MethodToken", "MethodFlags", "MethodNamespace", "MethodName", "MethodSignature", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return MethodID;
                case 1:
                    return ModuleID;
                case 2:
                    return MethodStartAddress;
                case 3:
                    return MethodSize;
                case 4:
                    return MethodToken;
                case 5:
                    return MethodFlags;
                case 6:
                    return MethodNamespace;
                case 7:
                    return MethodName;
                case 8:
                    return MethodSignature;
                case 9:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<MethodLoadUnloadVerboseTraceData> Action;
        #endregion
    }

    public sealed class MethodJittingStartedTraceData : TraceEvent
    {
        public long MethodID { get { return GetInt64At(0); } }
        public long ModuleID { get { return GetInt64At(8); } }
        public int MethodToken { get { return GetInt32At(16); } }
        public int MethodILSize { get { return GetInt32At(20); } }
        public string MethodNamespace { get { return GetUnicodeStringAt(24); } }
        public string MethodName { get { return GetUnicodeStringAt(SkipUnicodeString(24)); } }
        public string MethodSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(24))); } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(24)))); return 0; } }

        #region Private
        internal MethodJittingStartedTraceData(Action<MethodJittingStartedTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(24)))));
            Debug.Assert(!(Version == 1 && EventDataLength != SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(24))) + 2));
            Debug.Assert(!(Version > 1 && EventDataLength < SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(24))) + 2));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("MethodID", MethodID);
            sb.XmlAttribHex("ModuleID", ModuleID);
            sb.XmlAttribHex("MethodToken", MethodToken);
            sb.XmlAttribHex("MethodILSize", MethodILSize);
            sb.XmlAttrib("MethodNamespace", MethodNamespace);
            sb.XmlAttrib("MethodName", MethodName);
            sb.XmlAttrib("MethodSignature", MethodSignature);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "MethodID", "ModuleID", "MethodToken", "MethodILSize", "MethodNamespace", "MethodName", "MethodSignature", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return MethodID;
                case 1:
                    return ModuleID;
                case 2:
                    return MethodToken;
                case 3:
                    return MethodILSize;
                case 4:
                    return MethodNamespace;
                case 5:
                    return MethodName;
                case 6:
                    return MethodSignature;
                case 7:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<MethodJittingStartedTraceData> Action;
        #endregion
    }
    public sealed class ModuleLoadUnloadTraceData : TraceEvent
    {
        public long ModuleID { get { return GetInt64At(0); } }
        public long AssemblyID { get { return GetInt64At(8); } }
        public ModuleFlags ModuleFlags { get { return (ModuleFlags)GetInt32At(16); } }
        // Skipping Reserved1
        public string ModuleILPath { get { return GetUnicodeStringAt(24); } }
        public string ModuleNativePath { get { return GetUnicodeStringAt(SkipUnicodeString(24)); } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(SkipUnicodeString(SkipUnicodeString(24))); return 0; } }
        public Guid ManagedPdbSignature { get { if (Version >= 2) return GetGuidAt(SkipUnicodeString(SkipUnicodeString(24)) + 2); return Guid.Empty; } }
        public int ManagedPdbAge { get { if (Version >= 2) return GetInt32At(SkipUnicodeString(SkipUnicodeString(24)) + 18); return 0; } }
        public string ManagedPdbBuildPath { get { if (Version >= 2) return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(24)) + 22); return ""; } }

        public Guid NativePdbSignature { get { if (Version >= 2) return GetGuidAt(GetNativePdbSigStart); return Guid.Empty; } }
        public int NativePdbAge { get { if (Version >= 2) return GetInt32At(GetNativePdbSigStart + 16); return 0; } }
        public string NativePdbBuildPath { get { if (Version >= 2) return GetUnicodeStringAt(GetNativePdbSigStart + 20); return ""; } }

        /// <summary>
        /// This is simply the file name part of the ModuleILPath.  It is a convinience method. 
        /// </summary>
        public string ModuleILFileName { get { return System.IO.Path.GetFileName(ModuleILPath); } }
        #region Private
        internal ModuleLoadUnloadTraceData(Action<ModuleLoadUnloadTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }

        int GetNativePdbSigStart { get { return SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(24)) + 22); } }

        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(SkipUnicodeString(24))));
            Debug.Assert(!(Version == 1 && EventDataLength != SkipUnicodeString(SkipUnicodeString(24)) + 2));
            Debug.Assert(!(Version == 2 && EventDataLength != SkipUnicodeString(GetNativePdbSigStart + 20)));
            Debug.Assert(!(Version > 2 && EventDataLength < SkipUnicodeString(GetNativePdbSigStart + 20)));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("ModuleID", ModuleID);
            sb.XmlAttribHex("AssemblyID", AssemblyID);
            sb.XmlAttrib("ModuleFlags", ModuleFlags);
            sb.XmlAttrib("ModuleILPath", ModuleILPath);
            sb.XmlAttrib("ModuleNativePath", ModuleNativePath);
            if (ManagedPdbSignature != Guid.Empty)
                sb.XmlAttrib("ManagedPdbSignature", ManagedPdbSignature);
            if (ManagedPdbAge != 0)
                sb.XmlAttrib("ManagedPdbAge", ManagedPdbAge);
            if (ManagedPdbBuildPath.Length != 0)
                sb.XmlAttrib("ManagedPdbBuildPath", ManagedPdbBuildPath);
            if (NativePdbSignature != Guid.Empty)
                sb.XmlAttrib("NativePdbSignature", NativePdbSignature);
            if (NativePdbAge != 0)
                sb.XmlAttrib("NativePdbAge", NativePdbAge);
            if (NativePdbBuildPath.Length != 0)
                sb.XmlAttrib("NativePdbBuildPath", NativePdbBuildPath);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ModuleID", "AssemblyID", "ModuleFlags", "ModuleILPath", "ModuleNativePath",
                        "ManagedPdbSignature", "ManagedPdbAge", "ManagedPdbBuildPath",
                        "NativePdbSignature", "NativePdbAge", "NativePdbBuildPath", "ModuleILFileName" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ModuleID;
                case 1:
                    return AssemblyID;
                case 2:
                    return ModuleFlags;
                case 3:
                    return ModuleILPath;
                case 4:
                    return ModuleNativePath;
                case 5:
                    return ManagedPdbSignature;
                case 6:
                    return ManagedPdbAge;
                case 7:
                    return ManagedPdbBuildPath;
                case 8:
                    return NativePdbSignature;
                case 9:
                    return NativePdbAge;
                case 10:
                    return NativePdbBuildPath;
                case 11:
                    return ModuleILFileName;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ModuleLoadUnloadTraceData> Action;
        #endregion
    }
    public sealed class DomainModuleLoadUnloadTraceData : TraceEvent
    {
        public long ModuleID { get { return GetInt64At(0); } }
        public long AssemblyID { get { return GetInt64At(8); } }
        public long AppDomainID { get { return GetInt64At(16); } }
        public ModuleFlags ModuleFlags { get { return (ModuleFlags)GetInt32At(24); } }
        // Skipping Reserved1
        public string ModuleILPath { get { return GetUnicodeStringAt(32); } }
        public string ModuleNativePath { get { return GetUnicodeStringAt(SkipUnicodeString(32)); } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(SkipUnicodeString(SkipUnicodeString(32))); return 0; } }

        #region Private
        internal DomainModuleLoadUnloadTraceData(Action<DomainModuleLoadUnloadTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(SkipUnicodeString(32))));
            Debug.Assert(!(Version == 1 && EventDataLength != SkipUnicodeString(SkipUnicodeString(32)) + 2));
            Debug.Assert(!(Version > 1 && EventDataLength < SkipUnicodeString(SkipUnicodeString(32)) + 2));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("ModuleID", ModuleID);
            sb.XmlAttribHex("AssemblyID", AssemblyID);
            sb.XmlAttribHex("AppDomainID", AppDomainID);
            sb.XmlAttrib("ModuleFlags", ModuleFlags);
            sb.XmlAttrib("ModuleILPath", ModuleILPath);
            sb.XmlAttrib("ModuleNativePath", ModuleNativePath);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ModuleID", "AssemblyID", "AppDomainID", "ModuleFlags", "ModuleILPath", "ModuleNativePath", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ModuleID;
                case 1:
                    return AssemblyID;
                case 2:
                    return AppDomainID;
                case 3:
                    return ModuleFlags;
                case 4:
                    return ModuleILPath;
                case 5:
                    return ModuleNativePath;
                case 6:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<DomainModuleLoadUnloadTraceData> Action;
        #endregion
    }
    public sealed class AssemblyLoadUnloadTraceData : TraceEvent
    {
        public long AssemblyID { get { return GetInt64At(0); } }
        public long AppDomainID { get { return GetInt64At(8); } }
        public AssemblyFlags AssemblyFlags { get { if (Version >= 1) return (AssemblyFlags)GetInt32At(24); return (AssemblyFlags)GetInt32At(16); } }
        public string FullyQualifiedAssemblyName { get { if (Version >= 1) return GetUnicodeStringAt(28); return GetUnicodeStringAt(20); } }
        public long BindingID { get { if (Version >= 1) return GetInt64At(16); return 0; } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(SkipUnicodeString(28)); return 0; } }

        #region Private
        internal AssemblyLoadUnloadTraceData(Action<AssemblyLoadUnloadTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(20)));
            Debug.Assert(!(Version == 1 && EventDataLength != SkipUnicodeString(28) + 2));
            Debug.Assert(!(Version > 1 && EventDataLength < SkipUnicodeString(28) + 2));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("AssemblyID", AssemblyID);
            sb.XmlAttribHex("AppDomainID", AppDomainID);
            sb.XmlAttrib("AssemblyFlags", AssemblyFlags);
            sb.XmlAttrib("FullyQualifiedAssemblyName", FullyQualifiedAssemblyName);
            sb.XmlAttribHex("BindingID", BindingID);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "AssemblyID", "AppDomainID", "AssemblyFlags", "FullyQualifiedAssemblyName", "BindingID", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return AssemblyID;
                case 1:
                    return AppDomainID;
                case 2:
                    return AssemblyFlags;
                case 3:
                    return FullyQualifiedAssemblyName;
                case 4:
                    return BindingID;
                case 5:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<AssemblyLoadUnloadTraceData> Action;
        #endregion
    }
    public sealed class AppDomainLoadUnloadTraceData : TraceEvent
    {
        public long AppDomainID { get { return GetInt64At(0); } }
        public AppDomainFlags AppDomainFlags { get { return (AppDomainFlags)GetInt32At(8); } }
        public string AppDomainName { get { return GetUnicodeStringAt(12); } }
        public int AppDomainIndex { get { if (Version >= 1) return GetInt32At(SkipUnicodeString(12)); return 0; } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(SkipUnicodeString(12) + 4); return 0; } }

        #region Private
        internal AppDomainLoadUnloadTraceData(Action<AppDomainLoadUnloadTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(12)));
            Debug.Assert(!(Version == 1 && EventDataLength != SkipUnicodeString(12) + 6));
            Debug.Assert(!(Version > 1 && EventDataLength < SkipUnicodeString(12) + 6));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("AppDomainID", AppDomainID);
            sb.XmlAttrib("AppDomainFlags", AppDomainFlags);
            sb.XmlAttrib("AppDomainName", AppDomainName);
            sb.XmlAttrib("AppDomainIndex", AppDomainIndex);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "AppDomainID", "AppDomainFlags", "AppDomainName", "AppDomainIndex", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return AppDomainID;
                case 1:
                    return AppDomainFlags;
                case 2:
                    return AppDomainName;
                case 3:
                    return AppDomainIndex;
                case 4:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<AppDomainLoadUnloadTraceData> Action;
        #endregion
    }
    public sealed class StrongNameVerificationTraceData : TraceEvent
    {
        public int VerificationFlags { get { return GetInt32At(0); } }
        public int ErrorCode { get { return GetInt32At(4); } }
        public string FullyQualifiedAssemblyName { get { return GetUnicodeStringAt(8); } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(SkipUnicodeString(8)); return 0; } }

        #region Private
        internal StrongNameVerificationTraceData(Action<StrongNameVerificationTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(8)));
            Debug.Assert(!(Version == 1 && EventDataLength != SkipUnicodeString(8) + 2));
            Debug.Assert(!(Version > 1 && EventDataLength < SkipUnicodeString(8) + 2));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("VerificationFlags", VerificationFlags);
            sb.XmlAttribHex("ErrorCode", ErrorCode);
            sb.XmlAttrib("FullyQualifiedAssemblyName", FullyQualifiedAssemblyName);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "VerificationFlags", "ErrorCode", "FullyQualifiedAssemblyName", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return VerificationFlags;
                case 1:
                    return ErrorCode;
                case 2:
                    return FullyQualifiedAssemblyName;
                case 3:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<StrongNameVerificationTraceData> Action;
        #endregion
    }
    public sealed class AuthenticodeVerificationTraceData : TraceEvent
    {
        public int VerificationFlags { get { return GetInt32At(0); } }
        public int ErrorCode { get { return GetInt32At(4); } }
        public string ModulePath { get { return GetUnicodeStringAt(8); } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(SkipUnicodeString(8)); return 0; } }

        #region Private
        internal AuthenticodeVerificationTraceData(Action<AuthenticodeVerificationTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(8)));
            Debug.Assert(!(Version == 1 && EventDataLength != SkipUnicodeString(8) + 2));
            Debug.Assert(!(Version > 1 && EventDataLength < SkipUnicodeString(8) + 2));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("VerificationFlags", VerificationFlags);
            sb.XmlAttribHex("ErrorCode", ErrorCode);
            sb.XmlAttrib("ModulePath", ModulePath);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "VerificationFlags", "ErrorCode", "ModulePath", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return VerificationFlags;
                case 1:
                    return ErrorCode;
                case 2:
                    return ModulePath;
                case 3:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<AuthenticodeVerificationTraceData> Action;
        #endregion
    }
    public sealed class MethodJitInliningSucceededTraceData : TraceEvent
    {
        public string MethodBeingCompiledNamespace { get { return GetUnicodeStringAt(0); } }
        public string MethodBeingCompiledName { get { return GetUnicodeStringAt(SkipUnicodeString(0)); } }
        public string MethodBeingCompiledNameSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(0))); } }
        public string InlinerNamespace { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0)))); } }
        public string InlinerName { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))); } }
        public string InlinerNameSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0)))))); } }
        public string InlineeNamespace { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))); } }
        public string InlineeName { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0)))))))); } }
        public string InlineeNameSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))); } }
        public int ClrInstanceID { get { return GetInt16At(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0)))))))))); } }

        #region Private
        internal MethodJitInliningSucceededTraceData(Action<MethodJitInliningSucceededTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))) + 2));
            Debug.Assert(!(Version > 0 && EventDataLength < SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))) + 2));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("MethodBeingCompiledNamespace", MethodBeingCompiledNamespace);
            sb.XmlAttrib("MethodBeingCompiledName", MethodBeingCompiledName);
            sb.XmlAttrib("MethodBeingCompiledNameSignature", MethodBeingCompiledNameSignature);
            sb.XmlAttrib("InlinerNamespace", InlinerNamespace);
            sb.XmlAttrib("InlinerName", InlinerName);
            sb.XmlAttrib("InlinerNameSignature", InlinerNameSignature);
            sb.XmlAttrib("InlineeNamespace", InlineeNamespace);
            sb.XmlAttrib("InlineeName", InlineeName);
            sb.XmlAttrib("InlineeNameSignature", InlineeNameSignature);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "MethodBeingCompiledNamespace", "MethodBeingCompiledName", "MethodBeingCompiledNameSignature", "InlinerNamespace", "InlinerName", "InlinerNameSignature", "InlineeNamespace", "InlineeName", "InlineeNameSignature", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return MethodBeingCompiledNamespace;
                case 1:
                    return MethodBeingCompiledName;
                case 2:
                    return MethodBeingCompiledNameSignature;
                case 3:
                    return InlinerNamespace;
                case 4:
                    return InlinerName;
                case 5:
                    return InlinerNameSignature;
                case 6:
                    return InlineeNamespace;
                case 7:
                    return InlineeName;
                case 8:
                    return InlineeNameSignature;
                case 9:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<MethodJitInliningSucceededTraceData> Action;
        #endregion
    }
    public sealed class MethodJitInliningFailedTraceData : TraceEvent
    {
        public string MethodBeingCompiledNamespace { get { return GetUnicodeStringAt(0); } }
        public string MethodBeingCompiledName { get { return GetUnicodeStringAt(SkipUnicodeString(0)); } }
        public string MethodBeingCompiledNameSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(0))); } }
        public string InlinerNamespace { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0)))); } }
        public string InlinerName { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))); } }
        public string InlinerNameSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0)))))); } }
        public string InlineeNamespace { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))); } }
        public string InlineeName { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0)))))))); } }
        public string InlineeNameSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))); } }
        public bool FailAlways { get { return GetInt32At(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0)))))))))) != 0; } }
        public string FailReason { get { return GetUTF8StringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))) + 4); } }
        public int ClrInstanceID { get { return GetInt16At(SkipUTF8String(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))) + 4)); } }

        #region Private
        internal MethodJitInliningFailedTraceData(Action<MethodJitInliningFailedTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUTF8String(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))) + 4) + 2));
            Debug.Assert(!(Version > 0 && EventDataLength < SkipUTF8String(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))) + 4) + 2));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("MethodBeingCompiledNamespace", MethodBeingCompiledNamespace);
            sb.XmlAttrib("MethodBeingCompiledName", MethodBeingCompiledName);
            sb.XmlAttrib("MethodBeingCompiledNameSignature", MethodBeingCompiledNameSignature);
            sb.XmlAttrib("InlinerNamespace", InlinerNamespace);
            sb.XmlAttrib("InlinerName", InlinerName);
            sb.XmlAttrib("InlinerNameSignature", InlinerNameSignature);
            sb.XmlAttrib("InlineeNamespace", InlineeNamespace);
            sb.XmlAttrib("InlineeName", InlineeName);
            sb.XmlAttrib("InlineeNameSignature", InlineeNameSignature);
            sb.XmlAttrib("FailAlways", FailAlways);
            sb.XmlAttrib("FailReason", FailReason);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "MethodBeingCompiledNamespace", "MethodBeingCompiledName", "MethodBeingCompiledNameSignature", "InlinerNamespace", "InlinerName", "InlinerNameSignature", "InlineeNamespace", "InlineeName", "InlineeNameSignature", "FailAlways", "FailReason", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return MethodBeingCompiledNamespace;
                case 1:
                    return MethodBeingCompiledName;
                case 2:
                    return MethodBeingCompiledNameSignature;
                case 3:
                    return InlinerNamespace;
                case 4:
                    return InlinerName;
                case 5:
                    return InlinerNameSignature;
                case 6:
                    return InlineeNamespace;
                case 7:
                    return InlineeName;
                case 8:
                    return InlineeNameSignature;
                case 9:
                    return FailAlways;
                case 10:
                    return FailReason;
                case 11:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<MethodJitInliningFailedTraceData> Action;
        #endregion
    }
    public sealed class RuntimeInformationTraceData : TraceEvent
    {
        public int ClrInstanceID { get { return GetInt16At(0); } }
        public RuntimeSku Sku { get { return (RuntimeSku)GetInt16At(2); } }
        public int BclMajorVersion { get { return GetInt16At(4); } }
        public int BclMinorVersion { get { return GetInt16At(6); } }
        public int BclBuildNumber { get { return GetInt16At(8); } }
        public int BclQfeNumber { get { return GetInt16At(10); } }
        public int VMMajorVersion { get { return GetInt16At(12); } }
        public int VMMinorVersion { get { return GetInt16At(14); } }
        public int VMBuildNumber { get { return GetInt16At(16); } }
        public int VMQfeNumber { get { return GetInt16At(18); } }
        public StartupFlags StartupFlags { get { return (StartupFlags)GetInt32At(20); } }
        public StartupMode StartupMode { get { return (StartupMode)GetByteAt(24); } }
        public string CommandLine { get { return GetUnicodeStringAt(25); } }
        public Guid ComObjectGuid { get { return GetGuidAt(SkipUnicodeString(25)); } }
        public string RuntimeDllPath { get { return GetUnicodeStringAt(SkipUnicodeString(25) + 16); } }

        #region Private
        internal RuntimeInformationTraceData(Action<RuntimeInformationTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(SkipUnicodeString(25) + 16)));
            Debug.Assert(!(Version > 0 && EventDataLength < SkipUnicodeString(SkipUnicodeString(25) + 16)));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.XmlAttrib("Sku", Sku);
            sb.XmlAttrib("BclMajorVersion", BclMajorVersion);
            sb.XmlAttrib("BclMinorVersion", BclMinorVersion);
            sb.XmlAttrib("BclBuildNumber", BclBuildNumber);
            sb.XmlAttrib("BclQfeNumber", BclQfeNumber);
            sb.XmlAttrib("VMMajorVersion", VMMajorVersion);
            sb.XmlAttrib("VMMinorVersion", VMMinorVersion);
            sb.XmlAttrib("VMBuildNumber", VMBuildNumber);
            sb.XmlAttrib("VMQfeNumber", VMQfeNumber);
            sb.XmlAttrib("StartupFlags", StartupFlags);
            sb.XmlAttrib("StartupMode", StartupMode);
            sb.XmlAttrib("CommandLine", CommandLine);
            sb.XmlAttrib("ComObjectGuid", ComObjectGuid);
            sb.XmlAttrib("RuntimeDllPath", RuntimeDllPath);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ClrInstanceID", "Sku", "BclMajorVersion", "BclMinorVersion", "BclBuildNumber", "BclQfeNumber", "VMMajorVersion", "VMMinorVersion", "VMBuildNumber", "VMQfeNumber", "StartupFlags", "StartupMode", "CommandLine", "ComObjectGuid", "RuntimeDllPath" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ClrInstanceID;
                case 1:
                    return Sku;
                case 2:
                    return BclMajorVersion;
                case 3:
                    return BclMinorVersion;
                case 4:
                    return BclBuildNumber;
                case 5:
                    return BclQfeNumber;
                case 6:
                    return VMMajorVersion;
                case 7:
                    return VMMinorVersion;
                case 8:
                    return VMBuildNumber;
                case 9:
                    return VMQfeNumber;
                case 10:
                    return StartupFlags;
                case 11:
                    return StartupMode;
                case 12:
                    return CommandLine;
                case 13:
                    return ComObjectGuid;
                case 14:
                    return RuntimeDllPath;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<RuntimeInformationTraceData> Action;
        #endregion
    }
    public sealed class MethodJitTailCallSucceededTraceData : TraceEvent
    {
        public string MethodBeingCompiledNamespace { get { return GetUnicodeStringAt(0); } }
        public string MethodBeingCompiledName { get { return GetUnicodeStringAt(SkipUnicodeString(0)); } }
        public string MethodBeingCompiledNameSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(0))); } }
        public string CallerNamespace { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0)))); } }
        public string CallerName { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))); } }
        public string CallerNameSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0)))))); } }
        public string CalleeNamespace { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))); } }
        public string CalleeName { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0)))))))); } }
        public string CalleeNameSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))); } }
        public bool TailPrefix { get { return GetInt32At(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0)))))))))) != 0; } }
        public TailCallType TailCallType { get { return (TailCallType)GetInt32At(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))) + 4); } }
        public int ClrInstanceID { get { return GetInt16At(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))) + 8); } }

        #region Private
        internal MethodJitTailCallSucceededTraceData(Action<MethodJitTailCallSucceededTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))) + 10));
            Debug.Assert(!(Version > 0 && EventDataLength < SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))) + 10));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("MethodBeingCompiledNamespace", MethodBeingCompiledNamespace);
            sb.XmlAttrib("MethodBeingCompiledName", MethodBeingCompiledName);
            sb.XmlAttrib("MethodBeingCompiledNameSignature", MethodBeingCompiledNameSignature);
            sb.XmlAttrib("CallerNamespace", CallerNamespace);
            sb.XmlAttrib("CallerName", CallerName);
            sb.XmlAttrib("CallerNameSignature", CallerNameSignature);
            sb.XmlAttrib("CalleeNamespace", CalleeNamespace);
            sb.XmlAttrib("CalleeName", CalleeName);
            sb.XmlAttrib("CalleeNameSignature", CalleeNameSignature);
            sb.XmlAttrib("TailPrefix", TailPrefix);
            sb.XmlAttrib("TailCallType", TailCallType);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "MethodBeingCompiledNamespace", "MethodBeingCompiledName", "MethodBeingCompiledNameSignature", "CallerNamespace", "CallerName", "CallerNameSignature", "CalleeNamespace", "CalleeName", "CalleeNameSignature", "TailPrefix", "TailCallType", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return MethodBeingCompiledNamespace;
                case 1:
                    return MethodBeingCompiledName;
                case 2:
                    return MethodBeingCompiledNameSignature;
                case 3:
                    return CallerNamespace;
                case 4:
                    return CallerName;
                case 5:
                    return CallerNameSignature;
                case 6:
                    return CalleeNamespace;
                case 7:
                    return CalleeName;
                case 8:
                    return CalleeNameSignature;
                case 9:
                    return TailPrefix;
                case 10:
                    return TailCallType;
                case 11:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<MethodJitTailCallSucceededTraceData> Action;
        #endregion
    }
    public sealed class MethodJitTailCallFailedTraceData : TraceEvent
    {
        public string MethodBeingCompiledNamespace { get { return GetUnicodeStringAt(0); } }
        public string MethodBeingCompiledName { get { return GetUnicodeStringAt(SkipUnicodeString(0)); } }
        public string MethodBeingCompiledNameSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(0))); } }
        public string CallerNamespace { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0)))); } }
        public string CallerName { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))); } }
        public string CallerNameSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0)))))); } }
        public string CalleeNamespace { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))); } }
        public string CalleeName { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0)))))))); } }
        public string CalleeNameSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))); } }
        public bool TailPrefix { get { return GetInt32At(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0)))))))))) != 0; } }
        public string FailReason { get { return GetUTF8StringAt(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))) + 4); } }
        public int ClrInstanceID { get { return GetInt16At(SkipUTF8String(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))) + 4)); } }

        #region Private
        internal MethodJitTailCallFailedTraceData(Action<MethodJitTailCallFailedTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUTF8String(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))) + 4) + 2));
            Debug.Assert(!(Version > 0 && EventDataLength < SkipUTF8String(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(0))))))))) + 4) + 2));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttrib("MethodBeingCompiledNamespace", MethodBeingCompiledNamespace);
            sb.XmlAttrib("MethodBeingCompiledName", MethodBeingCompiledName);
            sb.XmlAttrib("MethodBeingCompiledNameSignature", MethodBeingCompiledNameSignature);
            sb.XmlAttrib("CallerNamespace", CallerNamespace);
            sb.XmlAttrib("CallerName", CallerName);
            sb.XmlAttrib("CallerNameSignature", CallerNameSignature);
            sb.XmlAttrib("CalleeNamespace", CalleeNamespace);
            sb.XmlAttrib("CalleeName", CalleeName);
            sb.XmlAttrib("CalleeNameSignature", CalleeNameSignature);
            sb.XmlAttrib("TailPrefix", TailPrefix);
            sb.XmlAttrib("FailReason", FailReason);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "MethodBeingCompiledNamespace", "MethodBeingCompiledName", "MethodBeingCompiledNameSignature", "CallerNamespace", "CallerName", "CallerNameSignature", "CalleeNamespace", "CalleeName", "CalleeNameSignature", "TailPrefix", "FailReason", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return MethodBeingCompiledNamespace;
                case 1:
                    return MethodBeingCompiledName;
                case 2:
                    return MethodBeingCompiledNameSignature;
                case 3:
                    return CallerNamespace;
                case 4:
                    return CallerName;
                case 5:
                    return CallerNameSignature;
                case 6:
                    return CalleeNamespace;
                case 7:
                    return CalleeName;
                case 8:
                    return CalleeNameSignature;
                case 9:
                    return TailPrefix;
                case 10:
                    return FailReason;
                case 11:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<MethodJitTailCallFailedTraceData> Action;
        #endregion
    }

    [Flags]
    public enum AppDomainFlags
    {
        None = 0,
        Default = 0x1,
        Executable = 0x2,
        Shared = 0x4,
    }
    [Flags]
    public enum AssemblyFlags
    {
        None = 0,
        DomainNeutral = 0x1,
        Dynamic = 0x2,
        Native = 0x4,
        Collectible = 0x8,
    }
    [Flags]
    public enum ModuleFlags
    {
        None = 0,
        DomainNeutral = 0x1,
        Native = 0x2,
        Dynamic = 0x4,
        Manifest = 0x8,
    }
    [Flags]
    public enum MethodFlags
    {
        None = 0,
        Dynamic = 0x1,
        Generic = 0x2,
        HasSharedGenericCode = 0x4,
        Jitted = 0x8,
    }
    [Flags]
    public enum StartupMode
    {
        None = 0,
        ManagedExe = 0x1,
        HostedClr = 0x2,
        IjwDll = 0x4,
        ComActivated = 0x8,
        Other = 0x10,
    }
    [Flags]
    public enum RuntimeSku
    {
        None = 0,
        DesktopClr = 0x1,
        CoreClr = 0x2,
    }
    [Flags]
    public enum ExceptionThrownFlags
    {
        None = 0,
        HasInnerException = 0x1,
        Nested = 0x2,
        ReThrown = 0x4,
        CorruptedState = 0x8,
        CLSCompliant = 0x10,
    }
    [Flags]
    public enum ILStubGeneratedFlags
    {
        None = 0,
        ReverseInterop = 0x1,
        ComInterop = 0x2,
        NGenedStub = 0x4,
        Delegate = 0x8,
        VarArg = 0x10,
        UnmanagedCallee = 0x20,
    }
    [Flags]
    public enum StartupFlags
    {
        None = 0,
        CONCURRENT_GC = 0x000001,
        LOADER_OPTIMIZATION_SINGLE_DOMAIN = 0x000002,
        LOADER_OPTIMIZATION_MULTI_DOMAIN = 0x000004,
        LOADER_SAFEMODE = 0x000010,
        LOADER_SETPREFERENCE = 0x000100,
        SERVER_GC = 0x001000,
        HOARD_GC_VM = 0x002000,
        SINGLE_VERSION_HOSTING_INTERFACE = 0x004000,
        LEGACY_IMPERSONATION = 0x010000,
        DISABLE_COMMITTHREADSTACK = 0x020000,
        ALWAYSFLOW_IMPERSONATION = 0x040000,
        TRIM_GC_COMMIT = 0x080000,
        ETW = 0x100000,
        SERVER_BUILD = 0x200000,
        ARM = 0x400000,
    }
    public enum GCSegmentType
    {
        SmallObjectHeap = 0x0,
        LargeObjectHeap = 0x1,
        ReadOnlyHeap = 0x2,
    }
    public enum GCAllocationKind
    {
        Small = 0x0,
        Large = 0x1,
    }
    public enum GCType
    {
        NonConcurrentGC = 0x0,      // A 'blocking' GC.  
        BackgroundGC = 0x1,         // A Gen 2 GC happening while code continues to run
        ForegroundGC = 0x2,         // A Gen 0 or Gen 1 blocking GC which is happening when a Background GC is in progress.  
    }
    public enum GCReason
    {
        AllocSmall = 0x0,
        Induced = 0x1,
        LowMemory = 0x2,
        Empty = 0x3,
        AllocLarge = 0x4,
        OutOfSpaceSOH = 0x5,
        OutOfSpaceLOH = 0x6,
        InducedNotForced = 0x7,
        Internal = 0x8,
        InducedLowMemory = 0x9,
    }
    public enum GCSuspendEEReason
    {
        SuspendOther = 0x0,
        SuspendForGC = 0x1,
        SuspendForAppDomainShutdown = 0x2,
        SuspendForCodePitching = 0x3,
        SuspendForShutdown = 0x4,
        SuspendForDebugger = 0x5,
        SuspendForGCPrep = 0x6,
        SuspendForDebuggerSweep = 0x7,
    }
    public enum ContentionFlags
    {
        Managed = 0x0,
        Native = 0x1,
    }
    public enum TailCallType
    {
        OptimizedTailCall = 0x0,
        RecursiveLoop = 0x1,
        HelperAssistedTailCall = 0x2,
    }
    public enum ThreadAdjustmentReason
    {
        Warmup = 0x0,
        Initializing = 0x1,
        RandomMove = 0x2,
        ClimbingMove = 0x3,
        ChangePoint = 0x4,
        Stabilizing = 0x5,
        Starvation = 0x6,
        ThreadTimedOut = 0x7,
    }

    // [SecuritySafeCritical]
    public sealed class ClrRundownTraceEventParser : TraceEventParser
    {
        public static string ProviderName = "Microsoft-Windows-DotNETRuntimeRundown";
        public static Guid ProviderGuid = new Guid(unchecked((int)0xa669021c), unchecked((short)0xc450), unchecked((short)0x4609), 0xa0, 0x35, 0x5a, 0xf5, 0x9a, 0xf4, 0xdf, 0x18);
        public enum Keywords : long
        {
            Loader = 0x8,
            Jit = 0x10,
            NGen = 0x20,
            Start = 0x40,                   // Do rundown at DC_START
            ForceEndRundown = 0x100,        
            AppDomainResourceManagement = 0x800,
            /// <summary>
            /// Log events associated with the threadpool, and other threading events.  
            /// </summary>
            Threading = 0x10000,
            /// <summary>
            /// Dump the native to IL mapping of any method that is JIT compiled.  (V4.5 runtimes and above).  
            /// </summary>
            JittedMethodILToNativeMap = 0x20000,
            /// <summary>
            /// This supresses NGEN events on V4.0 (where you have NGEN PDBs), but not on V2.0 (which does not know about this 
            /// bit and also does not have NGEN PDBS).  
            /// </summary>
            SupressNGen = 0x40000,
            /// <summary>
            /// TODO document
            /// </summary>
            PerfTrack = 0x20000000,
            Stack = 0x40000000,

            Default = ForceEndRundown+NGen+Jit+SupressNGen+JittedMethodILToNativeMap+Threading+Loader,
        };

        public ClrRundownTraceEventParser(TraceEventSource source) : base(source) { }

        public event Action<MethodILToNativeMapTraceData> MethodILToNativeMapDCStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodILToNativeMapTraceData(value, 149, 1, "Method", MethodTaskGuid, 41, "ILToNativeMapDCStart", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodILToNativeMapTraceData> MethodILToNativeMapDCStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodILToNativeMapTraceData(value, 150, 1, "Method", MethodTaskGuid, 42, "ILToNativeMapDCStop", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ClrStackWalkTraceData> ClrStackWalk
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ClrStackWalkTraceData(value, 0, 11, "ClrStack", ClrStackTaskGuid, 82, "Walk", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodLoadUnloadTraceData> MethodDCStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodLoadUnloadTraceData(value, 141, 1, "Method", MethodTaskGuid, 35, "DCStart", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodLoadUnloadTraceData> MethodDCStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodLoadUnloadTraceData(value, 142, 1, "Method", MethodTaskGuid, 36, "DCEnd", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodLoadUnloadVerboseTraceData> MethodDCStartVerbose
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodLoadUnloadVerboseTraceData(value, 143, 1, "Method", MethodTaskGuid, 39, "DCStartVerbose", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<MethodLoadUnloadVerboseTraceData> MethodDCStopVerbose
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodLoadUnloadVerboseTraceData(value, 144, 1, "Method", MethodTaskGuid, 40, "DCStopVerbose", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DCStartEndTraceData> MethodDCStartComplete
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DCStartEndTraceData(value, 145, 1, "Method", MethodTaskGuid, 14, "DCStartComplete", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DCStartEndTraceData> MethodDCStopComplete
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DCStartEndTraceData(value, 146, 1, "Method", MethodTaskGuid, 15, "DCStopComplete", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DCStartEndTraceData> MethodDCStartInit
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DCStartEndTraceData(value, 147, 1, "Method", MethodTaskGuid, 16, "DCStartInit", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DCStartEndTraceData> MethodDCStopInit
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DCStartEndTraceData(value, 148, 1, "Method", MethodTaskGuid, 17, "DCStopInit", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DomainModuleLoadUnloadTraceData> LoaderDomainModuleDCStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DomainModuleLoadUnloadTraceData(value, 151, 2, "Loader", LoaderTaskGuid, 46, "DomainModuleDCStart", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DomainModuleLoadUnloadTraceData> LoaderDomainModuleDCStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DomainModuleLoadUnloadTraceData(value, 152, 2, "Loader", LoaderTaskGuid, 47, "DomainModuleDCStop", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ModuleLoadUnloadTraceData> ModuleDCStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ModuleLoadUnloadTraceData(value, 153, 2, "Loader", LoaderTaskGuid, 35, "ModuleDCStart", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ModuleLoadUnloadTraceData> LoaderModuleDCStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ModuleLoadUnloadTraceData(value, 154, 2, "Loader", LoaderTaskGuid, 36, "ModuleDCStop", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<AssemblyLoadUnloadTraceData> AssemblyDCStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new AssemblyLoadUnloadTraceData(value, 155, 2, "Loader", LoaderTaskGuid, 39, "AssemblyDCStart", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<AssemblyLoadUnloadTraceData> LoaderAssemblyDCStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new AssemblyLoadUnloadTraceData(value, 156, 2, "Loader", LoaderTaskGuid, 40, "AssemblyDCStop", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<AppDomainLoadUnloadTraceData> AppDomainDCStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new AppDomainLoadUnloadTraceData(value, 157, 2, "Loader", LoaderTaskGuid, 43, "AppDomainDCStart", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<AppDomainLoadUnloadTraceData> LoaderAppDomainDCStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new AppDomainLoadUnloadTraceData(value, 158, 2, "Loader", LoaderTaskGuid, 44, "AppDomainDCStop", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ThreadCreatedTraceData> LoaderThreadDCStop
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ThreadCreatedTraceData(value, 159, 2, "Loader", LoaderTaskGuid, 48, "ThreadDCStop", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RuntimeInformationTraceData> RuntimeStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RuntimeInformationTraceData(value, 187, 19, "Runtime", RuntimeTaskGuid, 1, "Start", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }

        #region Event ID Definitions
        public const TraceEventID ClrStackWalkEventID = (TraceEventID)0;
        public const TraceEventID MethodDCStartEventID = (TraceEventID)141;
        public const TraceEventID MethodDCStopEventID = (TraceEventID)142;
        public const TraceEventID MethodDCStartVerboseEventID = (TraceEventID)143;
        public const TraceEventID MethodDCStopVerboseEventID = (TraceEventID)144;
        public const TraceEventID MethodDCStartCompleteEventID = (TraceEventID)145;
        public const TraceEventID MethodDCStopCompleteEventID = (TraceEventID)146;
        public const TraceEventID MethodDCStartInitEventID = (TraceEventID)147;
        public const TraceEventID MethodDCStopInitEventID = (TraceEventID)148;
        public const TraceEventID LoaderDomainModuleDCStartEventID = (TraceEventID)151;
        public const TraceEventID LoaderDomainModuleDCStopEventID = (TraceEventID)152;
        public const TraceEventID LoaderModuleDCStartEventID = (TraceEventID)153;
        public const TraceEventID LoaderModuleDCStopEventID = (TraceEventID)154;
        public const TraceEventID LoaderAssemblyDCStartEventID = (TraceEventID)155;
        public const TraceEventID LoaderAssemblyDCStopEventID = (TraceEventID)156;
        public const TraceEventID LoaderAppDomainDCStartEventID = (TraceEventID)157;
        public const TraceEventID LoaderAppDomainDCStopEventID = (TraceEventID)158;
        public const TraceEventID LoaderThreadDCStopEventID = (TraceEventID)159;
        public const TraceEventID RuntimeStartEventID = (TraceEventID)187;
        #endregion

        public sealed class DCStartEndTraceData : TraceEvent
        {
            public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(0); return 0; } }

            #region Private
            internal DCStartEndTraceData(Action<DCStartEndTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
                : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
            {
                this.Action = action;
            }
            protected internal override void Dispatch()
            {
                Action(this);
            }
            protected internal override void Validate()
            {
                Debug.Assert(!(Version == 1 && EventDataLength != 2));
                Debug.Assert(!(Version > 1 && EventDataLength < 2));
            }
            public override StringBuilder ToXml(StringBuilder sb)
            {
                Prefix(sb);
                sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
                sb.Append("/>");
                return sb;
            }

            public override string[] PayloadNames
            {
                get
                {
                    if (payloadNames == null)
                        payloadNames = new string[] { "ClrInstanceID" };
                    return payloadNames;
                }
            }

            public override object PayloadValue(int index)
            {
                switch (index)
                {
                    case 0:
                        return ClrInstanceID;
                    default:
                        Debug.Assert(false, "Bad field index");
                        return null;
                }
            }

            private event Action<DCStartEndTraceData> Action;
            #endregion
        }
        #region private
        private static Guid MethodTaskGuid = new Guid(unchecked((int)0x0bcd91db), unchecked((short)0xf943), unchecked((short)0x454a), 0xa6, 0x62, 0x6e, 0xdb, 0xcf, 0xbb, 0x76, 0xd2);
        private static Guid LoaderTaskGuid = new Guid(unchecked((int)0x5a54f4df), unchecked((short)0xd302), unchecked((short)0x4fee), 0xa2, 0x11, 0x6c, 0x2c, 0x0c, 0x1d, 0xcb, 0x1a);
        private static Guid ClrStackTaskGuid = new Guid(unchecked((int)0xd3363dc0), unchecked((short)0x243a), unchecked((short)0x4620), 0xa4, 0xd0, 0x8a, 0x07, 0xd7, 0x72, 0xf5, 0x33);
        private static Guid RuntimeTaskGuid = new Guid(unchecked((int)0xcd7d3e32), unchecked((short)0x65fe), unchecked((short)0x40cd), 0x92, 0x25, 0xa2, 0x57, 0x7d, 0x20, 0x3f, 0xc3);
        #endregion
    }

    public sealed class ClrStressTraceEventParser : TraceEventParser
    {
        public static string ProviderName = "Microsoft-Windows-DotNETRuntimeStress";
        public static Guid ProviderGuid = new Guid(unchecked((int)0xcc2bcbba), unchecked((short)0x16b6), unchecked((short)0x4cf3), 0x89, 0x90, 0xd7, 0x4c, 0x2e, 0x8a, 0xf5, 0x00);
        public enum Keywords : long
        {
            Stack = 0x40000000,
        };

        public ClrStressTraceEventParser(TraceEventSource source) : base(source) { }

        public event Action<StressLogTraceData> StressLogStart
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new StressLogTraceData(value, 0, 1, "StressLog", StressLogTaskGuid, 1, "Start", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ClrStackWalkTraceData> ClrStackWalk
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ClrStackWalkTraceData(value, 1, 11, "ClrStack", ClrStackTaskGuid, 82, "Walk", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }

        #region Event ID Definitions
        public const TraceEventID StressLogStartEventID = (TraceEventID)0;
        public const TraceEventID ClrStackWalkEventID = (TraceEventID)1;
        #endregion

        #region private
        private static Guid StressLogTaskGuid = new Guid(unchecked((int)0xea40c74d), unchecked((short)0x4f65), unchecked((short)0x4561), 0xbb, 0x26, 0x65, 0x62, 0x31, 0xc8, 0x96, 0x7f);
        private static Guid ClrStackTaskGuid = new Guid(unchecked((int)0xd3363dc0), unchecked((short)0x243a), unchecked((short)0x4620), 0xa4, 0xd0, 0x8a, 0x07, 0xd7, 0x72, 0xf5, 0x33);
        #endregion
    }

    public sealed class StressLogTraceData : TraceEvent
    {
        public int Facility { get { return GetInt32At(0); } }
        public int LogLevel { get { return GetByteAt(4); } }
        public string Message { get { return GetUTF8StringAt(5); } }
        public int ClrInstanceID { get { if (Version >= 1) return GetInt16At(SkipUTF8String(5)); return 0; } }

        #region Private
        internal StressLogTraceData(Action<StressLogTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUTF8String(5)));
            Debug.Assert(!(Version == 1 && EventDataLength != SkipUTF8String(5) + 2));
            Debug.Assert(!(Version > 1 && EventDataLength < SkipUTF8String(5) + 2));
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("Facility", Facility);
            sb.XmlAttrib("LogLevel", LogLevel);
            sb.XmlAttrib("Message", Message);
            sb.XmlAttrib("ClrInstanceID", ClrInstanceID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Facility", "LogLevel", "Message", "ClrInstanceID" };
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Facility;
                case 1:
                    return LogLevel;
                case 2:
                    return Message;
                case 3:
                    return ClrInstanceID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<StressLogTraceData> Action;
        #endregion
    }
}
