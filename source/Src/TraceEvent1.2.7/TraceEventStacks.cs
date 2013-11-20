// Copyright (c) Microsoft Corporation.  All rights reserved
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// 
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Diagnostics.Tracing.Parsers;
using Diagnostics.Tracing;
using Diagnostics.Tracing.StackSources;
using Symbols;
using Address = System.UInt64;

namespace Diagnostics.Tracing
{
    public static class TraceEventStackSourceExtensions
    {
        public static StackSource CPUStacks(this TraceLog eventLog, TraceProcess process = null, bool showUnknownAddresses = false, Predicate<TraceEvent> predicate = null)
        {
            TraceEvents events;
            if (process == null)
                events = eventLog.Events.Filter((x) => ((predicate == null) || predicate(x)) && x is SampledProfileTraceData && x.ProcessID != 0);
            else
                events = process.EventsInProcess.Filter((x) => ((predicate == null) || predicate(x)) && x is SampledProfileTraceData);

            var traceStackSource = new TraceEventStackSource(events);
            traceStackSource.ShowUnknownAddresses = showUnknownAddresses;
            // We clone the samples so that we don't have to go back to the ETL file from here on.  
            return CopyStackSource.Clone(traceStackSource);
        }
        public static StackSource ThreadTimeStacks(this TraceLog eventLog, TraceProcess process = null, bool showUnknownAddresses = false)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// This is the traditional grouping by method.
    /// 
    /// TraceEventStackSource create the folowing meaning for the code:StackSourceCallStackIndex
    /// 
    /// * The call stacks ID consists of the following ranges concatinated together. 
    ///     * a small set of fixed Pseuo stacks (Start marks the end of these)
    ///     * CallStackIndex
    ///     * ThreadIndex
    ///     * ProcessIndex
    ///     * BrokenStacks (One per thread)
    ///     * Stacks for threads without explicit stacks (Limited to 1K)
    ///         
    /// TraceEventStackSource create the folowing meaning for the code:StackSourceFrameIndex
    /// 
    /// The frame ID consists of the following ranges concatinated together. 
    ///     * a small fixed number of Pseudo frame (Broken, and Unknown)
    ///     * MaxCodeAddressIndex - something with a TraceCodeAddress. 
    ///     * ThreadIndex         - ETW stacks don't have a thread or process node, so we add them.
    ///     * ProcessIndex
    ///     
    /// </summary>
    public class TraceEventStackSource : StackSource
    {
        public TraceEventStackSource(TraceEvents events)
        {
            Debug.Assert(m_log == null);
            if (events != null)
                m_log = events.Log;
            m_goodTopModuleIndex = ModuleFileIndex.Invalid;
            m_curSample = new StackSourceSample(this);
            m_curSample.Metric = events.Log.SampleProfileInterval100ns / 10000.0F;
            m_events = events;
            m_maxPseudoStack = m_log.CodeAddresses.MaxCodeAddressIndex;     // This really is a guess as to how many stacks we need.  
        }

        public override void ProduceSamples(Action<StackSourceSample> callback)
        {
            var dispatcher = m_events.GetSource();
            // TODO use callback model rather than enumerator
            foreach (var event_ in ((IEnumerable<TraceEvent>)m_events))
            {
                m_curSample.StackIndex = GetStack(event_);

                m_curSample.TimeRelMSec = event_.TimeStampRelativeMSec;
                Debug.Assert(event_.ProcessName != null);
                callback(m_curSample);
            };
        }
        public override double SampleTimeRelMSecLimit
        {
            get
            {
                return m_log.SessionEndTime100ns;
            }
        }

        // see code:TraceEventStackSource for the encoding of StackSourceCallStackIndex and StackSourceFrameIndex
        public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex)
        {
            Debug.Assert(callStackIndex >= 0);
            Debug.Assert(StackSourceCallStackIndex.Start == 0);         // If there are any cases before start, we need to handle them here. 
            int stackIndex = (int)callStackIndex - (int)StackSourceCallStackIndex.Start;
            if (stackIndex < m_log.CallStacks.MaxCallStackIndex)
            {
                CodeAddressIndex codeAddressIndex = m_log.CallStacks.CodeAddressIndex((CallStackIndex)stackIndex);
                return (StackSourceFrameIndex)(codeAddressIndex + (int)StackSourceFrameIndex.Start);
            }
            stackIndex -= m_log.CallStacks.MaxCallStackIndex;
            if (stackIndex < m_log.Threads.MaxThreadIndex + m_log.Processes.MaxProcessIndex)
            {
                // At this point this is the encoded thread/process index.   We use the same encoding for both stacks and for frame names
                // so we just need to add back in the proper offset. 
                return (StackSourceFrameIndex)(stackIndex + m_log.CodeAddresses.MaxCodeAddressIndex + (int)StackSourceFrameIndex.Start);
            }
            stackIndex -= m_log.Threads.MaxThreadIndex + m_log.Processes.MaxProcessIndex;

            if (stackIndex < m_log.Threads.MaxThreadIndex)      // Is it a broken stack 
                return StackSourceFrameIndex.Broken;
            stackIndex -= m_log.Threads.MaxThreadIndex;

            // Is it a 'single node' stack (e.g. a profile sample without a stack)
            if (stackIndex < m_pseudoStacks.Count)
            {
                // From the Pseudo stack index, find the code address.  
                int codeAddressIndex = (int)m_pseudoStacks[stackIndex].CodeAddressIndex;

                // Return it as the frame.  
                return (StackSourceFrameIndex)(codeAddressIndex + (int)StackSourceFrameIndex.Start);
            }

            Debug.Assert(false, "Illegal Call Stack Index");
            return StackSourceFrameIndex.Invalid;
        }
        // see code:TraceEventStackSource for the encoding of StackSourceCallStackIndex and StackSourceFrameIndex
        public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex)
        {
            Debug.Assert(callStackIndex >= 0);
            Debug.Assert(StackSourceCallStackIndex.Start == 0);         // If there are any cases before start, we need to handle them here. 

            int curIndex = (int)callStackIndex - (int)StackSourceCallStackIndex.Start;
            int nextIndex = (int)StackSourceCallStackIndex.Start;
            if (curIndex < m_log.CallStacks.MaxCallStackIndex)
            {
                var nextCallStackIndex = m_log.CallStacks.Caller((CallStackIndex)curIndex);
                if (nextCallStackIndex == CallStackIndex.Invalid)
                {
                    nextIndex += m_log.CallStacks.MaxCallStackIndex;    // Now points at the threads region.  
                    var threadIndex = m_log.CallStacks.ThreadIndex((CallStackIndex)curIndex);
                    nextIndex += (int)threadIndex;

                    // Mark it as a broken stack, which come after all the indexes for normal threads and processes. 
                    if (!ReasonableTopFrame(callStackIndex))
                        nextIndex += m_log.Threads.MaxThreadIndex + m_log.Processes.MaxProcessIndex;
                }
                else
                    nextIndex += (int)nextCallStackIndex;
                return (StackSourceCallStackIndex)nextIndex;
            }
            curIndex -= m_log.CallStacks.MaxCallStackIndex;                                 // Now is a thread index
            nextIndex += m_log.CallStacks.MaxCallStackIndex;                                // Output index points to the thread region.          

            if (curIndex < m_log.Threads.MaxThreadIndex)
            {
                nextIndex += m_log.Threads.MaxThreadIndex;                                  // Output index point to process region.
                nextIndex += (int)m_log.Threads[(ThreadIndex)curIndex].Process.ProcessIndex;
                return (StackSourceCallStackIndex)nextIndex;
            }
            curIndex -= m_log.Threads.MaxThreadIndex;                                      // Now is a broken thread index

            if (curIndex < m_log.Processes.MaxProcessIndex)
                return StackSourceCallStackIndex.Invalid;                                   // Process has no parent
            curIndex -= m_log.Processes.MaxProcessIndex;                                    // Now is a broken thread index

            if (curIndex < m_log.Threads.MaxThreadIndex)                                    // It is a broken stack
            {
                nextIndex += curIndex;                                                      // Indicate the real thread.  
                return (StackSourceCallStackIndex)nextIndex;
            }
            curIndex -= m_log.Threads.MaxThreadIndex;                                       // Now it points at the one-element stacks. 

            if (curIndex < m_pseudoStacks.Count)
            {
                // Now points begining of the broken stacks indexes.  
                nextIndex += m_log.Threads.MaxThreadIndex + m_log.Processes.MaxProcessIndex;

                // Pick the broken stack for this thread.  
                nextIndex += (int)m_pseudoStacks[curIndex].ThreadIndex;
                return (StackSourceCallStackIndex)nextIndex;
            }

            Debug.Assert(false, "Invalid CallStackIndex");
            return StackSourceCallStackIndex.Invalid;
        }
        public SourceLocation GetSourceLine(StackSourceFrameIndex frameIndex, SymbolReader reader)
        {
            uint codeAddressIndex = (uint)frameIndex - (uint)StackSourceFrameIndex.Start;
            if (codeAddressIndex >= m_log.CodeAddresses.MaxCodeAddressIndex)
                return null;
            return m_log.CodeAddresses.GetSourceLine(reader, (CodeAddressIndex)codeAddressIndex);
        }
        public override string GetFrameName(StackSourceFrameIndex frameIndex, bool fullModulePath)
        {
            string methodName = "?";
            var moduleFileIdx = ModuleFileIndex.Invalid;

            if (frameIndex < StackSourceFrameIndex.Start)
            {
                if (frameIndex == StackSourceFrameIndex.Broken)
                    return "BROKEN";
                else if (frameIndex == StackSourceFrameIndex.Overhead)
                    return "OVERHEAD";
                else if (frameIndex == StackSourceFrameIndex.Root)
                    return "ROOT";
                else
                    return "?!?";
            }
            int index = (int)frameIndex - (int)StackSourceFrameIndex.Start;
            if (index < m_log.CodeAddresses.MaxCodeAddressIndex)
            {
                var codeAddressIndex = (CodeAddressIndex)index;
                MethodIndex methodIndex = m_log.CallStacks.CodeAddresses.MethodIndex(codeAddressIndex);
                if (methodIndex != MethodIndex.Invalid)
                    methodName = m_log.CodeAddresses.Methods.FullMethodName(methodIndex);
                else
                {
                    if (ShowUnknownAddresses)
                        methodName = "0x" + m_log.CallStacks.CodeAddresses.Address(codeAddressIndex).ToString("x");
                }
                moduleFileIdx = m_log.CodeAddresses.ModuleFileIndex(codeAddressIndex);
            }
            else
            {
                index -= m_log.CodeAddresses.MaxCodeAddressIndex;
                if (index < m_log.Threads.MaxThreadIndex)
                    return m_log.Threads[(ThreadIndex)index].VerboseThreadName;
                index -= m_log.Threads.MaxThreadIndex;
                if (index < m_log.Processes.MaxProcessIndex)
                {
                    TraceProcess process = m_log.Processes[(ProcessIndex)index];
                    string ptrSize = process.Is64Bit ? "64" : "32";
                    return "Process" + ptrSize + " " + process.Name + " (" + process.ProcessID + ")";
                }
                Debug.Assert(false, "Illegal Frame index");
                return "";
            }

            string moduleName = "?";
            if (moduleFileIdx != ModuleFileIndex.Invalid)
            {
                if (fullModulePath)
                {
                    moduleName = m_log.CodeAddresses.ModuleFiles[moduleFileIdx].FilePath;
                    if (moduleName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                        moduleName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        moduleName = moduleName.Substring(0, moduleName.Length - 4);        // Remove the .dll or .exe
                }
                else
                    moduleName = m_log.CodeAddresses.ModuleFiles[moduleFileIdx].Name;
            }

            return moduleName + "!" + methodName;
        }

        public override int CallStackIndexLimit
        {
            get
            {
                return (int)StackSourceCallStackIndex.Start + m_log.CallStacks.MaxCallStackIndex +
                    2 * m_log.Threads.MaxThreadIndex + m_log.Processes.MaxProcessIndex +     // *2 one for normal threads, one for broken threads. 
                    m_maxPseudoStack;                                                        // These are for the threads with no explicit stacks. 
            }
        }
        public override int CallFrameIndexLimit
        {
            get
            {
                return (int)StackSourceFrameIndex.Start + m_log.CodeAddresses.MaxCodeAddressIndex + m_log.Threads.MaxThreadIndex + m_log.Processes.MaxProcessIndex;
            }
        }

        // These are TraceEventStackSource specific.  
        /// <summary>
        /// Return the TraceLog file that is associated with this stack source.  
        /// </summary>
        public TraceLog TraceLog { get { return m_log; } }
        /// <summary>
        /// Normally addresses without symbolic names are listed as ?, however sometimes it is useful 
        /// to see the actuall address as a hexidecimal number.  Setting this will do that.  
        /// </summary>
        public bool ShowUnknownAddresses { get; set; }
        /// <summary>
        /// Looks up symbols for all modules that have an inclusive count >= minCount. 
        /// stackSource, if given, can be used to be the filter.  If null, 'this' is used.
        /// If stackSource is given, it needs to use the same indexes for frames as 'this'
        /// </summary>
        public void LookupWarmSymbols(int minCount, SymbolReader reader, StackSource stackSource = null)
        {
            if (stackSource == null)
                stackSource = this;

            Debug.Assert(stackSource.CallFrameIndexLimit == this.CallFrameIndexLimit);
            Debug.Assert(stackSource.CallStackIndexLimit == this.CallStackIndexLimit);

            reader.Log.WriteLine("Resolving all symbols for modules with inclusive times > {0}", minCount);
            if ((reader.Flags & SymbolReaderFlags.CacheOnly) != 0)
                reader.Log.WriteLine("Cache-Only set: will only look on the local machine.");

            // Get a list of all the unique frames.   We also keep track of unique stacks for efficiency
            var stackModuleLists = new ModuleList[stackSource.CallStackIndexLimit];
            var stackCounts = new int[stackSource.CallStackIndexLimit];
            var totalCount = 0;

            // Compute for each stack, the set of inclusive modules for that stack
            stackSource.ProduceSamples(delegate(StackSourceSample sample)
            {
                stackCounts[(int)sample.StackIndex]++;
                totalCount++;
            });
            reader.Log.WriteLine("Got a total of {0} samples", totalCount);

            // for each stack in the trace, find the list of modules for that stack
            var moduleCounts = new int[TraceLog.ModuleFiles.MaxModuleFileIndex];
            for (int i = 0; i < stackCounts.Length; i++)
            {
                var count = stackCounts[i];
                if (count > 0)
                {
                    var modules = GetModulesForStack(stackModuleLists, (StackSourceCallStackIndex)i);
                    // Update the counts for each module in that stack.  
                    while (modules != null)
                    {
                        moduleCounts[(int)modules.Module.ModuleFileIndex] += count;
                        modules = modules.Next;
                    }
                }
            }

            // Now that we have an list of the inclusive counts of all frames.  Find all stacks that meet the threshold
            for (int i = 0; i < moduleCounts.Length; i++)
            {
                if (moduleCounts[i] >= minCount)
                {
                    var moduleFile = TraceLog.ModuleFiles[(ModuleFileIndex)i];
                    reader.Log.WriteLine("Resolving symbols (count={0}) for module {1} ", moduleCounts[i], moduleFile.FilePath);
                    TraceLog.CallStacks.CodeAddresses.LookupSymbolsForModule(reader, moduleFile);
                }
            }
            reader.Log.WriteLine("Done Resolving all symbols for modules with inclusive times > {0}", minCount);
        }
        /// <summary>
        /// Given a frame index, return the cooresponding code address for it. 
        /// </summary>
        public CodeAddressIndex GetFrameCodeAddress(StackSourceFrameIndex frameIndex)
        {
            uint codeAddressIndex = (uint)frameIndex - (uint)StackSourceFrameIndex.Start;
            if (codeAddressIndex >= m_log.CodeAddresses.MaxCodeAddressIndex)
                return CodeAddressIndex.Invalid;
            return (CodeAddressIndex)codeAddressIndex;
        }

        #region private
        /// <summary>
        /// Returns a list of modules for the stack 'stackIdx'.  It also updates the interning table stackModuleLists, so 
        /// that the entry cooresponding to stackIdx remembers the answer.  This can speed up processing alot since many
        /// stacks have the same prefixes to root.  
        /// </summary>
        private ModuleList GetModulesForStack(ModuleList[] stackModuleLists, StackSourceCallStackIndex stackIdx)
        {
            var ret = stackModuleLists[(int)stackIdx];
            if (ret == null)
            {
                // ret = the module list for the rest of the frames. 
                var callerIdx = GetCallerIndex(stackIdx);
                if (callerIdx != StackSourceCallStackIndex.Invalid)
                    ret = GetModulesForStack(stackModuleLists, callerIdx);

                // Compute the module for the top most frame, and add it to the list (if we find a module)  
                TraceModuleFile module = null;
                var frameIdx = GetFrameIndex(stackIdx);
                if (frameIdx != StackSourceFrameIndex.Invalid)
                {
                    var codeAddress = GetFrameCodeAddress(frameIdx);
                    if (codeAddress != CodeAddressIndex.Invalid)
                    {
                        var moduleFileIdx = TraceLog.CallStacks.CodeAddresses.ModuleFileIndex(codeAddress);
                        if (moduleFileIdx != ModuleFileIndex.Invalid)
                        {
                            module = TraceLog.ModuleFiles[moduleFileIdx];
                            ret = ModuleList.SetAdd(module, ret);
                        }
                    }
                }
                stackModuleLists[(int)stackIdx] = ret;
            }
            return ret;
        }

        /// <summary>
        /// A ModuleList is a linked list of modules.  It is only used in GetModulesForStack and LookupWarmSymbols
        /// </summary>
        class ModuleList
        {
            public static ModuleList SetAdd(TraceModuleFile module, ModuleList list)
            {
                if (!Member(module, list))
                    return new ModuleList(module, list);
                return list;
            }
            public static bool Member(TraceModuleFile module, ModuleList rest)
            {
                while (rest != null)
                {
                    if ((object)module == (object)rest.Module)
                        return true;
                    rest = rest.Next;
                }
                return false;
            }

            public ModuleList(TraceModuleFile module, ModuleList rest)
            {
                Module = module;
                Next = rest;
            }
            public TraceModuleFile Module;
            public ModuleList Next;
        }

        internal TraceEventStackSource(TraceLog log)
        {
            m_log = log;
            m_goodTopModuleIndex = ModuleFileIndex.Invalid;
            m_curSample = new StackSourceSample(this);
            m_curSample.Metric = log.SampleProfileInterval100ns / 10000.0F;
        }

        // Sometimes we just have a code address and thread, but no actual ETW stack.  Create a 'one element'
        // stack whose index is the index into the m_pseudoStacks array
        struct PseudoStack : IEquatable<PseudoStack>
        {
            public PseudoStack(ThreadIndex threadIndex, CodeAddressIndex codeAddressIndex)
            {
                ThreadIndex = threadIndex; CodeAddressIndex = codeAddressIndex;
            }
            public ThreadIndex ThreadIndex;
            public CodeAddressIndex CodeAddressIndex;

            public override int GetHashCode() { return (int)CodeAddressIndex + ((int)ThreadIndex) * 0x10000; }
            public override bool Equals(object obj) { throw new NotImplementedException(); }
            public bool Equals(PseudoStack other) { return ThreadIndex == other.ThreadIndex && CodeAddressIndex == other.CodeAddressIndex; }
        };
        private GrowableArray<PseudoStack> m_pseudoStacks;
        private int m_maxPseudoStack;
        /// <summary>
        /// This maps pseudo-stacks to their index (thus it is the inverse of m_pseudoStack; 
        /// </summary>
        private Dictionary<PseudoStack, int> m_pseudoStacksTable;

        /// <summary>
        /// Given a thread and a call stack that does not have a stack, make up a pseudo stack for it consisting of the code address, 
        /// the broken node, the thread and process.   Will return -1 if it can't allocate another Pseudo-stack.
        /// </summary> 
        private int GetPseudoStack(ThreadIndex threadIndex, CodeAddressIndex codeAddrIndex)
        {
            if (m_pseudoStacksTable == null)
                m_pseudoStacksTable = new Dictionary<PseudoStack, int>();

            var pseudoStack = new PseudoStack(threadIndex, codeAddrIndex);
            int ret;
            if (m_pseudoStacksTable.TryGetValue(pseudoStack, out ret))
                return ret;

            ret = m_pseudoStacks.Count;
            if (ret >= m_maxPseudoStack)
                return -1;
            m_pseudoStacks.Add(pseudoStack);
            m_pseudoStacksTable.Add(pseudoStack, ret);
            return ret;
        }

        private StackSourceCallStackIndex GetStack(TraceEvent event_)
        {
            // Console.WriteLine("Getting Stack for sample at {0:f4}", sample.TimeStampRelativeMSec);
            var ret = (int)event_.CallStackIndex();
            if (ret == (int)CallStackIndex.Invalid)
            {
                var thread = event_.Thread();
                if (thread == null)
                    return StackSourceCallStackIndex.Invalid;

                // If the event is a sample profile, or page fault we can make a one element stack with the EIP in the event 
                CodeAddressIndex codeAddrIdx = CodeAddressIndex.Invalid;
                var asSampleProfile = event_ as SampledProfileTraceData;
                if (asSampleProfile != null)
                    codeAddrIdx = asSampleProfile.IntructionPointerCodeAddressIndex();
                else
                {
                    var asPageFault = event_ as PageFaultHardFaultTraceData;
                    if (asPageFault != null)
                        codeAddrIdx = asSampleProfile.IntructionPointerCodeAddressIndex();
                }

                if (codeAddrIdx != CodeAddressIndex.Invalid)
                {
                    // Encode the code address for the given thread.  
                    int pseudoStackIndex = GetPseudoStack(thread.ThreadIndex, codeAddrIdx);
                    if (pseudoStackIndex < 0)
                        return StackSourceCallStackIndex.Start;

                    // Psuedostacks happen after all the others.  
                    ret = m_log.CallStacks.MaxCallStackIndex + 2 * m_log.Threads.MaxThreadIndex + m_log.Processes.MaxProcessIndex + pseudoStackIndex;
                }
                else
                {
                    // Otherwise we encode the stack as being at the thread.  
                    ret = m_log.CallStacks.MaxCallStackIndex + (int)thread.ThreadIndex;
                }
            }
            ret = ret + (int)StackSourceCallStackIndex.Start;
            return (StackSourceCallStackIndex)ret;
        }

        private bool ReasonableTopFrame(StackSourceCallStackIndex callStackIndex)
        {

            uint index = (uint)callStackIndex - (uint)StackSourceCallStackIndex.Start;

            var stack = m_log.CallStacks[(CallStackIndex)callStackIndex];
            if (index < (uint)m_log.CallStacks.MaxCallStackIndex)
            {
                CodeAddressIndex codeAddressIndex = m_log.CallStacks.CodeAddressIndex((CallStackIndex)index);
                ModuleFileIndex moduleFileIndex = m_log.CallStacks.CodeAddresses.ModuleFileIndex(codeAddressIndex);
                if (m_goodTopModuleIndex == moduleFileIndex)        // optimization
                    return true;

                TraceModuleFile moduleFile = m_log.CallStacks.CodeAddresses.ModuleFile(codeAddressIndex);
                if (moduleFile == null)
                    return false;

                // We allow things that end in ntdll to be considered unbroken (TODO is this too strong?)
                if (!moduleFile.FilePath.EndsWith("ntdll.dll", StringComparison.OrdinalIgnoreCase))
                    return false;

                m_goodTopModuleIndex = moduleFileIndex;
                return true;
            }
            return false;
        }

        StackSourceSample m_curSample;
        TraceEvents m_events;
        ModuleFileIndex m_goodTopModuleIndex;       // This is a known good module index for a 'good' stack (probably ntDll!RtlUserStackStart
        protected TraceLog m_log;
        #endregion
    }

    /// <summary>
    /// InternTraceEventStackSource works much like TraceEventStackSource.   What is different is
    /// that like all InternStackSources it copies out all the information from the original source
    /// to lookup its frames.  This makes it easy to add new 'psedudo' frames (typically at the top
    /// or the bottom of the stack), and thus is useful for views where you wish to do this.  The
    /// disadvantage of this is that you do need to compute all symbolic information before you 
    /// create the source (since changing symbols will invalidate the interning).  
    /// </summary>
    public class InternTraceEventStackSource : InternStackSource
    {
        public InternTraceEventStackSource(TraceLog log)
        {
            m_log = log;
            m_emptyModuleIdx = Interner.ModuleIntern("");
        }
        /// <summary>
        /// Normally addresses without symbolic names are listed as ?, however sometimes it is useful 
        /// to see the actuall address as a hexidecimal number.  Setting this will do that.  
        /// </summary>
        public bool ShowUnknownAddressses { get; set; }
        public StackSourceFrameIndex GetFrameIndexForName(string frameName, StackSourceModuleIndex moduleIdx = StackSourceModuleIndex.Invalid)
        {
            if (moduleIdx == StackSourceModuleIndex.Invalid)
                moduleIdx = m_emptyModuleIdx;
            return Interner.FrameIntern(frameName, moduleIdx);
        }
        public StackSourceCallStackIndex GetCallStack(StackSourceFrameIndex frameIndex, StackSourceCallStackIndex callerIndex)
        {
            return Interner.CallStackIntern(frameIndex, callerIndex);
        }
        /// <summary>
        /// Convert the TraceEvent callStack 'callStackIdx' into a StackSourceCallStack.  'callStackIdx is 
        /// assumed to be related to the traceEvent 'data'.   'data' is used to determine the process and thread
        /// of the stack.  (TODO data may not be needed anymore as callStackIndexes do encode their thread (and thus process)). 
        /// </summary>
        public StackSourceCallStackIndex GetCallStack(CallStackIndex callStackIndex, TraceEvent data)
        {
            // This only happens if only have ONLY the thread and process (no addressses)
            // TODO do we care about this case?  Should we remove?
            var thread = data.Thread();
            StackSourceCallStackIndex topOfStack;
            if (thread == null)
            {
                var process = data.Process();
                if (process != null)
                    topOfStack = GetCallStackForProcess(process);
                else
                    topOfStack = StackSourceCallStackIndex.Invalid;
            }
            else
                topOfStack = GetCallStackForThread(thread);

            return GetCallStack(callStackIndex, topOfStack, m_callStackMap);
        }

        // Advanced usage
        public StackSourceCallStackIndex GetCallStackForProcess(TraceProcess process)
        {
            string ptrSize = process.Is64Bit ? "64" : "32";
            var processName = "Process" + ptrSize + " " + process.Name + " (" + process.ProcessID + ")";
            var internedProcessFrame = Interner.FrameIntern(processName, m_emptyModuleIdx);
            var processStack = Interner.CallStackIntern(internedProcessFrame, StackSourceCallStackIndex.Invalid);
            return processStack;
        }
        public StackSourceCallStackIndex GetCallStackForThread(TraceThread thread)
        {
            var processStack = GetCallStackForProcess(thread.Process);
            var threadName = "Thread (" + thread.ThreadID + ")";
            var internedThreadFrame = Interner.FrameIntern(threadName, m_emptyModuleIdx);
            var threadStack = Interner.CallStackIntern(internedThreadFrame, processStack);
            return threadStack;
        }
        /// <summary>
        /// Find the StackSourceCallStackIndex for the TraceEvent call stack index 'callStackIndex' which has a top of its 
        /// stack as 'top'.  If callStckMap is non-null it is used as an interning table for CallStackIndex -> StackSourceCallStackIndex.
        /// This can speed up the transformation dramatically.  
        /// </summary>
        /// <param name="callStackIndex"></param>
        /// <param name="top"></param>
        /// <param name="callStackMap"></param>
        /// <returns></returns>
        public StackSourceCallStackIndex GetCallStack(CallStackIndex callStackIndex, StackSourceCallStackIndex top,
            Dictionary<int, StackSourceCallStackIndex> callStackMap)
        {
            if (callStackIndex == CallStackIndex.Invalid)
                return top;

            StackSourceCallStackIndex cachedValue;
            if (callStackMap != null && callStackMap.TryGetValue((int)callStackIndex, out cachedValue))
                return cachedValue;

            bool isReasonableTopStack;
            var frameIdx = GetFrameIndex(m_log.CallStacks.CodeAddressIndex(callStackIndex), out isReasonableTopStack);

            CallStackIndex nonInternedCallerIdx = m_log.CallStacks.Caller(callStackIndex);
            StackSourceCallStackIndex callerIdx;
            if (nonInternedCallerIdx == CallStackIndex.Invalid)
            {
                callerIdx = top;
                if (!isReasonableTopStack)
                {
                    var brokenFrame = Interner.FrameIntern("BROKEN", m_emptyModuleIdx);
                    callerIdx = Interner.CallStackIntern(brokenFrame, callerIdx);
                }
            }
            else
                callerIdx = GetCallStack(nonInternedCallerIdx, top, callStackMap);

            var ret = Interner.CallStackIntern(frameIdx, callerIdx);
            if (callStackMap != null)
                callStackMap[(int)callStackIndex] = ret;
            return ret;
        }
        /// <summary>
        /// This is only used for ETW unmanaged memory collection.  
        /// </summary>
        public void SortSamples()
        {
            if (m_sampleRemoved)
            {
                // remove null entries
                int readIdx = 0;
                int lenIdx = m_samples.Count;
                for (; ; )
                {
                    if (readIdx >= lenIdx)
                        break;
                    if (m_samples[readIdx] == null)
                    {
                        --lenIdx;
                        if (m_samples[lenIdx] != null)
                            m_samples[readIdx] = m_samples[lenIdx];
                    }
                    else
                        readIdx++;
                }
                m_samples.Count = lenIdx;
            }

            // TODO decide if we need this. 
            m_samples.Sort((x, y) => x.TimeRelMSec.CompareTo(y.TimeRelMSec));
            for (int i = 0; i < m_samples.Count; i++)
                m_samples[i].SampleIndex = (StackSourceSampleIndex)i;

            m_samples.Trim(m_samples.Count / 10);       // save space by making the size appropriate.  
            m_callStackMap = null;                      // We are done with this.  
        }
        public void RemoveSample(StackSourceSampleIndex index)
        {
            m_samples[(int)index] = null;
            m_sampleRemoved = true;
        }
        public TraceLog EventLog { get { return m_log; } }
        #region private

        // TODO is making this public a hack?
        public StackSourceFrameIndex GetFrameIndex(CodeAddressIndex codeAddressIndex, out bool isReasonableTopStack)
        {
            isReasonableTopStack = false;
            string moduleName = "?";
            ModuleFileIndex moduleIdx = m_log.CodeAddresses.ModuleFileIndex(codeAddressIndex);
            if (moduleIdx != Diagnostics.Tracing.ModuleFileIndex.Invalid)
            {
                moduleName = m_log.ModuleFiles[moduleIdx].FilePath;
                if (moduleName.EndsWith("ntdll.dll", StringComparison.OrdinalIgnoreCase))
                    isReasonableTopStack = true;
            }

            var internedModule = Interner.ModuleIntern(moduleName);

            string methodName = "?";
            var methodIdx = m_log.CodeAddresses.MethodIndex(codeAddressIndex);
            if (methodIdx != MethodIndex.Invalid)
                methodName = m_log.CodeAddresses.Methods.FullMethodName(methodIdx);
            else if (ShowUnknownAddressses)
                methodName = "0x" + m_log.CallStacks.CodeAddresses.Address(codeAddressIndex).ToString("x");

            var internedFrame = Interner.FrameIntern(methodName, internedModule);
            return internedFrame;
        }

        TraceLog m_log;
        StackSourceModuleIndex m_emptyModuleIdx;
        Dictionary<int, StackSourceCallStackIndex> m_callStackMap = new Dictionary<int, StackSourceCallStackIndex>();
        bool m_sampleRemoved;
        #endregion
    }

    /// <summary>
    /// A MutableTraceEventStackSource allows you to create new nodes on the fly as well as add samples
    /// However unlike an InternStackSource, it keeps the identity of the Stacks and Frames that came
    /// from the TraceLog (thus things like GetSourceLine and LookupSymbols work).  
    /// </summary>
    public class MutableTraceEventStackSource : TraceEventStackSource
    {
        public MutableTraceEventStackSource(TraceLog log)
            : base(log)
        {
            m_Interner = new StackSourceInterner(5000, 1000, 100,
                (StackSourceFrameIndex)base.CallFrameIndexLimit, (StackSourceCallStackIndex)base.CallStackIndexLimit);
            m_emptyModuleIdx = m_Interner.ModuleIntern("");
            m_Interner.FrameNameLookup = GetFrameName;
        }
        /// <summary>
        /// After creating a MultableTraceEventStackSource, you add the samples you want and then call DoneAddingSamples
        /// From that point on you have a fine, read-only stacks source.  
        /// </summary>
        public StackSourceSample AddSample(StackSourceSample sample)
        {
            var sampleCopy = new StackSourceSample(sample);
            sampleCopy.SampleIndex = (StackSourceSampleIndex)m_samples.Count;
            m_samples.Add(sampleCopy);
            if (sampleCopy.TimeRelMSec > m_sampleTimeRelMSecLimit)
                m_sampleTimeRelMSecLimit = sampleCopy.TimeRelMSec;
            return sampleCopy;
        }
        public void DoneAddingSamples() { m_Interner.DoneInterning(); }

        public void SortSamples()
        {
            if (m_sampleRemoved)
            {
                // remove null entries
                int readIdx = 0;
                int lenIdx = m_samples.Count;
                for (; ; )
                {
                    if (readIdx >= lenIdx)
                        break;
                    if (m_samples[readIdx] == null)
                    {
                        --lenIdx;
                        if (m_samples[lenIdx] != null)
                            m_samples[readIdx] = m_samples[lenIdx];
                    }
                    else
                        readIdx++;
                }
                m_samples.Count = lenIdx;
            }
            // TODO decide if we need this. 
            m_samples.Sort((x, y) => x.TimeRelMSec.CompareTo(y.TimeRelMSec));
            for (int i = 0; i < m_samples.Count; i++)
                m_samples[i].SampleIndex = (StackSourceSampleIndex)i;
        }

        public StackSourceModuleIndex GetModuleIndex(string moduleName)
        {
            return m_Interner.ModuleIntern(moduleName);
        }

        /// <summary>
        /// Create a new frame out of 'nothing' (just its name) and optionaly a module
        /// </summary>
        public StackSourceFrameIndex GetFrameIndexForName(string frameName, StackSourceModuleIndex moduleIdx = StackSourceModuleIndex.Invalid)
        {
            if (moduleIdx == StackSourceModuleIndex.Invalid)
                moduleIdx = m_emptyModuleIdx;
            return m_Interner.FrameIntern(frameName, moduleIdx);
        }
        /// <summary>
        /// If you wish to make a frame by annotatting another frame, you can use this method.  
        /// </summary>
        public StackSourceFrameIndex GetFrameIndexDerivedFrame(StackSourceFrameIndex baseFrame, string suffix)
        {
            return m_Interner.FrameIntern(baseFrame, suffix);
        }

        /// <summary>
        /// Create a frame name from a TraceEvent code address.  
        /// </summary>
        public StackSourceFrameIndex GetFrameIndex(CodeAddressIndex codeAddressIndex)
        {
            return (StackSourceFrameIndex)((int)StackSourceFrameIndex.Start + (int)codeAddressIndex);
        }

        /// <summary>
        /// Create a new stack out of 'nothing' just a frame and the caller.  
        /// </summary>
        public StackSourceCallStackIndex GetCallStack(StackSourceFrameIndex frameIndex, StackSourceCallStackIndex callerIndex)
        {
            return m_Interner.CallStackIntern(frameIndex, callerIndex);
        }

        // Caller only need to provide TraceThread 
        public StackSourceCallStackIndex GetCallStackThread(CallStackIndex callStackIndex, TraceThread thread)
        {
            if (callStackIndex == CallStackIndex.Invalid)
            {
                if (thread == null)
                    return StackSourceCallStackIndex.Invalid;
                return GetCallStackForThread(thread);
            }
            var idx = (int)StackSourceCallStackIndex.Start + (int)callStackIndex;
            return (StackSourceCallStackIndex)idx;
        }

        // Note that this always returns a stack, since we know the thread and process. 
        public StackSourceCallStackIndex GetCallStack(CallStackIndex callStackIndex, TraceEvent data)
        {
            if (callStackIndex == CallStackIndex.Invalid)
            {
                if (data == null)
                    return StackSourceCallStackIndex.Invalid;
                var thread = data.Thread();
                if (thread == null)
                    return StackSourceCallStackIndex.Invalid;
                return GetCallStackForThread(thread);
            }
            var idx = (int)StackSourceCallStackIndex.Start + (int)callStackIndex;
            return (StackSourceCallStackIndex)idx;
        }
        /// <summary>
        /// Get the call stack representing a TraceEvent process
        /// </summary>
        public StackSourceCallStackIndex GetCallStackForProcess(TraceProcess process)
        {
            var idx = (int)StackSourceCallStackIndex.Start + m_log.CallStacks.MaxCallStackIndex + m_log.Threads.MaxThreadIndex + (int)process.ProcessIndex;
            return (StackSourceCallStackIndex)idx;
        }
        /// <summary>
        /// Get the call stack representing a TraceEvent thread 
        /// </summary>
        public StackSourceCallStackIndex GetCallStackForThread(TraceThread thread)
        {
            var idx = (int)StackSourceCallStackIndex.Start + m_log.CallStacks.MaxCallStackIndex + (int)thread.ThreadIndex;
            return (StackSourceCallStackIndex)idx;
        }
        /// <summary>
        /// Find the StackSourceCallStackIndex for the TraceEvent call stack index 'callStackIndex' which has a top of its 
        /// stack as 'top'.  If callStckMap is non-null it is used as an interning table for CallStackIndex -> StackSourceCallStackIndex.
        /// This can speed up the transformation dramatically.  
        /// </summary>
        public StackSourceCallStackIndex GetCallStack(CallStackIndex callStackIndex, StackSourceCallStackIndex top,
            Dictionary<int, StackSourceCallStackIndex> callStackMap)
        {
            if (callStackIndex == CallStackIndex.Invalid)
                return top;

            StackSourceCallStackIndex cachedValue;
            if (callStackMap != null && callStackMap.TryGetValue((int)callStackIndex, out cachedValue))
                return cachedValue;

            var frameIdx = GetFrameIndex(m_log.CallStacks.CodeAddressIndex(callStackIndex));

            CallStackIndex nonInternedCallerIdx = m_log.CallStacks.Caller(callStackIndex);
            StackSourceCallStackIndex callerIdx;
            if (nonInternedCallerIdx == CallStackIndex.Invalid)
            {
                callerIdx = top;

                var frameName = GetFrameName(frameIdx, false);
                var bangIdx = frameName.IndexOf('!');
                if (0 < bangIdx)
                {
                    if (!(5 <= bangIdx && string.Compare(frameName, bangIdx-5, "ntdll", 0, 5, StringComparison.OrdinalIgnoreCase) == 0))
                    {
                        var brokenFrame = m_Interner.FrameIntern("BROKEN", m_emptyModuleIdx);
                        callerIdx = m_Interner.CallStackIntern(brokenFrame, callerIdx);
                    }
                }
            }
            else
                callerIdx = GetCallStack(nonInternedCallerIdx, top, callStackMap);

            var ret = m_Interner.CallStackIntern(frameIdx, callerIdx);
            if (callStackMap != null)
                callStackMap[(int)callStackIndex] = ret;
            return ret;
        }

        // overrides to be a stack source.  
        public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex)
        {
            if (callStackIndex >= (StackSourceCallStackIndex)base.CallStackIndexLimit)
                return m_Interner.GetCallerIndex(callStackIndex);
            return base.GetCallerIndex(callStackIndex);
        }
        public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex)
        {
            if (callStackIndex >= (StackSourceCallStackIndex)base.CallStackIndexLimit)
                return m_Interner.GetFrameIndex(callStackIndex);
            return base.GetFrameIndex(callStackIndex);
        }
        public StackSourceModuleIndex GetModuleIndex(StackSourceFrameIndex frameIndex)
        {
            if (frameIndex >= (StackSourceFrameIndex)base.CallFrameIndexLimit)
                return m_Interner.GetModuleIndex(frameIndex);

            return StackSourceModuleIndex.Invalid;      // TODO FIX NOW this is a poor approximation
        }
        public override string GetFrameName(StackSourceFrameIndex frameIndex, bool fullModulePath)
        {
            if (frameIndex >= (StackSourceFrameIndex)base.CallFrameIndexLimit)
                return m_Interner.GetFrameName(frameIndex, fullModulePath);
            return base.GetFrameName(frameIndex, fullModulePath);
        }
        public override StackSourceSample GetSampleByIndex(StackSourceSampleIndex sampleIndex)
        {
            return m_samples[(int)sampleIndex];
        }
        public override int SampleIndexLimit { get { return m_samples.Count; } }
        public override void ProduceSamples(Action<StackSourceSample> callback)
        {
            for (int i = 0; i < m_samples.Count; i++)
                callback(m_samples[i]);
        }
        public override bool SamplesImmutable { get { return true; } }
        public override int CallFrameIndexLimit { get { return base.CallFrameIndexLimit + m_Interner.FrameCount; } }
        public override int CallStackIndexLimit { get { return base.CallStackIndexLimit + m_Interner.CallStackCount; } }
        public override double SampleTimeRelMSecLimit { get { return m_sampleTimeRelMSecLimit; } }

        // TODO this is probably a hack 
        public void RemoveSample(StackSourceSampleIndex index)
        {
            m_samples[(int)index] = null;
            m_sampleRemoved = true;
        }
        #region private
        private StackSourceInterner m_Interner;

        StackSourceModuleIndex m_emptyModuleIdx;
        Dictionary<int, StackSourceCallStackIndex> m_callStackMap = new Dictionary<int, StackSourceCallStackIndex>();
        protected GrowableArray<StackSourceSample> m_samples;
        double m_sampleTimeRelMSecLimit;
        bool m_sampleRemoved;
        #endregion
    }



}