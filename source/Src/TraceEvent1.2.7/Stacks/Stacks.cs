#define TOSTRING_FTNS

// Copyright (c) Microsoft Corporation.  All rights reserved
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// 
using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.IO;                        // For TextWriter.  
using System.Text;
using Address = System.UInt64;
using Symbols;
using Utilities;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;



namespace Diagnostics.Tracing.StackSources
{
    /// <summary>
    /// It is the abstract contract for a sample.  All we need is the Metric and 
    /// </summary>    
    public abstract class StackSource : StackSourceStacks
    {
        /// <summary>
        /// Call 'callback' on every sample in the StackSource.   Will be done linerally and only
        /// one callback will be active simultaneously.  
        /// </summary>
        public abstract void ProduceSamples(Action<StackSourceSample> callback);
        /// <summary>
        /// If this is overidden to return true, then the consumer knows that the samples are not overwritten 
        /// </summary>
        public virtual bool SamplesImmutable { get { return false; } }

        /// <summary>
        /// Also called 'callback' on every sample in the StackSource however there may be more than
        /// one callback running simultaneously.    Thus 'callback' must be thread-safe and the order
        /// of the samples should not matter.  
        /// </summary>
        public virtual void ProduceParallelSamples(Action<StackSourceSample> callback, int desiredParallelism = 0)
        {
            if (desiredParallelism == 0)
                desiredParallelism = Environment.ProcessorCount * 5 / 4 + 1;

            var freeBlocks = new ConcurrentBag<StackSourceSample[]>();
            bool sampleImmutable = SamplesImmutable;

            // Create a set of workers waiting for work from the dispatcher.  
            var workerQueues = new BlockingCollection<StackSourceSample[]>[desiredParallelism];
            var workers = new Thread[desiredParallelism];
            for (int i = 0; i < workerQueues.Length; i++)
            {
                workerQueues[i] = new BlockingCollection<StackSourceSample[]>(3);
                var worker = workers[i] = new Thread(delegate(object workQueueObj)
                {
                    // Set me priority lower so that the producer can always outrun the consumer.  
                    Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                    var workQueue = (BlockingCollection<StackSourceSample[]>)workQueueObj;
                    for (; ; )
                    {
                        StackSourceSample[] readerSampleBlock;
                        if (!workQueue.TryTake(out readerSampleBlock, -1))
                            break;

                        for (int j = 0; j < readerSampleBlock.Length; j++)
                            callback(readerSampleBlock[j]);
                        freeBlocks.Add(readerSampleBlock);       // Recycle sample object. 
                    }
                });
                worker.Start(workerQueues[i]);
            }

            var curIdx = 0;
            StackSourceSample[] writerSampleBlock = null;
            ProduceSamples(delegate(StackSourceSample sample)
            {
                if (writerSampleBlock == null)
                {
                    freeBlocks.TryTake(out writerSampleBlock);
                    if (writerSampleBlock == null)
                    {
                        writerSampleBlock = new StackSourceSample[1000];
                        if (!sampleImmutable)
                        {
                            for (int i = 0; i < writerSampleBlock.Length; i++)
                                writerSampleBlock[i] = new StackSourceSample(this);
                        }
                    }
                }

                if (sampleImmutable)
                    writerSampleBlock[curIdx] = sample;
                else
                {
                    var sampleCopy = writerSampleBlock[curIdx];
                    sampleCopy.Count = sample.Count;
                    sampleCopy.Metric = sample.Metric;
                    sampleCopy.StackIndex = sample.StackIndex;
                    sampleCopy.Scenario = sample.Scenario;
                    sampleCopy.TimeRelMSec = sample.TimeRelMSec;
                }

                // We have a full block, give it to a worker.  
                curIdx++;
                if (curIdx >= writerSampleBlock.Length)
                {
                    // Add it to someone's work queue
                    int workerNum = BlockingCollection<StackSourceSample[]>.AddToAny(workerQueues, writerSampleBlock);
                    curIdx = 0;
                    writerSampleBlock = null;
                }
            });

            // Indicate to the workers they are done.   This will cause them to exit.  
            for (int i = 0; i < workerQueues.Length; i++)
                workerQueues[i].CompleteAdding();

            // Wait for all my workers to die before returning.  
            for (int i = 0; i < workers.Length; i++)
                workers[i].Join();

            // Write out any stragglers.  (do it after waiting since it keeps them in order (roughly).  
            for (int i = 0; i < curIdx; i++)
                callback(writerSampleBlock[i]);
        }

        // These are optional
        /// <summary>
        /// If this stack source is a source that simply groups another source, get the base source.  It will return
        /// itself if there is no base source.  
        /// </summary>
        public virtual StackSource BaseStackSource { get { return this; } }
        /// <summary>
        /// If this source supports fetching the samples by index, this is how you get it.  Like ProduceSamples the sample that
        /// is returned is not allowed to be modified.   Also the returned sample will become invalid the next time GetSampleIndex
        /// is called (we reuse the StackSourceSample on each call)
        /// </summary>
        public virtual StackSourceSample GetSampleByIndex(StackSourceSampleIndex sampleIndex) { return null; }
        public virtual int SampleIndexLimit { get { return 0; } }

        public virtual double SampleTimeRelMSecLimit { get { return 0; } }

        public virtual int ScenarioCount { get { return 0; } }

        /// <summary>
        /// StackSources can optionally support a sampling rate.   If the source supports it it will return
        /// non-null for the current sampling rate (1 if it is doing nothing).    Sampling is a way of speeding
        /// things up.  If you sample at a rate of 10, it means that only one out of every 10 samples is actually
        /// produced by 'ProduceSamples'.   Note that it is expected that when the sampling rate is set the 
        /// source will correspondingly adjust the CountMultiplier, so that the total will look like no sampling
        /// is occuring 
        /// </summary>
        public virtual float? SamplingRate { get { return null; } set { } }

        // GraphSource Support (optional)
        /// <summary>
        /// If each 'callstack' is really a node in a graph (like MemoryGraphStackSource)
        /// Then return true.  If this returns true 'GetRefs' works. 
        /// </summary>
        public virtual bool IsGraphSource { get { return false; } }
        /// <summary>
        /// Only used if IsGraphSource==true.   If 'dir' is 'From' Calls 'callback' for node that is refered to FROM nodeIndex.
        /// If 'dir' is 'To' then it calls 'callback' for every node that refers TO nodeIndex.
        /// </summary>
        public virtual void GetRefs(StackSourceSampleIndex nodeIndex, RefDirection dir, Action<StackSourceSampleIndex> callback) { }

#if TOSTRING_FTNS
        // For debugging.
        public void Dump(string fileName)
        {
            using (var writer = File.CreateText(fileName))
                Dump(writer);
        }
        public void Dump(TextWriter writer)
        {
            writer.WriteLine("<StackSource>");
            writer.WriteLine(" <Samples>");
            ProduceSamples(delegate(StackSourceSample sample)
            {
                writer.Write("  ");
                writer.WriteLine(this.ToString(sample));
            });
            writer.WriteLine(" </Samples>");
            writer.WriteLine("</StackSource>");
        }
#endif
    }

    public enum RefDirection { From, To };

    /// <summary>
    /// Samples have stacks (lists of frames, each frame contains a name) associated with them.  This interface allows you to get 
    /// at this information.  We don't use normal objects to represent these but rather give each stack (and frame) a unique
    /// (dense) index.   This has a number of advantages over using objects to represent the stack.
    /// 
    ///     * Indexes are very serialization friendly, and this data will be presisted.  Thus indexes are the natural form for data on disk. 
    ///     * It allows the data to be read from the serialized format (disk) lazily in a very straightfoward fashion, keeping only the
    ///         hottest elements in memory.  
    ///     * Users of this API can associate additional data with the call stacks or frames trivially and efficiently simply by
    ///         having an array indexed by the stack or frame index.   
    ///         
    /// So effecively a StackSourceStacks is simply a set of 'Get' methods that allow you to look up information given a Stack or
    /// frame index.  
    /// </summary>
    public abstract class StackSourceStacks
    {
        /// <summary>
        /// Given a call stack, return the call stack of the caller.   This function can return StackSourceCallStackIndex.Discard
        /// which means that this sample should be discarded.  
        /// </summary>
        public abstract StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex);
        /// <summary>
        /// For efficiency, m_frames are assumed have a integer ID instead of a string name that
        /// is unique to the frame.  Note that it is expected that GetFrameIndex(x) == GetFrameId(y) 
        /// then GetFrameName(x) == GetFrameName(y).   The converse does NOT have to be true (you 
        /// can reused the same name for distict m_frames, however this can be confusing to your
        /// users, so be careful.  
        /// </summary>
        public abstract StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex);
        /// <summary>
        /// FilterStackSources can combine more than one frame into a given frame.  It is useful to know
        /// how many times this happened.   Returning 0 means no combining happened.  This metric does
        /// not include grouping, but only folding.  
        /// </summary>
        public virtual int GetNumberOfFoldedFrames(StackSourceCallStackIndex callStackIndex)
        {
            return 0;
        }
        /// <summary>
        /// Get the frame name from the FrameIndex.   If 'verboseName' is true then full module path is included.
        /// </summary>
        public abstract string GetFrameName(StackSourceFrameIndex frameIndex, bool verboseName);
        /// <summary>
        /// all StackSourceCallStackIndex are guarenteed to be less than this.  Allocate an array of this size to associate side information
        /// </summary>
        public abstract int CallStackIndexLimit { get; }
        /// <summary>
        /// all StackSourceFrameIndex are guarenteed to be less than this.  Allocate an array of this size to associate side information
        /// </summary>
        public abstract int CallFrameIndexLimit { get; }
        public int StackDepth(StackSourceCallStackIndex callStackIndex)
        {
            int ret = 0;
            while (callStackIndex != StackSourceCallStackIndex.Invalid)
            {
                callStackIndex = GetCallerIndex(callStackIndex);
                ret++;
            }
            return ret;
        }


#if TOSTRING_FTNS
        public string ToString(StackSourceSample sample)
        {
            StringBuilder sb = new StringBuilder();
            return ToString(sample, sb);
        }
        public string ToString(StackSourceSample sample, StringBuilder sb)
        {
            sb.Append("<StackSourceSample");
            sb.Append(" Metric=\"").Append(sample.Metric.ToString("f1")).Append('"');
            sb.Append(" TimeRelMSec=\"").Append(sample.TimeRelMSec.ToString("n3")).Append('"');
            sb.Append(" SampleIndex=\"").Append(sample.SampleIndex.ToString()).Append('"');
            sb.Append(">").AppendLine();
            sb.AppendLine(ToString(sample.StackIndex));
            sb.Append("</StackSourceSample>");
            return sb.ToString();
        }
        public string ToString(StackSourceCallStackIndex callStackIndex)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(" <CallStack Index =\"").Append((int)callStackIndex).Append("\">").AppendLine();
            for (int i = 0; callStackIndex != StackSourceCallStackIndex.Invalid; i++)
            {
                if (i >= 300)
                {
                    sb.AppendLine("  <Truncated/>");
                    break;
                }
                sb.Append(ToString(GetFrameIndex(callStackIndex), callStackIndex)).AppendLine();
                callStackIndex = GetCallerIndex(callStackIndex);
            }
            sb.Append(" </CallStack>");
            return sb.ToString();
        }
        public string ToString(StackSourceFrameIndex frameIndex, StackSourceCallStackIndex stackIndex = StackSourceCallStackIndex.Invalid)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("  <Frame");
            if (stackIndex != StackSourceCallStackIndex.Invalid)
                sb.Append(" StackID=\"").Append(((int)stackIndex).ToString()).Append("\"");
            sb.Append(" FrameID=\"").Append(((int)frameIndex).ToString()).Append("\"");
            sb.Append(" Name = \"").Append(XmlUtilities.XmlEscape(GetFrameName(frameIndex, false))).Append("\"");
            sb.Append("/>");
            return sb.ToString();
        }
#endif
    }

    /// <summary>
    /// StackSourceSample represents a single sample that has a stack.  StackSource.GetNextSample returns these.  
    /// </summary>
    public class StackSourceSample
    {
        public StackSourceCallStackIndex StackIndex { get; set; }
        public float Metric { get; set; }

        // The rest of these are optional.  
        public StackSourceSampleIndex SampleIndex { get; set; }  // This identifies the sample uniquely in the source.  
        public double TimeRelMSec { get; set; }
        /// <summary>
        /// Normally the count of a sample is 1, however when you take a statistical sample, and you also have 
        /// other constraints (like you do when you are going a sample of heap memory),  you may need to have the
        /// count adjusted to something else.
        /// </summary>
        public float Count { get; set; }
        public int Scenario { get; set; }

#if TOSTRING_FTNS
        public override string ToString()
        {
            return String.Format("<Sample Metric=\"{0:f1}\" TimeRelMSec=\"{1:f3}\" StackIndex=\"{2}\" SampleIndex=\"{3}\">",
                Metric, TimeRelMSec, StackIndex, SampleIndex);
        }
        public string ToString(StackSource source)
        {
            return source.ToString(this);
        }
#endif

        #region protected
        public StackSourceSample(StackSource source) { SampleIndex = StackSourceSampleIndex.Invalid; Count = 1; }
        public StackSourceSample(StackSourceSample template)
        {
            StackIndex = template.StackIndex;
            Metric = template.Metric;
            TimeRelMSec = template.TimeRelMSec;
            SampleIndex = template.SampleIndex;
            Scenario = template.Scenario;
            Count = template.Count;
        }
        #endregion
    }

    /// <summary>
    /// Identifies a particular sample from the sample source, it allows 3rd parties to attach additional
    /// information to the sample by creating an array indexed by sampleIndex.  
    /// </summary>
    public enum StackSourceSampleIndex { Invalid = -1 };

    /// <summary>
    /// An opaque handle that are 1-1 with a complete call stack
    /// 
    /// </summary>
    public enum StackSourceCallStackIndex
    {
        Start = 0,             // The first real call stack index (after the pseudo-ones before this)
        Invalid = -1,          // Returned when there is no caller (top of stack)
    };

    /// <summary>
    /// Identifies a particular frame within a stack   It represents a particular instruction pointer (IP) location 
    /// in the code or a group of such locations.  
    /// </summary>
    public enum StackSourceFrameIndex
    {
        Root = 0,              // Pseduo-node representing the root of all stacks
        Broken = 1,            // Pseduo-frame that represents the caller of all broken stacks. 
        Unknown = 2,           // Unknown what to do (Must be before the 'special ones below')  // Non negative represents normal m_frames (e.g. names of methods)
        Overhead = 3,          // Profiling overhead (rundown)
        Start = 4,             // The first real call stack index (after the pseudo-ones before this)

        Invalid = -1,           // Should not happen (uninitialized) (also means completely folded away)
        Discard = -2,           // Sample has been filtered out (useful for filtering stack sources)
    };

    public enum StackSourceModuleIndex { Start = 0, Invalid = -1 };

    /// <summary>
    /// This stack source takes another and copies out all its events.   This allows you to 'replay' the source 
    /// efficiently when the original source only does this inefficiently.  
    /// </summary>
    public class CopyStackSource : StackSource
    {
        public CopyStackSource() { }
        public CopyStackSource(StackSourceStacks sourceStacks)
        {
            m_sourceStacks = sourceStacks;
        }
        public StackSourceSample AddSample(StackSourceSample sample)
        {
            // TODO assert that the samples are associated with this source.  
            var sampleCopy = new StackSourceSample(sample);
            sampleCopy.SampleIndex = (StackSourceSampleIndex)m_samples.Count;
            m_samples.Add(sampleCopy);
            if (sampleCopy.TimeRelMSec > m_sampleTimeRelMSecLimit)
                m_sampleTimeRelMSecLimit = sampleCopy.TimeRelMSec;
            return sampleCopy;
        }
        public static CopyStackSource Clone(StackSource source)
        {
            var ret = new CopyStackSource(source);
            source.ProduceSamples(delegate(StackSourceSample sample)
            {
                ret.AddSample(sample);
            });
            return ret;
        }

        // Support for sample indexes.  This allows you to look things up information in the sample
        // after being processed the first time.
        public override StackSourceSample GetSampleByIndex(StackSourceSampleIndex sampleIndex)
        {
            return m_samples[(int)sampleIndex];
        }
        public override int SampleIndexLimit
        {
            get { return m_samples.Count; }
        }

        public override double SampleTimeRelMSecLimit { get { return m_sampleTimeRelMSecLimit; } }
        public override void ProduceSamples(Action<StackSourceSample> callback)
        {
            for (int i = 0; i < m_samples.Count; i++)
                callback(m_samples[i]);
        }
        public override bool SamplesImmutable { get { return true; } }
        public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex)
        {
            return m_sourceStacks.GetCallerIndex(callStackIndex);
        }
        public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex)
        {
            return m_sourceStacks.GetFrameIndex(callStackIndex);
        }
        public override string GetFrameName(StackSourceFrameIndex frameIndex, bool fullModulePath)
        {
            return m_sourceStacks.GetFrameName(frameIndex, fullModulePath);
        }
        public override int CallStackIndexLimit
        {
            get { if (m_sourceStacks == null) return 0; return m_sourceStacks.CallStackIndexLimit; }
        }
        public override int CallFrameIndexLimit
        {
            get { if (m_sourceStacks == null) return 0; return m_sourceStacks.CallFrameIndexLimit; }
        }
        public StackSourceStacks SourceStacks { get { return m_sourceStacks; } }
        #region private
        protected GrowableArray<StackSourceSample> m_samples;
        protected double m_sampleTimeRelMSecLimit;
        protected StackSourceStacks m_sourceStacks;
        #endregion
    }

    /// <summary>
    /// Like CopyStackSource InternStackSource copies the samples. however unlike CopyStackSource
    /// InternStackSource copies all the information in the stacks too (mapping stack indexes to names)
    /// Thus it never refers to the original source again).   It also interns the stacks making for 
    /// an efficient representation of the data.   This is useful when the original source is expensive 
    /// to iterate over.   
    /// </summary>
    public class InternStackSource : CopyStackSource
    {
        /// <summary>
        /// Compute the difference between two sources of stacks.
        /// </summary>
        public static InternStackSource Diff(StackSource source, StackSource baselineSource)
        {
            return Diff(source, source, baselineSource, baselineSource);
        }
        /// <summary>
        /// Compute only the delta of source from the baseline.  This variation allows you to specify
        /// the unfiltered names (the sourceStacks and baselineStacks) but otherwise keep the filtering.  
        /// </summary>
        public static InternStackSource Diff(StackSource source, StackSourceStacks sourceStacks,
                                            StackSource baselineSource, StackSourceStacks baselineStacks)
        {
            // The ability to pass the StackSourceStacks is really just there to bypass grouping
            Debug.Assert(source == sourceStacks || source.BaseStackSource == sourceStacks);
            Debug.Assert(baselineSource == baselineStacks || baselineSource.BaseStackSource == baselineStacks);

            var ret = new InternStackSource();

            ret.ReadAllSamples(source, sourceStacks, 1.0F);
            ret.ReadAllSamples(baselineSource, baselineStacks, -1.0F);
            ret.Interner.DoneInterning();
            return ret;
        }

        public InternStackSource(StackSource source, StackSourceStacks sourceStacks)
            : this()
        {
            ReadAllSamples(source, sourceStacks, 1.0F);
            Interner.DoneInterning();
        }
        public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex)
        {
            return Interner.GetCallerIndex(callStackIndex);
        }
        public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex)
        {
            return Interner.GetFrameIndex(callStackIndex);
        }
        public override string GetFrameName(StackSourceFrameIndex frameIndex, bool fullModulePath)
        {
            // TODO does this belong in the interner?
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
            return Interner.GetFrameName(frameIndex, fullModulePath);
        }
        public StackSourceModuleIndex GetModuleIndex(StackSourceFrameIndex frameIndex)
        {
            return Interner.GetModuleIndex(frameIndex);
        }

        public override int CallStackIndexLimit
        {
            get { return (int)StackSourceCallStackIndex.Start + Interner.CallStackCount; }
        }
        public override int CallFrameIndexLimit
        {
            get { return (int)(StackSourceFrameIndex.Start + Interner.FrameCount); }
        }

        #region protected
        protected InternStackSource()
        {
            Interner = new StackSourceInterner();
        }
        protected void ReadAllSamples(StackSource source, StackSourceStacks stackLookup, float scaleFactor)
        {
            var ctr = 0;
            source.ProduceSamples(delegate(StackSourceSample sample)
            {
                var sampleCopy = new StackSourceSample(sample);
                if (scaleFactor != 1.0F)
                {
                    sampleCopy.Metric *= scaleFactor;
                    if (scaleFactor < 0)
                        sampleCopy.Count = -sampleCopy.Count;
                }
                sampleCopy.SampleIndex = (StackSourceSampleIndex)m_samples.Count;
                sampleCopy.StackIndex = InternFullStackFromSource(sampleCopy.StackIndex, stackLookup);
                m_samples.Add(sampleCopy);
                if (sampleCopy.TimeRelMSec > m_sampleTimeRelMSecLimit)
                    m_sampleTimeRelMSecLimit = sampleCopy.TimeRelMSec;
                if (ctr > 8192)
                {
                    System.Threading.Thread.Sleep(0);       // allow interruption
                    ctr = 0;
                }
            });
        }
        protected StackSourceInterner Interner;
        #endregion
        #region private
        /// <summary>
        /// InternFullStackFromSource will take a call stack 'baseCallStackIndex' from the source 'source' and completely copy it into
        /// the intern stack source (interning along the way of course).   Logically baseCallStackIndex has NOTHING to do with any of the
        /// call stack indexes in the intern stack source.  
        /// </summary>
        private StackSourceCallStackIndex InternFullStackFromSource(StackSourceCallStackIndex baseCallStackIndex, StackSourceStacks source)
        {
            if (baseCallStackIndex == StackSourceCallStackIndex.Invalid)
                return StackSourceCallStackIndex.Invalid;

            var baseCaller = source.GetCallerIndex(baseCallStackIndex);
            var baseFrame = source.GetFrameIndex(baseCallStackIndex);

            var baseFullFrameName = source.GetFrameName(baseFrame, true);
            var moduleName = "";
            var frameName = baseFullFrameName;
            var index = baseFullFrameName.IndexOf('!');
            if (index >= 0)
            {
                moduleName = baseFullFrameName.Substring(0, index);
                frameName = baseFullFrameName.Substring(index + 1);
            }

            var myModuleIndex = Interner.ModuleIntern(moduleName);
            var myFrameIndex = Interner.FrameIntern(frameName, myModuleIndex);
            var ret = Interner.CallStackIntern(myFrameIndex, InternFullStackFromSource(baseCaller, source));
            return ret;
        }
        #endregion
    }

    /// <summary>
    /// StackSourceInterner is a helper class that knows how to intern module, frame and call stacks. 
    /// </summary>
    public class StackSourceInterner
    {
        public StackSourceInterner(
            int estNumCallStacks = 5000, int estNumFrames = 1000, int estNumModules = 100,
            StackSourceFrameIndex frameStartIndex = StackSourceFrameIndex.Start,
            StackSourceCallStackIndex callStackStartIndex = StackSourceCallStackIndex.Start,
            StackSourceModuleIndex moduleStackStartIndex = StackSourceModuleIndex.Start)
        {
            m_modules = new GrowableArray<string>(estNumModules);
            m_frames = new GrowableArray<FrameInfo>(estNumFrames);
            m_callStacks = new GrowableArray<CallStackInfo>(estNumCallStacks);
            m_moduleIntern = new Dictionary<string, StackSourceModuleIndex>(estNumModules);
            m_frameIntern = new Dictionary<FrameInfo, StackSourceFrameIndex>(estNumFrames);
            m_callStackIntern = new Dictionary<CallStackInfo, StackSourceCallStackIndex>(estNumCallStacks);

            m_frameStartIndex = frameStartIndex;
            m_callStackStartIndex = callStackStartIndex;
            m_moduleStackStartIndex = moduleStackStartIndex;
        }
        /// <summary>
        /// As an optimization, if you are done adding new nodes, then you can call this routine can abadon
        /// some tables only needed during the interning phase.
        /// </summary>
        public void DoneInterning()
        {
            m_moduleIntern = null;
            m_frameIntern = null;
            m_callStackIntern = null;
        }
        public StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex)
        {
            return m_callStacks[callStackIndex - m_callStackStartIndex].callerIndex;
        }
        public StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex)
        {
            return m_callStacks[callStackIndex - m_callStackStartIndex].frameIndex;
        }
        public StackSourceModuleIndex GetModuleIndex(StackSourceFrameIndex frameIndex)
        {
            var framesIndex = frameIndex - m_frameStartIndex;
            Debug.Assert(frameIndex >= 0);
            return m_frames[framesIndex].ModuleIndex;
        }

        /// <summary>
        /// If you intern frames as derived frames, when GetFrameName is called the interner needs to know
        /// how to look up the derived frame from its index.  This is the function that is called.  
        /// 
        /// It is called with the frame index and a boolean which indicats whether the full path of the module 
        /// should be specified, and returns the frame string. 
        /// </summary>
        public Func<StackSourceFrameIndex, bool, string> FrameNameLookup { get; set; }

        /// <summary>
        /// Get a name from a frame index.  If the frame index is a 
        /// </summary>
        public string GetFrameName(StackSourceFrameIndex frameIndex, bool fullModulePath)
        {
            var frameIndexOffset = (int)(frameIndex - m_frameStartIndex);
            Debug.Assert(0 <= frameIndexOffset && frameIndexOffset < m_frames.Count);
            var frameName = m_frames[frameIndexOffset].FrameName;
            var baseFrameIndex = m_frames[frameIndexOffset].BaseFrameIndex;
            if (baseFrameIndex != StackSourceFrameIndex.Invalid)
            {
                string baseName;
                if (FrameNameLookup != null)
                    baseName = FrameNameLookup(baseFrameIndex, fullModulePath);
                else
                    baseName = "Frame " + ((int)baseFrameIndex).ToString();
                return baseName + " " + frameName;
            }
            var moduleName = m_modules[m_frames[frameIndexOffset].ModuleIndex - m_moduleStackStartIndex];
            if (moduleName.Length == 0)
                return frameName;

            if (!fullModulePath)
            {
                var lastBackSlash = moduleName.LastIndexOf('\\');
                if (lastBackSlash >= 0)
                    moduleName = moduleName.Substring(lastBackSlash + 1);
            }
            // Remove a .dll or .exe extention 
            // TODO should we be doing this here?  This feels like a presentation transformation
            // and we are at the semantic model layer.  
            int lastDot = moduleName.LastIndexOf('.');
            if (lastDot == moduleName.Length - 4 &&
                (moduleName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                moduleName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
            {
                moduleName = moduleName.Substring(0, lastDot);
            }

            return moduleName + "!" + frameName;
        }

        // Used to create new nodes 
        public StackSourceModuleIndex ModuleIntern(string moduleName)
        {
            StackSourceModuleIndex ret;
            if (!m_moduleIntern.TryGetValue(moduleName, out ret))
            {
                ret = (StackSourceModuleIndex)(m_moduleStackStartIndex + m_modules.Count);
                m_modules.Add(moduleName);
                m_moduleIntern.Add(moduleName, ret);
            }
            return ret;
        }
        public StackSourceFrameIndex FrameIntern(string frameName, StackSourceModuleIndex moduleIndex)
        {
            Debug.Assert(frameName != null);
            StackSourceFrameIndex ret;
            FrameInfo frame = new FrameInfo(frameName, moduleIndex);
            if (!m_frameIntern.TryGetValue(frame, out ret))
            {
                ret = (m_frameStartIndex + m_frames.Count);
                m_frames.Add(frame);
                m_frameIntern.Add(frame, ret);
            }
            return ret;
        }
        /// <summary>
        /// You can also create frames out of other frames using this method.  Given an existing frame, and
        /// a suffix 'frameSuffix' 
        /// </summary>
        public StackSourceFrameIndex FrameIntern(StackSourceFrameIndex frameIndex, string frameSuffix)
        {
            // In order to use this, you must 
            Debug.Assert(FrameNameLookup != null);
            Debug.Assert(frameSuffix != null);

            StackSourceFrameIndex ret;
            FrameInfo frame = new FrameInfo(frameSuffix, frameIndex);
            if (!m_frameIntern.TryGetValue(frame, out ret))
            {
                ret = (m_frameStartIndex + m_frames.Count);
                m_frames.Add(frame);
                m_frameIntern.Add(frame, ret);
            }
            return ret;
        }

        public StackSourceCallStackIndex CallStackIntern(StackSourceFrameIndex frameIndex, StackSourceCallStackIndex callerIndex)
        {
            StackSourceCallStackIndex ret;
            CallStackInfo callStack = new CallStackInfo(frameIndex, callerIndex);
            if (!m_callStackIntern.TryGetValue(callStack, out ret))
            {
                ret = (StackSourceCallStackIndex)(m_callStacks.Count + m_callStackStartIndex);
                m_callStacks.Add(callStack);
                m_callStackIntern.Add(callStack, ret);
            }
            return ret;
        }

        public int FrameCount { get { return m_frames.Count; } }
        public int CallStackCount { get { return m_callStacks.Count; } }

        #region private
        private struct FrameInfo : IEquatable<FrameInfo>
        {
            public FrameInfo(string frameName, StackSourceModuleIndex moduleIndex)
            {
                this.ModuleIndex = moduleIndex;
                this.FrameName = frameName;
                this.BaseFrameIndex = StackSourceFrameIndex.Invalid;
            }
            public FrameInfo(string frameSuffix, StackSourceFrameIndex baseFrame)
            {
                this.ModuleIndex = StackSourceModuleIndex.Invalid;
                this.BaseFrameIndex = baseFrame;
                this.FrameName = frameSuffix;
            }
            // TODO we could make this smaller if we care since BaseFrame and ModuleIndex are never used together.  
            public readonly StackSourceFrameIndex BaseFrameIndex;
            public readonly StackSourceModuleIndex ModuleIndex;
            public readonly string FrameName;       // This is the suffix if this is a derived frame

            public override int GetHashCode()
            {
                return (int)ModuleIndex + (int)BaseFrameIndex + FrameName.GetHashCode();
            }
            public override bool Equals(object obj) { throw new NotImplementedException(); }
            public bool Equals(FrameInfo other)
            {
                return ModuleIndex == other.ModuleIndex && BaseFrameIndex == other.BaseFrameIndex && FrameName == other.FrameName;
            }
        }
        private struct CallStackInfo : IEquatable<CallStackInfo>
        {
            public CallStackInfo(StackSourceFrameIndex frameIndex, StackSourceCallStackIndex callerIndex)
            {
                this.frameIndex = frameIndex;
                this.callerIndex = callerIndex;
            }
            public readonly StackSourceFrameIndex frameIndex;
            public readonly StackSourceCallStackIndex callerIndex;

            public override int GetHashCode()
            {
                return (int)callerIndex + (int)frameIndex * 0x10000;
            }
            public override bool Equals(object obj) { throw new NotImplementedException(); }
            public bool Equals(CallStackInfo other)
            {
                return frameIndex == other.frameIndex && callerIndex == other.callerIndex;
            }
        };

        // maps (moduleIndex - m_moduleStackStartIndex) to module name 
        private GrowableArray<string> m_modules;
        // maps (frameIndex - m_frameStartIndex) to frame information
        private GrowableArray<FrameInfo> m_frames;
        // mapx (callStackIndex - m_callStackStartIndex) to call stack information (frame and caller)
        private GrowableArray<CallStackInfo> m_callStacks;

        // Only needed during reading
        // Given a Call Stack index, return the list of call stack indexes that that routine calls.  
        private Dictionary<CallStackInfo, StackSourceCallStackIndex> m_callStackIntern;
        private Dictionary<FrameInfo, StackSourceFrameIndex> m_frameIntern;
        private Dictionary<string, StackSourceModuleIndex> m_moduleIntern;

        // TO allow the interner to 'open' an existing stackSource, we make it flexible about where indexes start.
        // The typcial case these are all 0.  
        private StackSourceFrameIndex m_frameStartIndex;
        private StackSourceCallStackIndex m_callStackStartIndex;
        private StackSourceModuleIndex m_moduleStackStartIndex;
        #endregion
    }

    /// <summary>
    /// An enum representing a displayed histogram bucket (one character in a histogram string).
    /// </summary>
    public enum HistogramCharacterIndex
    {
        Invalid = -1
    }

    /// <summary>
    /// A Histogram is conceputually an array of floating point values.   A Histogram Controller
    /// contains all the information besides the values themselves need to understand the array
    /// of floating point value.   There are alot of Histograms, however they all tend to share
    /// the same histogram controller.   Thus Histograms know their Histogram controller, but not
    /// the reverse.  
    /// 
    /// Thus HistogramContoller is a abstract class (we have one for time, and one for scenarios).  
    ///
    /// HistogramControllers are responsible for:
    /// 
    /// - Adding a sample to the histogram for a node (see <see cref="AddSample"/>)
    /// - Converting a histogram to its string representation see (<see cref="GetDisplayString"/>)
    /// - Managing the size and scale of histograms and their corresponding display strings
    /// </summary>
    public abstract class HistogramController
    {
        /// <summary>
        /// Initialize a new HistogramController.
        /// </summary>
        /// <param name="tree">The CallTree that this HistogramController controls.</param>
        protected HistogramController(CallTree tree)
        {
            BucketCount = 32;
            CharacterCount = 32;
            Tree = tree;
        }
        /// <summary>
        /// The scale factor for histograms controlled by this HistogramController.
        /// </summary>
        public double Scale
        {
            get
            {
                lock (this)
                {
                    if (m_scale == 0.0)
                    {
                        m_scale = CalculateScale();
                    }
                }
                return m_scale;
            }
        }
        /// <summary>
        /// The number of buckets in each histogram controlled by this HistogramController.
        /// </summary>
        public int BucketCount { get; protected set; }
        /// <summary>
        /// The number of characters in the display string for histograms controlled by this HistogramController.
        /// Buckets are a logial concept, where CharacterCount is a visual concept (how many you can see on the 
        /// screen right now).  
        /// </summary>
        public int CharacterCount { get; protected set; }
        /// <summary>
        /// The CallTree managed by this HistogramController.
        /// </summary>
        public CallTree Tree { get; protected set; }
        /// <summary>
        /// Force recalculation of the scale parameter.
        /// </summary>
        public void InvalidateScale()
        {
            m_scale = 0.0;
        }

        // Abstract methods
        /// <summary>
        /// Add a sample to the histogram for a node.
        /// </summary>
        /// <param name="histogram">The histogram to add this sample to. Must be controlled by this HistogramController.</param>
        /// <param name="sample">The sample to add.</param>
        /// <remarks>
        /// Overriding classes are responsible for extracting the metric, scaling the metric,
        /// determining the appropriate bucket or buckets, and adding the metric to the histogram using <see cref="Histogram.AddMetric"/>.
        /// </remarks>
        public abstract void AddSample(Histogram histogram, StackSourceSample sample);
        /// <summary>
        /// Gets human-readable information about a range of histogram characters.
        /// </summary>
        /// <param name="start">The start character index (inclusive).</param>
        /// <param name="end">The end character index (exclusive).</param>
        /// <param name="histogram">The histogram.</param>
        /// <returns>A string containing information about the contents of that character range.</returns>
        public abstract string GetInfoForCharacterRange(HistogramCharacterIndex start, HistogramCharacterIndex end, Histogram histogram);
        /// <summary>
        /// Convert a histogram into its display string.
        /// </summary>
        /// <param name="histogram">The histogram to convert to a string.</param>
        /// <returns>A string suitable for GUI display.</returns>
        public abstract string GetDisplayString(Histogram histogram);

        // Static utility functions
        /// <summary>
        /// A utility function that turns an array of floats into a ASCII character graph.  
        /// </summary>
        public static string HistogramString(IEnumerable<float> buckets, int bucketCount, double scale, int maxLegalBucket = 0)
        {
            if (buckets == null)
                return "";
            var chars = new char[bucketCount];
            int i = 0;
            foreach (float metric in buckets)
            {
                char val = '_';
                if (0 < maxLegalBucket && maxLegalBucket <= i)
                    val = '?';
                int valueBucket = (int)(metric / scale * 10);       // TODO should we round?
                if (metric > 0)
                {
                    // Scale the metric acording to the wishes of the client
                    if (valueBucket < 10)
                    {
                        val = (char)('0' + valueBucket);
                        if (valueBucket == 0 && (metric / scale < .01))
                        {
                            val = 'o';
                            if (metric / scale < .001)
                                val = '.';
                        }
                    }
                    else
                    {
                        valueBucket -= 10;
                        if (valueBucket < 25)
                            val = (char)('A' + valueBucket);          // We go through the alphabet too.
                        else
                            val = '*';                                // Greater than 3.6X CPUs 
                    }
                }
                else if (metric < 0)
                {
                    valueBucket = -valueBucket;
                    // TODO we are not symetric, we use digits on the positive side but not negative.  
                    if (valueBucket < 25)
                        val = (char)('a' + valueBucket);          // We go through the alphabet too.
                    else
                        val = '@';
                }
                chars[i] = val;
                i++;
            }
            return new string(chars);
        }
        /// <summary>
        /// A utility function that turns an array of floats into a ASCII character graph.  
        /// </summary>
        public static string HistogramString(float[] buckets, double scale, int maxLegalBucket = 0)
        {
            return (buckets == null) ? "" : HistogramString(buckets, buckets.Length, scale, maxLegalBucket);
        }

        /// <summary>
        /// Calculate the scale factor for this histogram.
        /// </summary>
        /// <returns>The scale factor for this histogram.</returns>
        protected abstract double CalculateScale();
        /// <summary>
        /// Calculates an average scale factor for a histogram.
        /// </summary>
        /// <param name="hist">The root histogram to calculate against.</param>
        /// <returns>A scale factor that will normalize the maximum value to 200%.</returns>
        protected double CalculateAverageScale(Histogram hist)
        {
            // Return half the max of the absolute values in the top histogram 
            double max = 0;
            for (int i = 0; i < hist.Count; i++)
                max = Math.Max(Math.Abs(hist[i]), max);
            return max / 2;
        }

        #region private
        /// <summary>
        /// The scale parameter. 0.0 if uncalculated.
        /// </summary>
        private double m_scale;
        #endregion
    }

    /// <summary>
    /// A <see cref="HistogramController"/> that groups histograms by scenarios.
    /// </summary>
    public class ScenarioHistogramController : HistogramController
    {
        /// <summary>
        /// Initialize a new ScenarioHistogramController.
        /// </summary>
        /// <param name="tree">The CallTree to manage.</param>
        /// <param name="scenarios">An ordered array of scenario IDs to display.</param>
        /// <param name="totalScenarios">The total number of possible scenarios that can be supplied by the underlying StackSource.
        /// This number might be larger than the highest number in <paramref name="scenarios"/>.</param>
        /// <param name="scenarioNames">The names of the scenarios (for UI use).</param>
        public ScenarioHistogramController(CallTree tree, int[] scenarios, int totalScenarios, string[] scenarioNames = null)
            : base(tree)
        {
            Debug.Assert(totalScenarios > 0);

            BucketCount = totalScenarios;
            CharacterCount = Math.Min(scenarios.Length, CharacterCount);

            m_scenariosFromCharacter = new List<int>[CharacterCount];
            m_characterFromScenario = new HistogramCharacterIndex[BucketCount];

            for (int i = 0; i < CharacterCount; i++)
            {
                m_scenariosFromCharacter[i] = new List<int>();
            }

            for (int i = 0; i < BucketCount; i++)
            {
                m_characterFromScenario[i] = HistogramCharacterIndex.Invalid;
            }

            for (int i = 0; i < scenarios.Length; i++)
            {
                var scenario = scenarios[i];
                var bucket = (i * CharacterCount) / scenarios.Length;

                m_characterFromScenario[scenario] = (HistogramCharacterIndex)bucket;
                m_scenariosFromCharacter[bucket].Add(scenario);
            }

            m_scenarioNames = scenarioNames;
        }

        /// <summary>
        /// Get a list of scenarios contained in a given bucket.
        /// </summary>
        /// <param name="bucket">The bucket to look up.</param>
        /// <returns>The scenarios contained in that bucket.</returns>
        public int[] GetScenariosForCharacterIndex(HistogramCharacterIndex bucket)
        {
            return m_scenariosFromCharacter[(int)bucket].ToArray();
        }

        /// <summary>
        /// Get a list of scenarios contained in a given bucket range.
        /// </summary>
        /// <param name="start">The start of the bucket range (inclusive).</param>
        /// <param name="end">The end of the bucket range (exclusive).</param>
        /// <returns>The scenarios contained in that range of buckets.</returns>
        public int[] GetScenariosForCharacterRange(HistogramCharacterIndex start, HistogramCharacterIndex end)
        {
            var rv = new List<int>();

            for (var bucket = start; bucket < end; bucket++)
            {
                rv.AddRange(m_scenariosFromCharacter[(int)bucket]);
            }

            return rv.ToArray();
        }

        /// <summary>
        /// Calculate the scale factor for all histograms controlled by this ScenarioHistogramController.
        /// </summary>
        /// <returns>
        /// In the current implementation, returns a scale that normalizes 100% to half of the maximum value at the root.
        /// </returns>
        protected override double CalculateScale()
        {
            return CalculateAverageScale(Tree.Root.InclusiveMetricByScenario);
        }

        /// <summary>
        /// Add a sample to a histogram controlled by this HistogramController.
        /// </summary>
        /// <param name="histogram">The histogram to add the sample to.</param>
        /// <param name="sample">The sample to add.</param>
        public override void AddSample(Histogram histogram, StackSourceSample sample)
        {
            histogram.AddMetric(sample.Metric, sample.Scenario);
        }

        /// <summary>
        /// Get the human-readable name for a scenario.
        /// </summary>
        /// <param name="scenario">The ID of the scenario to look up.</param>
        /// <returns>The human-readable name for that scenario.</returns>
        public string GetNameForScenario(int scenario)
        {
            if (m_scenarioNames != null)
                return m_scenarioNames[scenario];
            else
                return string.Format("<scenario #{0}>", scenario);
        }

        /// <summary>
        /// Get the human-readable names for all scenarios contained in a range of histogram characters.
        /// </summary>
        /// <param name="start">The (inclusive) start index of the range.</param>
        /// <param name="end">The (exclusive) end index of the range.</param>
        /// <param name="histogram">The histogram.</param>
        /// <returns>A comma-separated list of scenario names contained in that range.</returns>
        public override string GetInfoForCharacterRange(HistogramCharacterIndex start, HistogramCharacterIndex end, Histogram histogram)
        {
            var sb = new StringBuilder();
            for (var bucket = start; bucket < end; bucket++)
            {
                if (bucket == start)
                    sb.Append("Scenarios: ");
                foreach (int scenario in m_scenariosFromCharacter[(int)bucket])
                    sb.AppendFormat("{0}, ", GetNameForScenario(scenario));
            }
            if (2 <= sb.Length)
                sb.Remove(sb.Length - 2, 2);
            return sb.ToString();
        }

        /// <summary>
        /// Convert a histogram into a string suitable for UI display.
        /// </summary>
        /// <param name="histogram">The histogram to convert.</param>
        /// <returns>A string representing the histogram that is suitable for UI display.</returns>
        public override string GetDisplayString(Histogram histogram)
        {
            float[] displayBuckets = new float[CharacterCount];

            // Sort out and add up our metrics from the model buckets.
            // Each display bucket is the average of the scenarios in the corresponding model bucket.
            for (int i = 0; i < histogram.Count; i++)
            {
                if (m_characterFromScenario[i] != HistogramCharacterIndex.Invalid)
                    displayBuckets[(int)m_characterFromScenario[i]] += histogram[i];
            }

            for (int i = 0; i < displayBuckets.Length; i++)
            {
                displayBuckets[i] /= m_scenariosFromCharacter[i].Count;
            }

            return HistogramString(displayBuckets, Scale);
        }

        #region Private data members
        /// <summary>
        /// An array mapping each scenario to a bucket.
        /// </summary>
        private readonly HistogramCharacterIndex[] m_characterFromScenario;
        /// <summary>
        /// An array mapping each bucket to a list of scenarios.
        /// </summary>
        private readonly List<int>[] m_scenariosFromCharacter;
        /// <summary>
        /// An array mapping each scenario to its name.
        /// </summary>
        private readonly string[] m_scenarioNames;

        #endregion
    }

    /// <summary>
    /// A HistogramController holds all the information to understand the buckets of a histogram
    /// (basically everything except the array of metrics itself.   For time this is the
    /// start and end time  
    /// </summary>
    public class TimeHistogramController : HistogramController
    {
        /// <summary>
        /// Create a new TimeHistogramController.
        /// </summary>
        /// <param name="tree">The CallTree to control with this controller.</param>
        /// <param name="start">The start time of the histogram.</param>
        /// <param name="end">The end time of the histogram.</param>
        public TimeHistogramController(CallTree tree, double start, double end)
            : base(tree)
        {
            Start = start;
            End = end;
        }

        /// <summary>
        /// The start time of the histogram.
        /// </summary>
        public double Start { get; private set; }

        /// <summary>
        /// The end time of the histogram.
        /// </summary>
        public double End { get; private set; }

        /// <summary>
        /// Gets the start time for the histogram bucket represented by a character.
        /// </summary>
        /// <param name="bucket">The index of the character to look up.</param>
        /// <returns>The start time of the bucket represented by the character.</returns>
        public double GetStartTimeForBucket(HistogramCharacterIndex bucket)
        {
            Debug.Assert(bucket != HistogramCharacterIndex.Invalid);

            return (BucketDuration * (int)bucket) + Start;
        }

        /// <summary>
        /// The duration of time represented by each bucket.
        /// </summary>
        public double BucketDuration
        {
            get { return (End - Start) / BucketCount; }
        }

        protected override double CalculateScale()
        {
            if (Tree.ScalingPolicy == ScalingPolicyKind.TimeMetric)
                return BucketDuration;
            else
                return CalculateAverageScale(Tree.Root.InclusiveMetricByTime);
        }

        public override void AddSample(Histogram histogram, StackSourceSample sample)
        {
            double bucketDuration = BucketDuration;
            double startSampleInBucket = sample.TimeRelMSec;
            int bucketIndex = (int)((sample.TimeRelMSec - Start) / bucketDuration);
            Debug.Assert(0 <= bucketIndex && bucketIndex <= BucketCount);

            if (Tree.ScalingPolicy == ScalingPolicyKind.TimeMetric)
            {
                // place the metric in each of the buckets it overlaps with. 
                var nextBucketStart = GetStartTimeForBucket((HistogramCharacterIndex)(bucketIndex + 1));
                double endSample = sample.TimeRelMSec + sample.Metric;
                for (; ; )
                {
                    if (BucketCount <= bucketIndex)
                        break;

                    var metricInBucket = Math.Min(nextBucketStart, endSample) - startSampleInBucket;
                    Debug.Assert(metricInBucket >= 0);
                    histogram.AddMetric((float)metricInBucket, bucketIndex);

                    bucketIndex++;
                    startSampleInBucket = nextBucketStart;
                    nextBucketStart += bucketDuration;
                    if (startSampleInBucket > endSample)
                        break;
                }
            }
            else
            {
                // Put the sample in the right bucket.  Note that because we allow inclusive times on the end
                // point we could get bucketIndex == Length, so put that sample in the last bucket.  
                if (bucketIndex >= BucketCount)
                    bucketIndex = BucketCount - 1;
                histogram.AddMetric(sample.Metric, bucketIndex);
            }
        }

        public override string GetInfoForCharacterRange(HistogramCharacterIndex start, HistogramCharacterIndex end, Histogram histogram)
        {
            var rangeStart = GetStartTimeForBucket(start);
            var rangeEnd = GetStartTimeForBucket(end);

            var cumulativeStats = "";
            if (start != HistogramCharacterIndex.Invalid && end != HistogramCharacterIndex.Invalid && start < end)
            {
                float cumulativeStart = 0;
                for (int i = 0; i < (int)start; i++)
                    cumulativeStart += histogram[i];

                float cumulative = cumulativeStart;
                float cumulativeMax = cumulativeStart;
                HistogramCharacterIndex cumulativeMaxIdx = start;

                for (HistogramCharacterIndex i = start; i < end; i++)
                {
                    var val = histogram[(int)i];
                    cumulative += val;
                    if (cumulative > cumulativeMax)
                    {
                        cumulativeMax = cumulative;
                        cumulativeMaxIdx = i + 1;
                    }
                }
                cumulativeStats = string.Format(" CumStart:{0,9:n3}M Cum:{1,9:n3}M  CumMax:{2,9:n3}M at {3,11:n3}ms",
                    cumulativeStart / 1000000, cumulative / 1000000, cumulativeMax / 1000000, GetStartTimeForBucket(cumulativeMaxIdx));
            }

            return string.Format("TimeRange = {0,11:n3} - {1,11:n3} Duration {2,9:n3}ms{3}", rangeStart, rangeEnd, rangeEnd - rangeStart, cumulativeStats);
        }

        public override string GetDisplayString(Histogram histogram)
        {
            return HistogramString(histogram, histogram.Count, Scale);
        }
    }

    /// <summary>
    /// A Histogram is logically an array of floating point values.  Often they
    /// represent frequency, but it can be some other metric.  The X axis can 
    /// represent different things (time, scenario).  It is the HisogramContoller
    /// which understands what the X axis is.   Histograms know their HistogramController
    /// but not the reverse.  
    /// 
    /// Often Histograms are sparse (most array elements are zero), so the represnetation
    /// is designed to optimzed for this case (an array of non-zero index, value pairs). 
    /// </summary>
    public class Histogram : IEnumerable<float>
    {
        public Histogram(HistogramController controller)
        {
            m_controller = controller;
            m_singleBucketNum = -1;
        }

        /// <summary>
        /// Add a sample to this histogram.
        /// </summary>
        /// <param name="sample">The sample to add.</param>
        public void AddSample(StackSourceSample sample)
        {
            m_controller.AddSample(this, sample);
        }

        /// <summary>
        /// Add an amount to a bucket in this histogram.
        /// </summary>
        /// <param name="metric">The amount to add to the bucket.</param>
        /// <param name="bucket">The bucket to add to.</param>
        public void AddMetric(float metric, int bucket)
        {
            Debug.Assert(0 <= bucket && bucket < Count);

            if (m_singleBucketNum < 0)
                m_singleBucketNum = bucket;
            if (m_singleBucketNum == bucket)
            {
                m_singleBucketValue += metric;
                return;
            }
            if (m_buckets == null)
            {
                m_buckets = new float[Count];
                m_buckets[m_singleBucketNum] = m_singleBucketValue;
            }
            m_buckets[bucket] += metric;
        }

        /// <summary>
        /// Computes this = this + histogram * weight in place (this is updated).  
        /// </summary>
        public void AddScaled(Histogram histogram, double weight = 1)
        {
            var histArray = histogram.m_buckets;
            if (histArray != null)
            {
                for (int i = 0; i < histArray.Length; i++)
                {
                    var val = histArray[i];
                    if (val != 0)
                        AddMetric(val, i);
                }
            }
            else if (0 <= histogram.m_singleBucketNum)
                this.AddMetric((float)(histogram.m_singleBucketValue * weight), histogram.m_singleBucketNum);
        }

        /// <summary>
        /// The number of buckets in this histogram.
        /// </summary>
        public int Count
        {
            get { return m_controller.BucketCount; }
        }

        /// <summary>
        /// The <see cref="HistogramController"/> that controls this histogram.
        /// </summary>
        public HistogramController Controller
        {
            get { return m_controller; }
        }

        /// <summary>
        /// Get the metric contained in a bucket.
        /// </summary>
        /// <param name="index">The bucket to retrieve.</param>
        /// <returns>The metric contained in that bucket.</returns>
        public float this[int index]
        {
            get
            {
                Debug.Assert(0 <= index && index < Count);
                if (m_buckets != null)
                    return m_buckets[index];
                else if (m_singleBucketNum == index)
                    return m_singleBucketValue;
                else
                    return 0;
            }
        }

        /// <summary>
        /// Make a copy of this histogram.
        /// </summary>
        /// <returns>An independent copy of this histogram.</returns>
        public Histogram Clone()
        {
            return new Histogram(this);
        }

        public IEnumerator<float> GetEnumerator()
        {
            return GetEnumerable().GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() { throw new NotImplementedException(); }

        public override string ToString()
        {
            return Controller.GetDisplayString(this);
        }

        #region private
        /// <summary>
        /// Create a histogram that is a copy of another histogram.
        /// </summary>
        /// <param name="other">The histogram to copy.</param>
        private Histogram(Histogram other)
        {
            m_controller = other.m_controller;

            m_singleBucketNum = other.m_singleBucketNum;
            m_singleBucketValue = other.m_singleBucketValue;
            if (other.m_buckets != null)
            {
                m_buckets = new float[other.m_buckets.Length];
                Array.Copy(other.m_buckets, m_buckets, other.m_buckets.Length);
            }
        }

        /// <summary>
        /// Get an IEnumerable that can be used to enumerate the metrics stored in the buckets of this Histogram.
        /// </summary>
        private IEnumerable<float> GetEnumerable()
        {
            int end = Count;
            for (int i = 0; i < end; i++)
                yield return this[i];
        }

        /// <summary>
        /// The controller for this histogram.
        /// </summary>
        private readonly HistogramController m_controller;

        public float[] m_buckets;               // If null means is it s single value or no values

        // We special case a histogram with a single bucket.  
        private int m_singleBucketNum;          // -1 means no values
        private float m_singleBucketValue;
        #endregion
    }

    /// <summary>
    /// SampleInfos of a set of stackSource by eventToStack.  This represents the entire call tree.   You create an empty one in using
    /// the default constructor and use 'AddSample' to add stackSource to it.   You traverse it by 
    /// </summary>
    public class CallTree
    {
        /// <summary>
        /// Creates an empty call tree.  Only useful so you can have a valid 'placeholder' value when you 
        /// have no samples.  
        /// </summary>
        public CallTree(ScalingPolicyKind scalingPolicy)
        {
            m_root = new CallTreeNode("ROOT", StackSourceFrameIndex.Root, null, this);
            ScalingPolicy = scalingPolicy;
        }

        /// <summary>
        /// When converting the InclusiveMetricByTime to a InclusiveMetricByTimeString you have to decide 
        /// how to scale the samples to the digits displayed in the string.  This enum indicates this policy
        /// </summary>
        public ScalingPolicyKind ScalingPolicy { get; private set; }

        public TimeHistogramController TimeHistogramController
        {
            get { return m_timeHistogram; }
            set
            {
                Debug.Assert(Root == null || Root.HasChildren == false);
                m_timeHistogram = value;
                if (value != null)
                {
                    Root.m_inclusiveMetricByTime = new Histogram(value);
                }
            }
        }

        public ScenarioHistogramController ScenarioHistogram
        {
            get { return m_scenarioHistogram; }
            set
            {
                Debug.Assert(Root == null || Root.HasChildren == false);
                m_scenarioHistogram = value;
                if (value != null)
                {
                    Root.m_inclusiveMetricByScenario = new Histogram(value);
                }
            }
        }

        // TODO FIX NOW remove after we are happy with things. 
        public static bool DisableParallelism;

        public StackSource StackSource
        {
            get { return m_SampleInfo; }
            set
            {
                if (m_SampleInfo != null)
                    m_root = new CallTreeNode("ROOT", StackSourceFrameIndex.Root, null, this);
                m_SampleInfo = value;
                m_sumByID = null;
                if (TimeHistogramController != null)
                    TimeHistogramController.InvalidateScale();

                m_frames = new FrameInfo[100];  // A temporary stack used during AddSample, This is just a guess as to a good size.  
                m_TreeForStack = new TreeCacheEntry[StackInfoCacheSize];
                m_frameIntern = new ConcurrentDictionary<string, StackSourceFrameIndex>(1, value.CallFrameIndexLimit);
                m_canonicalID = new StackSourceFrameIndex[value.CallFrameIndexLimit];

                // If it is a graph source, keep track of the mapping (so GetRefs works)
                if (m_SampleInfo.IsGraphSource)
                    m_samplesToTreeNodes = new CallTreeNode[m_SampleInfo.SampleIndexLimit];

                if (DisableParallelism)
                    value.ProduceSamples(AddSample);
                else 
                    value.ProduceParallelSamples(AddSample);
                // And the basis for forming the % is total metric of stackSource.  
                PercentageBasis = Math.Abs(Root.InclusiveMetric);       // People get confused if this swaps. 

                // By default sort by inclusive Metric
                SortInclusiveMetricDecending();
                m_frames = null;                // Frames not needed anymore.  
                m_TreeForStack = null;
                m_frameIntern = null;
                m_canonicalID = null;
            }
        }

        /// <summary>
        /// If there are any nodes that have strictly less than to 'minInclusiveMetric'
        /// then remove the node, placing its samples into its parent (thus the parent's
        /// exclusive metric goes up).  
        /// 
        /// If useWholeTraceMetric is true, nodes are only folded if their inclusive metric
        /// OVER THE WHOLE TRACE is less than 'minInclusiveMetric'.  If false, then a node
        /// is folded if THAT NODE has less than the 'minInclusiveMetric'  
        /// 
        /// Thus if 'useWholeTraceMetric' == false then after calling this routine no
        /// node will have less than minInclusiveMetric.  
        /// 
        /// </summary>
        public int FoldNodesUnder(float minInclusiveMetric, bool useWholeTraceMetric)
        {
            m_root.CheckClassInvarients();

            // If we filter by whole trace metric we need to cacluate the byID sums.  
            Dictionary<int, CallTreeNodeBase> sumByID = null;
            if (useWholeTraceMetric)
                sumByID = GetSumByID();

            int ret = m_root.FoldNodesUnder(minInclusiveMetric, sumByID);

            m_root.CheckClassInvarients();
            m_sumByID = null;   // Force a recalculation of the list by ID
            return ret;
        }

        /// <summary>
        /// When calculating percentages, what do we use as 100%.  By default we use the
        /// Inclusive time for the root, but that can be changed here.  
        /// </summary>
        public float PercentageBasis { get; set; }
        public CallTreeNode Root { get { return m_root; } }

        /// <summary>
        /// Cause each treeNode in the calltree to be sorted (accending) based on comparer
        /// </summary>
        public void Sort(Comparison<CallTreeNode> comparer)
        {
            m_root.SortAll(comparer);
        }

        /// <summary>
        /// Sorting by InclusiveMetric Decending is so common, provide a shortcut.  
        /// </summary>
        public void SortInclusiveMetricDecending()
        {
            Sort(delegate(CallTreeNode x, CallTreeNode y)
            {
                int ret = Math.Abs(y.InclusiveMetric).CompareTo(Math.Abs(x.InclusiveMetric));
                if (ret != 0)
                    return ret;
                // Sort by first sample time (assending) if the counts are the same.  
                return x.FirstTimeRelMSec.CompareTo(y.FirstTimeRelMSec);
            });
        }
        public void ToXml(TextWriter writer)
        {
            writer.WriteLine("<CallTree TotalMetric=\"{0:f1}\">", Root.InclusiveMetric);
            Root.ToXml(writer, "");
            writer.WriteLine("</CallTree>");
        }
        public override string ToString()
        {
            StringWriter sw = new StringWriter();
            ToXml(sw);
            return sw.ToString();
        }

        // Get a callerSum-calleeSum treeNode for 'nodeName'
        public CallerCalleeNode CallerCallee(string nodeName)
        {
            return new CallerCalleeNode(nodeName, this);
        }
        /// <summary>
        /// Return a list of nodes that have statisicts rolled up by treeNode by ID.  It is not
        /// sorted by anything in particular.   Note that ID is not quite the same thing as the 
        /// name.  You can have two nodes that have different IDs but the same Name.  These 
        /// will show up as two distinct entries in the resulting list.  
        /// </summary>
        public IEnumerable<CallTreeNodeBase> ByID { get { return GetSumByID().Values; } }
        public List<CallTreeNodeBase> ByIDSortedExclusiveMetric()
        {
            var ret = new List<CallTreeNodeBase>(ByID);
            ret.Sort((x, y) => Math.Abs(y.ExclusiveMetric).CompareTo(Math.Abs(x.ExclusiveMetric)));
            return ret;
        }
        public virtual void Dispose()
        {
            if (m_sumByID != null)
            {
                foreach (var node in m_sumByID.Values)
                    node.Dispose();
                m_sumByID = null;
            }
            m_root.Dispose();
            m_root = null;
            m_SampleInfo = null;
        }
        #region private
        private CallTree(CallTreeNode root) { m_root = root; }

        private struct FrameInfo
        {
            public StackSourceFrameIndex frameIndex;
            public int numFolds;
        }

        // This keeps track of stacks that I have used in the past
        const int StackInfoCacheSize = 128;          // Must be a power of 2
        TreeCacheEntry[] m_TreeForStack;

        // Maps frame IDs to their canonical one (we group all frame IDs)
        internal StackSourceFrameIndex[] m_canonicalID;        // Maps frame IDs to their canonical one
        internal ConcurrentDictionary<string, StackSourceFrameIndex> m_frameIntern;        // Maps strings to their canonical frame ID

        struct TreeCacheEntry
        {
            public volatile StackSourceCallStackIndex StackIndex;
            public CallTreeNode Tree;
        }

        private CallTreeNode FindTreeNode(StackSourceCallStackIndex stack)
        {
            // Is it in our cache?
            int hash = (((int)stack) & (StackInfoCacheSize - 1));
            var entry = m_TreeForStack[hash];
            if (entry.StackIndex == stack && entry.Tree != null)
                return entry.Tree;

            if (stack == StackSourceCallStackIndex.Invalid)
                return m_root;

            var callerIndex = m_SampleInfo.GetCallerIndex(stack);
            var callerNode = FindTreeNode(callerIndex);

            var frameIndex = m_SampleInfo.GetFrameIndex(stack);
            var retNode = callerNode.FindCallee(frameIndex);

            // Update the cache.
            m_TreeForStack[hash].Tree = null;              // Clear the entry to avoid race conditions if run on multiple threads. (ELFIX)
            m_TreeForStack[hash].StackIndex = stack;
            m_TreeForStack[hash].Tree = retNode;

            return retNode;
        }

        private void AddSample(StackSourceSample sample)
        {
            var callTreeNode = FindTreeNode(sample.StackIndex);
            if (m_samplesToTreeNodes != null)
                m_samplesToTreeNodes[(int)sample.SampleIndex] = callTreeNode;

            // TODO se can be more conurrent tha this.    
            lock (this)
                AddSampleToTreeNode(callTreeNode, sample);
        }

        private void AddSampleToTreeNode(CallTreeNode treeNode, StackSourceSample sample)
        {
            // Add the sample to treeNode.
            Debug.Assert(sample.Count != 0);
            treeNode.m_exclusiveCount += sample.Count;
            treeNode.m_exclusiveMetric += sample.Metric;
            if (sample.SampleIndex != StackSourceSampleIndex.Invalid)
                treeNode.m_samples.Add(sample.SampleIndex);

            var stackIndex = sample.StackIndex;
            if (stackIndex != StackSourceCallStackIndex.Invalid)
            {
                // Increment the folded count
                var numFoldedNodes = m_SampleInfo.GetNumberOfFoldedFrames(sample.StackIndex);
                if (numFoldedNodes > 0)
                {
                    treeNode.m_exclusiveFoldedCount += sample.Count;
                    treeNode.m_exclusiveFoldedMetric += sample.Metric;
                }
            }

            // And update all the inclusive times up the tree to the root (including this node)
            while (treeNode != null)
            {
                treeNode.m_inclusiveCount += sample.Count;
                treeNode.m_inclusiveMetric += sample.Metric;

                if (treeNode.InclusiveMetricByTime != null)
                    treeNode.InclusiveMetricByTime.AddSample(sample);

                if (treeNode.InclusiveMetricByScenario != null)
                    treeNode.InclusiveMetricByScenario.AddSample(sample);

                if (sample.TimeRelMSec < treeNode.m_firstTimeRelMSec)
                    treeNode.m_firstTimeRelMSec = sample.TimeRelMSec;

                var sampleEndTime = sample.TimeRelMSec;
                if (ScalingPolicy == ScalingPolicyKind.TimeMetric)
                {
                    // The sample ends at the end of its metric, however we trucate at the end of the range.  
                    sampleEndTime += sample.Metric;
                    if (TimeHistogramController != null && sampleEndTime > TimeHistogramController.End)
                        sampleEndTime = TimeHistogramController.End;
                }

                if (sampleEndTime > treeNode.m_lastTimeRelMSec)
                    treeNode.m_lastTimeRelMSec = sampleEndTime;
                Debug.Assert(treeNode.m_firstTimeRelMSec <= treeNode.m_lastTimeRelMSec);

                if (stackIndex != StackSourceCallStackIndex.Invalid)
                    stackIndex = m_SampleInfo.GetCallerIndex(stackIndex);
                else
                    Debug.Assert(treeNode == m_root);

                treeNode = treeNode.Caller;
            }
        }

        internal Dictionary<int, CallTreeNodeBase> GetSumByID()
        {
            if (m_sumByID == null)
            {
                m_sumByID = new Dictionary<int, CallTreeNodeBase>();
                var callersOnStack = new Dictionary<int, CallTreeNodeBase>();       // This is just a set
                AccumulateSumByID(m_root, callersOnStack);
            }
            return m_sumByID;
        }
        /// <summary>
        /// Traverse the subtree of 'treeNode' into the m_sumByID dictionary.   We don't want to
        /// double-count inclusive times, so we have to keep track of all callers currently on the
        /// stack and we only add inclusive times for nodes that are not already on the stack.  
        /// </summary>
        private void AccumulateSumByID(CallTreeNode treeNode, Dictionary<int, CallTreeNodeBase> callersOnStack)
        {
            CallTreeNodeBase byIDNode;
            if (!m_sumByID.TryGetValue((int)treeNode.m_id, out byIDNode))
            {
                byIDNode = new CallTreeNodeBase(treeNode.Name, treeNode.m_id, this);
                byIDNode.m_isByIdNode = true;
                m_sumByID.Add((int)treeNode.m_id, byIDNode);
            }

            bool newOnStack = !callersOnStack.ContainsKey((int)treeNode.m_id);
            // Add in the tree treeNode's contribution
            byIDNode.CombineByIdSamples(treeNode, newOnStack);

            // TODO FIX NOW
            // Debug.Assert(treeNode.m_nextSameId == null);
            treeNode.m_nextSameId = byIDNode.m_nextSameId;
            byIDNode.m_nextSameId = treeNode;
            if (treeNode.Callees != null)
            {
                if (newOnStack)
                    callersOnStack.Add((int)treeNode.m_id, null);
                foreach (var child in treeNode.m_callees)
                    AccumulateSumByID(child, callersOnStack);
                if (newOnStack)
                    callersOnStack.Remove((int)treeNode.m_id);
            }
        }

        internal StackSource m_SampleInfo;
        private CallTreeNode m_root;
        private TimeHistogramController m_timeHistogram;
        private ScenarioHistogramController m_scenarioHistogram;
        Dictionary<int, CallTreeNodeBase> m_sumByID;          // These nodes hold the rollup by Frame ID (name)
        private FrameInfo[] m_frames;                         // Used to invert the stack only used during 'AddSample' phase.  
        internal CallTreeNode[] m_samplesToTreeNodes;             // Used for the graph support. 
        #endregion
    }

    public enum ScalingPolicyKind
    {
        /// <summary>
        /// This is the default.  In this policy, 100% is chosen so that the histogram is scaled as best it can.   
        /// </summary>
        ScaleToData,
        /// <summary>
        /// It assumes that the metric represents time 
        /// </summary>
        TimeMetric
    }

    /// <summary>
    /// The part of a CalltreeNode that is common to Caller-calleeSum and the Calltree view.  
    /// </summary>
    public class CallTreeNodeBase
    {
        public CallTreeNodeBase(CallTreeNodeBase template)
        {
            m_id = template.m_id;
            m_name = template.m_name;
            m_callTree = template.m_callTree;
            m_inclusiveMetric = template.m_inclusiveMetric;
            m_inclusiveCount = template.m_inclusiveCount;
            m_exclusiveMetric = template.m_exclusiveMetric;
            m_exclusiveCount = template.m_exclusiveCount;
            m_exclusiveFoldedMetric = template.m_exclusiveFoldedMetric;
            m_exclusiveFoldedCount = template.m_exclusiveFoldedCount;
            m_firstTimeRelMSec = template.m_firstTimeRelMSec;
            m_lastTimeRelMSec = template.m_lastTimeRelMSec;
            // m_samples left out intentionally
            // m_nextSameId
            // m_isByIdNode
            if (template.m_inclusiveMetricByTime != null)
                m_inclusiveMetricByTime = template.m_inclusiveMetricByTime.Clone();
            if (template.m_inclusiveMetricByScenario != null)
                m_inclusiveMetricByScenario = template.m_inclusiveMetricByScenario.Clone();
        }

        public string Name { get { return m_name; } }
        /// <summary>
        /// Currently the same as Name, but could contain additional info.  
        /// Suitable for display but not for programatic comparision.  
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (m_isGraphNode)
                    return Name + " {MinDepth " + m_minDepth + "}";
                return Name;
            }
        }
        /// <summary>
        /// The ID represents a most fine grained uniqueness associated with this node.   Typically it represents
        /// a particular method (however it is possible that two methods can have the same name (because the scope
        /// was not captured).   Thus there can be multiple nodes with the same Name but different IDs.   
        /// 
        /// This can be StackSourceFrameIndex.Invalid for Caller-callee nodes (which have names, but no useful ID) 
        ///
        /// If ID != Invalid, and the IDs are the same then the names are guarenteed to be the same.  
        /// </summary>
        public StackSourceFrameIndex ID { get { return m_id; } }
        public float InclusiveMetric { get { return m_inclusiveMetric; } }
        public float ExclusiveMetric { get { return m_exclusiveMetric; } }
        public float ExclusiveFoldedMetric { get { return m_exclusiveFoldedMetric; } }

        public float InclusiveCount { get { return m_inclusiveCount; } }
        public float ExclusiveCount { get { return m_exclusiveCount; } }
        public float ExclusiveFoldedCount { get { return m_exclusiveFoldedCount; } }

        public float InclusiveMetricPercent { get { return m_inclusiveMetric * 100 / m_callTree.PercentageBasis; } }
        public float ExclusiveMetricPercent { get { return m_exclusiveMetric * 100 / m_callTree.PercentageBasis; } }
        public float ExclusiveFoldedMetricPercent { get { return m_exclusiveFoldedMetric * 100 / m_callTree.PercentageBasis; } }

        public double FirstTimeRelMSec { get { return m_firstTimeRelMSec; } }
        public double LastTimeRelMSec { get { return m_lastTimeRelMSec; } }
        public double DurationMSec { get { return m_lastTimeRelMSec - m_firstTimeRelMSec; } }
        /// <summary>
        /// The call tree that contains this node.  
        /// </summary>
        public CallTree CallTree { get { return m_callTree; } }


        public Histogram InclusiveMetricByTime { get { return m_inclusiveMetricByTime; } }
        public string InclusiveMetricByTimeString
        {
            get
            {
                if (m_inclusiveMetricByTime != null)
                    return m_inclusiveMetricByTime.ToString();
                else
                    return null;
            }
            set { }
        }
        public Histogram InclusiveMetricByScenario { get { return m_inclusiveMetricByScenario; } }
        public string InclusiveMetricByScenarioString
        {
            get
            {
                if (m_inclusiveMetricByScenario != null)
                    return m_inclusiveMetricByScenario.ToString();
                else
                    return null;
            }
            set { }
        }

        /// <summary>
        /// Return all the original stack samples in this node.  If exclusive==true then just he
        /// sample exclusively in this node are returned, otherwise it is the inclusive samples.   
        /// 
        /// If the original stack source that was used to create this CodeTreeNode was a FilterStackSource
        /// then that filtering is removed in the returned Samples.  
        /// 
        /// returns the total number of samples (the number of times 'callback' is called)
        /// 
        /// If the callback returns false, the iteration over samples stops. 
        /// </summary>
        public virtual int GetSamples(bool exclusive, Func<StackSourceSampleIndex, bool> callback)
        {
            // Graph nodes don't care about trees, they just return the samples 'directly'.   They don't have a notion of 'inclusive'  
            if (m_isGraphNode)
                return GetSamplesForTreeNode((CallTreeNode)this, true, callback, StackSourceFrameIndex.Invalid);

            int count = 0;
            var excludeChildrenID = GetExcludeChildID();

            GetTrees(delegate(CallTreeNode node)
            {
                count += GetSamplesForTreeNode(node, exclusive, callback, excludeChildrenID);
            });
#if DEBUG
            if (exclusive)
            {
                if (count != ExclusiveCount)
                {
                    // Exclusive counts for caller nodes are always 0
                    var agg = this as AggregateCallTreeNode;
                    Debug.Assert(agg != null && !agg.IsCalleeTree && ExclusiveCount == 0);
                }
            }
            else
                Debug.Assert(count == InclusiveCount);
#endif
            return count;
        }
        protected virtual StackSourceFrameIndex GetExcludeChildID()
        {
            var excludeChildrenID = StackSourceFrameIndex.Invalid;
            if (m_isByIdNode)
                excludeChildrenID = this.ID;
            return excludeChildrenID;
        }

        /// <summary>
        /// While 'GetSamples' can return all the samples in the tree, this is a relatively
        /// inefficient way of representing the samples.   Instead you can return a list of
        /// trees whose samples represent all the samples.   This is what GetTrees does.
        /// It calls 'callback' on a set of trees that taken as a whole have all the samples
        /// in 'node'.  
        /// 
        /// Note you ave to be careful when using this for inclusive summation of byname nodes because 
        /// you will get trees that 'overlap' (bname nodes might refer into the 'middle' of another
        /// call tree).   This can be avoided pretty easily by simply stopping inclusive traversal 
        /// whenever a tree node with that ID occurs (see GetSamples for an example). 
        /// </summary>
        public virtual void GetTrees(Action<CallTreeNode> callback)
        {
            // if we are a treeNode 
            var asTreeNode = this as CallTreeNode;
            if (asTreeNode != null)
            {
                callback(asTreeNode);
                return;
            };
            if (!m_isByIdNode)
            {
                Debug.Assert(false, "Error: unexpected CallTreeNodeBase");
                return;
            }
            for (var curNode = m_nextSameId; curNode != null; curNode = curNode.m_nextSameId)
            {
                Debug.Assert(curNode is CallTreeNode);
                callback(curNode as CallTreeNode);
            }
        }

        public void ToXmlAttribs(TextWriter writer)
        {
            writer.Write(" Name=\"{0}\"", XmlUtilities.XmlEscape(Name ?? "", false));
            writer.Write(" ID=\"{0}\"", (int)m_id);
            writer.Write(" InclusiveMetric=\"{0}\"", InclusiveMetric);
            writer.Write(" ExclusiveMetric=\"{0}\"", ExclusiveMetric);
            writer.Write(" InclusiveCount=\"{0}\"", InclusiveCount);
            writer.Write(" ExclusiveCount=\"{0}\"", ExclusiveCount);
            writer.Write(" FirstTimeRelMSec=\"{0:f4}\"", FirstTimeRelMSec);
            writer.Write(" LastTimeRelMSec=\"{0:f4}\"", LastTimeRelMSec);
            // Don't show the samples. 
            //if (m_samples.Count != 0)
            //{
            //    StringBuilder sb = new StringBuilder();
            //    sb.Append(" Samples = \"");
            //    for (int i = 0; i < m_samples.Count; i++)
            //        sb.Append(' ').Append((int)m_samples[i]);
            //    sb.Append("\"");
            //    writer.Write(sb.ToString());
            //}
        }
        public override string ToString()
        {
            StringWriter sw = new StringWriter();
            sw.Write("<Node");
            ToXmlAttribs(sw);
            sw.Write("/>");
            return sw.ToString();
        }
        // Frees the resources associated with it agressively (Needed because GUI's hold on to object too long) 
        public virtual void Dispose()
        {
            m_samples.Clear();
            m_nextSameId = null;
            m_name = null;
            m_callTree = null;
            m_inclusiveMetricByTime = null;
            m_inclusiveMetricByScenario = null;
        }

        #region private
        internal CallTreeNodeBase(string name, StackSourceFrameIndex id, CallTree container)
        {
            // We use {} to express things that are not logically part of the name, so strip any 'real' {}
            // because it confuses the upper level logic TODO: this is kind of a hack.
            var idx = name.IndexOf('{');
            if (0 < idx)
                name = name.Substring(0, idx);
            this.m_name = name;
            this.m_callTree = container;
            this.m_id = id;
            this.m_firstTimeRelMSec = Double.PositiveInfinity;
            this.m_lastTimeRelMSec = Double.NegativeInfinity;
            if (container.TimeHistogramController != null)
                this.m_inclusiveMetricByTime = new Histogram(container.TimeHistogramController);
            if (container.ScenarioHistogram != null)
                this.m_inclusiveMetricByScenario = new Histogram(container.ScenarioHistogram);
        }

        /// <summary>
        /// Combines the 'this' node with 'otherNode'.   If 'newOnStack' is true, then the inclusive
        /// metrics are also updated.  
        /// 
        /// Note that I DON'T accumlate other.m_samples into this.m_samples.   This is because we want to share
        /// samples as much a possible.  Thus nodes remeber their samples by pointing at other call trees
        /// and you fetch the samples by an inclusive walk of the tree.  
        /// </summary>
        internal void CombineByIdSamples(CallTreeNodeBase other, bool addInclusive, double weight = 1.0, bool addExclusive = true)
        {
            if (addInclusive)
            {
                m_inclusiveMetric += (float)(other.m_inclusiveMetric * weight);
                m_inclusiveCount += (float)(other.m_inclusiveCount * weight);
                if (m_inclusiveMetricByTime != null && other.m_inclusiveMetricByTime != null)
                    m_inclusiveMetricByTime.AddScaled(other.m_inclusiveMetricByTime, weight);
                if (m_inclusiveMetricByScenario != null && other.m_inclusiveMetricByScenario != null)
                    m_inclusiveMetricByScenario.AddScaled(other.m_inclusiveMetricByScenario, weight);
            }

            if (addExclusive)
            {
                m_exclusiveMetric += (float)(other.m_exclusiveMetric * weight);
                m_exclusiveCount += (float)(other.m_exclusiveCount * weight);
                m_exclusiveFoldedMetric += (float)(other.m_exclusiveFoldedMetric * weight);
                m_exclusiveFoldedCount += (float)(other.m_exclusiveFoldedCount * weight);
            }

            if (other.m_firstTimeRelMSec < m_firstTimeRelMSec)
                m_firstTimeRelMSec = other.m_firstTimeRelMSec;
            if (other.m_lastTimeRelMSec > m_lastTimeRelMSec)
                m_lastTimeRelMSec = other.m_lastTimeRelMSec;

            Debug.Assert(m_firstTimeRelMSec <= m_lastTimeRelMSec || double.IsInfinity(m_firstTimeRelMSec));
        }

        /// <summary>
        /// To avoid double-counting for byname nodes, with we can be told to exclude any children with a particular ID 
        /// (the ID of the ByName node itself) if are doing the inclusive case.   The goal is to count every reachable
        /// tree exactly once.  We do this by conceptually 'marking' each node with ID at the top level (when they are 
        /// enumerated as children of the Byname node), and thus any node with that excludeChildrenWithID is conceptually
        /// marked if you encounter it as a child in the tree itself (so you should exclude it).  The result is that 
        /// every node is visited exactly once (without the expense of having a 'visited' bit).  
        /// </summary>
        protected static int GetSamplesForTreeNode(CallTreeNode curNode, bool exclusive, Func<StackSourceSampleIndex, bool> callback, StackSourceFrameIndex excludeChildrenWithID)
        {
            // Include any nodes from myself. 
            int count = 0;
            for (int i = 0; i < curNode.m_samples.Count; i++)
            {
                count++;
                if (!callback(curNode.m_samples[i]))
                    return count;
            }

            if (!exclusive)
            {
                if (curNode.Callees != null)
                {
                    foreach (var callee in curNode.Callees)
                    {
                        Debug.Assert(callee.ID != StackSourceFrameIndex.Invalid);
                        // 
                        if (callee.ID != excludeChildrenWithID)
                            count += GetSamplesForTreeNode(callee, exclusive, callback, excludeChildrenWithID);
                    }
                }
            }
#if DEBUG
            // The number of samples does not equal ithe InclusiveCount on intermediate nodes if we have 
            // recursion because we are exclusing some of the samples to avoid double counting
            if (exclusive)
                Debug.Assert(count == curNode.ExclusiveCount);
            else
                Debug.Assert(count == curNode.InclusiveCount || excludeChildrenWithID != StackSourceFrameIndex.Invalid);
#endif
            return count;
        }


        internal StackSourceFrameIndex m_id;
        internal string m_name;
        internal CallTree m_callTree;
        internal float m_inclusiveMetric;
        internal float m_inclusiveCount;
        internal float m_exclusiveMetric;
        internal float m_exclusiveCount;
        internal float m_exclusiveFoldedMetric;
        internal float m_exclusiveFoldedCount;
        internal double m_firstTimeRelMSec;
        internal double m_lastTimeRelMSec;

        internal GrowableArray<StackSourceSampleIndex> m_samples;       // The actual samples.  
        internal Histogram m_inclusiveMetricByTime;                     // histogram by time. Can be null if no histogram is needed.
        internal Histogram m_inclusiveMetricByScenario;                 // Histogram by scenario. Can be null if we're only dealing with one scenario.
        internal CallTreeNodeBase m_nextSameId;                         // We keep a linked list of tree nodes with the same ID (name)
        internal bool m_isByIdNode;                                     // Is this a node representing a rollup by ID (name)?  

        // TODO FIX NOW should this be a seaparate sub-type
        internal bool m_isGraphNode;                                    // Children represent memory graph references
        internal bool m_isCallerTree;
        internal int m_minDepth;                                        // Only used by Graph nodes, it is the minimum of the depth of all samples
        #endregion
    }

    /// <summary>
    /// Represents a single treeNode in a code:CallTree 
    /// 
    /// Each node keeps all the sample with the same path to the root.  
    /// Each node also remembers its parent (caller) and children (callees).
    /// The nodes also keeps the IDs of all its samples (so no information
    /// is lost, just sorted by stack).   You get at this through the
    /// code:CallTreeNodeBase.GetSamples method.  
    /// </summary>
    public class CallTreeNode : CallTreeNodeBase
    {
        public CallTreeNode Caller { get { return m_caller; } }
        public IList<CallTreeNode> Callees
        {
            get
            {
                if (m_callees == null)
                {
                    m_callees = GetCallees();
                }
                return m_callees;
            }
        }
        virtual public bool HasChildren
        {
            get
            {
                // We try to be very lazy since HasChildren is called just to determine a check box is available.  
                var callees = Callees;
                if (callees != null && callees.Count != 0)
                    return true;
                var stackSource = CallTree.StackSource;
                if (stackSource == null)
                    return false;
                if (!stackSource.IsGraphSource)
                    return false;
                callees = AllCallees;
                return (callees != null && callees.Count != 0);
            }
        }

        /// <summary>
        /// DisplayCallees is a set of Callee that you actually display
        /// It always starts with the 'normal' Callee, however in addition if we are
        /// displaying a MemoryGraph, it will also show 'pruned' arcs (by calling GetRefs)
        /// so that you can see the whole graph if you like.  
        /// </summary>
        public IList<CallTreeNode> AllCallees
        {
            get
            {
                if (m_displayCallees == null)
                {
                    m_displayCallees = Callees;
                    if (CallTree.StackSource.IsGraphSource)
                        m_displayCallees = GetAllChildren();
                }
                return m_displayCallees;
            }
        }
        public bool IsLeaf { get { return Callees == null; } }
        public bool IsGraphNode { get { return m_isGraphNode; } }
        public void SortAll(Comparison<CallTreeNode> comparer)
        {
            if (Callees != null)
            {
                m_callees.Sort(comparer);
                for (int i = 0; i < m_callees.Count; i++)
                    m_callees[i].SortAll(comparer);
                m_displayCallees = null;    // Recompute
            }

        }
        public void ToXml(TextWriter writer, string indent = "")
        {

            writer.Write("{0}<CallTree ", indent);
            this.ToXmlAttribs(writer);
            writer.WriteLine(">");

            var childIndent = indent + " ";
            if (Callees != null)
            {
                foreach (CallTreeNode callee in m_callees)
                {
                    callee.ToXml(writer, childIndent);
                }
            }
            writer.WriteLine("{0}</CallTree>", indent);
        }

        /// <summary>
        /// Adds up the counts of all nodes called 'BROKEN' nodes in a particular tree node
        /// 
        /// This is a utility function.  
        /// </summary>
        public float GetBrokenStackCount()
        {
            return GetBrokenStackCount(4);
        }

        /// <summary>
        /// Creates a string that has spaces | and + signs that represent the indentation level 
        /// for the tree node.  (Called from XAML)
        /// </summary>
        public string IndentString(bool displayPrimaryOnly)
        {
            if (m_indentString == null || m_indentStringForPrimary != displayPrimaryOnly)
            {
                var depth = Depth();
                var chars = new char[depth];
                var i = depth - 1;
                if (0 <= i)
                {
                    chars[i] = '+';
                    var ancestor = Caller;
                    --i;
                    while (i >= 0)
                    {
                        chars[i] = ancestor.IsLastChild(displayPrimaryOnly) ? ' ' : '|';
                        ancestor = ancestor.Caller;
                        --i;
                    }
                }

                m_indentString = new string(chars);
                m_indentStringForPrimary = displayPrimaryOnly;
            }
            return m_indentString;
        }
        public override string ToString()
        {
            StringWriter sw = new StringWriter();
            ToXml(sw, "");
            return sw.ToString();
        }
        public override void Dispose()
        {
            if (m_callees != null)
            {
                foreach (var node in m_callees)
                    node.Dispose();
                m_callees.Clear();
            }
            m_caller = null;
            base.Dispose();
        }
        #region private
        public CallTreeNode(string name, StackSourceFrameIndex id, CallTreeNode caller, CallTree container)
            : base(name, id, container)
        {
            this.m_caller = caller;
        }

        // Graph support.  
        private IList<CallTreeNode> GetAllChildren()
        {
            var source = CallTree.StackSource;
            CallTreeNode[] samplesToNodes = CallTree.m_samplesToTreeNodes;
            Debug.Assert(source.IsGraphSource);
            Debug.Assert(samplesToNodes != null);

            var childrenSet = new Dictionary<string, CallTreeNode>();
            // Exclude myself
            childrenSet[Name] = null;
            // Exclude the primary children
            if (Callees != null)
            {
                foreach (var callee in Callees)
                    childrenSet[callee.Name] = null;
            }

            // TODO FIX NOW.  This is a hack, we know every type of CallTreeNode.     
            var asAgg = this as AggregateCallTreeNode;
            var dir = IsCalleeTree ? RefDirection.From : RefDirection.To;
            var sampleSet = new Dictionary<StackSourceSampleIndex, int>();
            this.GetSamples(true, delegate(StackSourceSampleIndex sampleIndex)
            {
                sampleSet[sampleIndex] = 0;
                return true;
            });

            this.GetSamples(true, delegate(StackSourceSampleIndex sampleIndex)
            {
                // TODO FIX NOW too subtle!  This tracing back up the stack is tricky.  
                if (!IsCalleeTree && asAgg != null)
                {
                    // For Caller nodes, you need to move 'toward the root' a certain number of call frames.  
                    // especially because recursive nodes are folded in the tree by not in the graph.  
                    var sample = CallTree.StackSource.GetSampleByIndex(sampleIndex);
                    StackSourceCallStackIndex samplePath = sample.StackIndex;
                    Debug.Assert(asAgg != null);
                    for (int i = 0; i < asAgg.m_callerOffset; i++)
                        samplePath = CallTree.StackSource.GetCallerIndex(samplePath);

                    if (samplePath == StackSourceCallStackIndex.Invalid)
                        return true;
                    // This is where we break abstraction.   We know that the callStackIndex is in fact a sample index
                    // so we can simply cast.   TODO FIX NOW decide how to not break the absraction.  
                    sampleIndex = (StackSourceSampleIndex)samplePath;
                }
                source.GetRefs(sampleIndex, dir, delegate(StackSourceSampleIndex childIndex)
                {
                    // Ignore samples to myself.  
                    if (sampleSet.ContainsKey(childIndex))
                        return;

                    var childNode = samplesToNodes[(int)childIndex];
                    if (childNode != null)       // TODO FIX NOW: I would not think this check would be needed.  
                    {
                        CallTreeNode graphChild;
                        if (!childrenSet.TryGetValue(childNode.Name, out graphChild))
                        {
                            childrenSet[childNode.Name] = graphChild = new CallTreeNode(childNode.Name, childNode.ID, this, CallTree);
                            graphChild.IsCalleeTree = IsCalleeTree;
                            graphChild.m_isGraphNode = true;
                            graphChild.m_minDepth = int.MaxValue;
                        }

                        // Add the sample 
                        if (graphChild != null)
                        {
                            graphChild.m_minDepth = Math.Min(childNode.Depth(), graphChild.m_minDepth);
                            graphChild.m_samples.Add(childIndex);
                            // TODO FIX NOW, these are arc counts, they should be node counts.  (need interning).  
                            graphChild.m_exclusiveCount++;
                            graphChild.m_exclusiveMetric += source.GetSampleByIndex(childIndex).Metric;
                        }
                    }
                });
                return true;
            });

            // Sort by min depth then name.  
            var ret = new List<CallTreeNode>();
            foreach (var val in childrenSet.Values)
            {
                if (val != null)
                    ret.Add(val);
            }
            ret.Sort(delegate(CallTreeNode x, CallTreeNode y)
            {
                var cmp = x.m_minDepth - y.m_minDepth;
                if (cmp != 0)
                    return cmp;
                return x.Name.CompareTo(y.Name);
            });

            // Put the true callees first.  
            if (Callees != null)
                ret.InsertRange(0, Callees);

            return ret;
        }

        /// <summary>
        /// Some calltrees already fill in their children, others do so lazily, in which case they 
        /// override this method.  
        /// </summary>
        protected virtual List<CallTreeNode> GetCallees() { return null; }

        /// <summary>
        /// Fold away any nodes having less than 'minInclusiveMetric'.  If 'sumByID' is non-null then the 
        /// only nodes that have a less then the minInclusiveMetric for the whole trace are folded. 
        /// </summary>
        internal int FoldNodesUnder(float minInclusiveMetric, Dictionary<int, CallTreeNodeBase> sumByID)
        {
            int nodesFolded = 0;
            if (Callees != null)
            {
                int to = 0;
                for (int from = 0; from < m_callees.Count; from++)
                {
                    var callee = m_callees[from];
                    // We don't fold away Broken stacks ever.  
                    if (Math.Abs(callee.InclusiveMetric) < minInclusiveMetric && callee.m_id != StackSourceFrameIndex.Broken &&
                    (sumByID == null || callee.IsFoldable(minInclusiveMetric, sumByID)))
                    {
                        // TODO the samples are no longer in time order, do we care?
                        nodesFolded++;
                        m_exclusiveCount += callee.m_inclusiveCount;
                        m_exclusiveMetric += callee.m_inclusiveMetric;
                        m_exclusiveFoldedMetric += callee.m_inclusiveMetric;
                        m_exclusiveFoldedCount += callee.m_inclusiveCount;

                        // Transfer the samples to the caller 
                        TransferInclusiveSamplesToList(callee, ref m_samples);
                    }
                    else
                    {
                        nodesFolded += callee.FoldNodesUnder(minInclusiveMetric, sumByID);
                        if (to != from)
                            m_callees[to] = m_callees[from];
                        to++;
                    }
                }

                if (to == 0)
                    m_callees = null;
                else if (to != m_callees.Count)
                    m_callees.RemoveRange(to, m_callees.Count - to);
                Debug.Assert((to == 0 && m_callees == null) || to == m_callees.Count);
            }

            Debug.Assert(Math.Abs(InclusiveMetric - ExclusiveMetric) >= -Math.Abs(InclusiveMetric) * .001);
            Debug.Assert(m_callees != null || Math.Abs(ExclusiveMetric - InclusiveMetric) <= .001 * Math.Abs(ExclusiveMetric));
            return nodesFolded;
        }

        // TODO FIX NOW: decide what to do here, we originally did a recursive IsFolable but that causes very little folding. 
        private bool IsFoldable(float minInclusiveMetric, Dictionary<int, CallTreeNodeBase> sumByID)
        {
            return Math.Abs(sumByID[(int)m_id].InclusiveMetric) < minInclusiveMetric;
        }

        // Transfer all samples (inclusively from 'fromNode' to 'toList'.  
        private static void TransferInclusiveSamplesToList(CallTreeNode fromNode, ref GrowableArray<StackSourceSampleIndex> toList)
        {
            // Transfer the exclusive samples.
            for (int i = 0; i < fromNode.m_samples.Count; i++)
                toList.Add(fromNode.m_samples[i]);

            // And now all the samples from children
            if (fromNode.Callees != null)
            {
                for (int i = 0; i < fromNode.m_callees.Count; i++)
                    TransferInclusiveSamplesToList(fromNode.m_callees[i], ref toList);
            }
        }

        internal CallTreeNode FindCallee(StackSourceFrameIndex frameID)
        {
            var canonicalFrameID = m_callTree.m_canonicalID[(int)frameID];
            string frameName = null;
            if (canonicalFrameID == 0)
            {
                frameName = m_callTree.m_SampleInfo.GetFrameName(frameID, false);
                canonicalFrameID = m_callTree.m_frameIntern.GetOrAdd(frameName, frameID);
                m_callTree.m_canonicalID[(int)frameID] = canonicalFrameID;
            }

            // TODO see if taking the lock in the read case is espensive or not.  
            CallTreeNode callee;
            lock (this)
            {
                if (m_callees != null)
                {
                    for (int i = m_callees.Count; 0 < i; )
                    {
                        --i;
                        callee = m_callees[i];
                        if (callee != null && callee.m_id == canonicalFrameID)
                            return callee;
                    }
                }

                // No luck, add a new node. 
                if (frameName == null)
                    frameName = m_callTree.m_SampleInfo.GetFrameName(canonicalFrameID, false);
                callee = new CallTreeNode(frameName, canonicalFrameID, this, m_callTree);

                if (m_callees == null)
                    m_callees = new List<CallTreeNode>();
                m_callees.Add(callee);
            }
            return callee;
        }

        private bool IsLastChild(bool displayPrimaryOnly)
        {
            var parentCallees = displayPrimaryOnly ? Caller.Callees : Caller.AllCallees;
            return (parentCallees[parentCallees.Count - 1] == this);
        }

        private int Depth()
        {
            int ret = 0;
            CallTreeNode ptr = Caller;
            while (ptr != null)
            {
                ret++;
                ptr = ptr.Caller;
            }
            return ret;
        }

        private float GetBrokenStackCount(int depth = 4)
        {
            if (depth <= 0)
                return 0;

            if (this.Name == "BROKEN")          // TODO use ID instead
                return this.InclusiveCount;

            float ret = 0;
            if (this.Callees != null)
                foreach (var child in this.Callees)
                    ret += child.GetBrokenStackCount(depth - 1);

            return ret;
        }

        [Conditional("DEBUG")]
        public void CheckClassInvarients()
        {
            float sum = m_exclusiveMetric;
            float count = m_exclusiveCount;
            if (m_callees != null)
            {
                for (int i = 0; i < Callees.Count; i++)
                {
                    var callee = m_callees[i];
                    callee.CheckClassInvarients();
                    sum += callee.m_inclusiveMetric;
                    count += callee.m_inclusiveCount;
                }
            }
            Debug.Assert(Math.Abs(sum - m_inclusiveMetric) <= Math.Abs(sum) * .001);
            Debug.Assert(count == m_inclusiveCount);
        }

        // state;
        private CallTreeNode m_caller;
        internal List<CallTreeNode> m_callees;
        IList<CallTreeNode> m_displayCallees;           // Might contain more 'nodes' that are not in the tree proper
        private string m_indentString;
        private bool m_indentStringForPrimary;

        protected internal virtual bool IsCalleeTree { get { return !m_isCallerTree; } set { m_isCallerTree = !value; } }

        // TODO FIX NOW use or remove internal int m_visitNum;        // used to insure that you visit a node only once.  
        #endregion
    }

    /// <summary>
    /// A code:CallerCalleeNode gives statistics that focus on a NAME.  (unlike calltrees that use ID)
    /// It takes all stackSource that have callStacks that include that treeNode and compute the metrics for
    /// all the callers and all the callees for that treeNode.  
    /// </summary>
    public class CallerCalleeNode : CallTreeNodeBase
    {
        /// <summary>
        /// Given a complete call tree, and a Name within that call tree to focus on, create a
        /// CallerCalleeNode that represents the single Caller-Callee view for that treeNode. 
        /// </summary>
        public CallerCalleeNode(string nodeName, CallTree callTree)
            : base(nodeName, StackSourceFrameIndex.Invalid, callTree)
        {
            m_callees = new List<CallTreeNodeBase>();
            m_callers = new List<CallTreeNodeBase>();

            CallTreeNodeBase weightedSummary;
            double weightedSummaryScale;
            bool isUniform;
            AccumlateSamplesForNode(callTree.Root, 0, out weightedSummary, out weightedSummaryScale, out isUniform);

            m_callers.Sort((x, y) => Math.Abs(y.InclusiveMetric).CompareTo(Math.Abs(x.InclusiveMetric)));
            m_callees.Sort((x, y) => Math.Abs(y.InclusiveMetric).CompareTo(Math.Abs(x.InclusiveMetric)));

#if DEBUG
            float callerSum = 0;
            foreach (var caller in m_callers)
                callerSum += caller.m_inclusiveMetric;

            float calleeSum = 0;
            foreach (var callee in m_callees)
                calleeSum += callee.m_inclusiveMetric;

            if (this.Name != m_callTree.Root.Name)
                Debug.Assert(Math.Abs(callerSum - m_inclusiveMetric) <= .001);
            Debug.Assert(Math.Abs(calleeSum + m_exclusiveMetric - m_inclusiveMetric) <= .001 * Math.Abs(m_inclusiveMetric));

            // We should get he same stats as the byID view
            CallTreeNodeBase byID = null;
            foreach (var sumNode in callTree.ByID)
            {
                if (sumNode.Name == this.Name)
                {
                    if (byID != null)
                    {
                        byID = null; // TODO right now we might get duplicates that have the same  name but different ID.  Give up.  
                        break;
                    }
                    byID = sumNode;
                }
            }
            if (byID != null)
            {
                Debug.Assert(Math.Abs(byID.InclusiveCount - InclusiveCount) < .001);
                Debug.Assert(Math.Abs(byID.InclusiveMetric - InclusiveMetric) < .001);
                Debug.Assert(byID.InclusiveMetricByTimeString == InclusiveMetricByTimeString);
                Debug.Assert(byID.FirstTimeRelMSec == FirstTimeRelMSec);
                Debug.Assert(byID.LastTimeRelMSec == LastTimeRelMSec);
                // Because of the weighting (caused by splitting samples) exclusive metric and count may
                // not be the same as the the ByID exclusive metric and count 
            }
#endif
        }

        public IList<CallTreeNodeBase> Callers { get { return m_callers; } }
        public IList<CallTreeNodeBase> Callees { get { return m_callees; } }

        public void ToXml(TextWriter writer, string indent)
        {
            writer.Write("{0}<CallerCallee", indent); this.ToXmlAttribs(writer); writer.WriteLine(">");
            writer.WriteLine("{0} <Callers Count=\"{1}\">", indent, m_callers.Count);
            foreach (CallTreeNodeBase caller in m_callers)
            {
                writer.Write("{0}  <Node", indent);
                caller.ToXmlAttribs(writer);
                writer.WriteLine("/>");
            }
            writer.WriteLine("{0} </Callers>", indent);
            writer.WriteLine("{0} <Callees Count=\"{1}\">", indent, m_callees.Count);
            foreach (CallTreeNodeBase callees in m_callees)
            {
                writer.Write("{0}  <Node", indent);
                callees.ToXmlAttribs(writer);
                writer.WriteLine("/>");
            }
            writer.WriteLine("{0} </Callees>", indent);
            writer.WriteLine("{0}</CallerCallee>", indent);
        }
        public override string ToString()
        {
            StringWriter sw = new StringWriter();
            ToXml(sw, "");
            return sw.ToString();
        }

        public override void Dispose()
        {
            foreach (var node in m_callers)
                node.Dispose();
            m_callers = null;
            foreach (var node in m_callees)
                node.Dispose();
            m_callees = null;
            base.Dispose();
        }
        #region private
        /// <summary>
        /// A caller callee view is a sumation which centers around one 'focus' node which is represented by the CallerCalleeNode.
        /// This node has a caller and callee list, and these nodes (as well as the CallerCalleNode itself) represent the aggregation
        /// over the entire tree.
        /// 
        /// AccumlateSamplesForNode is the routine that takes a part of a aggregated call tree (repsesented by 'treeNode' and adds
        /// in the statistics for that call tree into the CallerCalleeNode aggregations (and its caller and callee lists).  
        /// 
        /// 'recursionsCount' is the number of times the focus node name has occured in the path from 'treeNode' to the root.   In 
        /// addition to setting the CallerCalleeNode aggregation, it also returns a 'weightedSummary' inclusive aggregation 
        /// FOR JUST treeNode (the CallerCalleNode is an aggregation over the entire call tree accumulated so far).  
        /// 
        /// The key problem for this routine to avoid is double counting of inclusive samples in the face of recursive functions. 
        /// Thus all samples are weighted by the recurision count before being included in 'weightedSummaryRet (as well as in
        /// the CallerCalleeNode and its Callers and Callees).    
        /// 
        /// An important optimization is the ability to NOT create (but rather reuse) CallTreeNodes when returning weightedSummaryRet.
        /// To accompish this the weightedSummaryScaleRet is needed.  To get the correct numerical value for weightedSummaryRet, you 
        /// actually have to scale values by weightedSummaryScaleRet before use.   This allows us to represent weights of 0 (subtree has 
        /// no calls to the focus node), or cases where the subtree is completely uniform in its weigthing (the subtree does not contain
        /// any additional focus nodes), by simply returning the tree node itself and scaling it by the recursion count).  
        /// 
        /// isUniformRet is set to false if anyplace in 'treeNode' does not have the scaling factor weightedSummaryScaleRet.  This
        /// means the the caller cannot simply scale 'treeNode' by a weight to get weightedSummaryRet.  
        /// </summary>
        private void AccumlateSamplesForNode(CallTreeNode treeNode, int recursionCount,
            out CallTreeNodeBase weightedSummaryRet, out double weightedSummaryScaleRet, out bool isUniformRet)
        {
            bool isFocusNode = treeNode.Name.Equals(Name);
            if (isFocusNode)
                recursionCount++;

            // We hope we are uniform (will fix if this is not true)
            isUniformRet = true;

            // Compute the weighting.   This is either 0 if we have not yet seen the focus node, or
            // 1/recusionCount if we have (splitting all samples equally among each of the samples)
            weightedSummaryScaleRet = 0;
            weightedSummaryRet = null;          // If the weight is zero, we don't care about the value
            if (recursionCount > 0)
            {
                weightedSummaryScaleRet = 1.0F / recursionCount;

                // We oportunistically hope that all nodes in this subtree have the same weighting and thus
                // we can simply return the treeNode itself as the summary node for this subtree.  
                // This will get corrected to the proper value if our hopes prove unfounded.  
                weightedSummaryRet = treeNode;
            }

            // Get all the samples for the children and set the calleeSum information  We also set the
            // information in the CallerCalleNode's Callees list.  
            if (treeNode.Callees != null)
            {
                for (int i = 0; i < treeNode.m_callees.Count; i++)
                {
                    CallTreeNode treeNodeCallee = treeNode.m_callees[i];

                    // Get the correct weighted summary for the children.  
                    CallTreeNodeBase calleeWeightedSummary;
                    double calleeWeightedSummaryScale;
                    bool isUniform;
                    AccumlateSamplesForNode(treeNodeCallee, recursionCount, out calleeWeightedSummary, out calleeWeightedSummaryScale, out isUniform);

                    // Did we have any samples at all that contained the focus node this treeNode's callee?
                    if (weightedSummaryScaleRet != 0 && calleeWeightedSummaryScale != 0)
                    {
                        // Yes, then add the summary for the treeNode's callee to cooresponding callee node in 
                        // the caller-callee aggregation. 
                        if (isFocusNode)
                        {
                            var callee = Find(ref m_callees, treeNodeCallee.Name);
                            callee.CombineByIdSamples(calleeWeightedSummary, true, calleeWeightedSummaryScale);
                        }

                        // And also add it to the weightedSummaryRet node we need to return.   
                        // This is the trickiest part of this code.  The way this works is that
                        // return value ALWAYS starts with the aggregation AS IF the weighting
                        // was uniform.   However if that proves to be an incorrect assumption
                        // we subtract out the uniform values and add back in the correctly weighted 
                        // values.   
                        if (!isUniform || calleeWeightedSummaryScale != weightedSummaryScaleRet)
                        {
                            isUniformRet = false;       // We ourselves are not uniform.  

                            // We can no longer use the optimization of using the treenode itself as our weighted
                            // summary node because we need to write to it.   Thus replace the node with a copy.  
                            if (weightedSummaryRet == treeNode)
                                weightedSummaryRet = new CallTreeNodeBase(weightedSummaryRet);

                            // Subtract out the unweighted value and add in the weighted one
                            double scale = calleeWeightedSummaryScale / weightedSummaryScaleRet;
                            weightedSummaryRet.m_inclusiveMetric += (float)(calleeWeightedSummary.m_inclusiveMetric * scale - treeNodeCallee.m_inclusiveMetric);
                            weightedSummaryRet.m_inclusiveCount += (float)(calleeWeightedSummary.m_inclusiveCount * scale - treeNodeCallee.m_inclusiveCount);
                            if (weightedSummaryRet.m_inclusiveMetricByTime != null)
                            {
                                weightedSummaryRet.m_inclusiveMetricByTime.AddScaled(calleeWeightedSummary.m_inclusiveMetricByTime, scale);
                                weightedSummaryRet.m_inclusiveMetricByTime.AddScaled(treeNodeCallee.m_inclusiveMetricByTime, -1);
                            }
                            if (weightedSummaryRet.m_inclusiveMetricByScenario != null)
                            {
                                weightedSummaryRet.m_inclusiveMetricByScenario.AddScaled(calleeWeightedSummary.m_inclusiveMetricByScenario, scale);
                                weightedSummaryRet.m_inclusiveMetricByScenario.AddScaled(treeNodeCallee.m_inclusiveMetricByScenario, -1);
                            }
                        }
                    }
                }
            }

            // OK we are past the tricky part of creating a weighted summary node.   If this is a focus node, we can simply
            // Add this aggregation to the CallerCallee node itself as well as the proper Caller node.  
            if (isFocusNode)
            {
                this.CombineByIdSamples(weightedSummaryRet, true, weightedSummaryScaleRet);

                // Set the Caller information now 
                CallTreeNode callerTreeNode = treeNode.Caller;
                if (callerTreeNode != null)
                    Find(ref m_callers, callerTreeNode.Name).CombineByIdSamples(weightedSummaryRet, true, weightedSummaryScaleRet);
            }
        }

        /// <summary>
        /// Find the Caller-Callee treeNode in 'elems' with name 'frameName'.  Always succeeds because it
        /// creates one if necessary. 
        /// </summary>
        private CallTreeNodeBase Find(ref List<CallTreeNodeBase> elems, string frameName)
        {
            CallTreeNodeBase elem;
            for (int i = 0; i < elems.Count; i++)
            {
                elem = elems[i];
                if (elem.Name == frameName)
                    return elem;
            }
            elem = new CallTreeNodeBase(frameName, StackSourceFrameIndex.Invalid, m_callTree);
            elems.Add(elem);
            return elem;
        }

        // state;
        private List<CallTreeNodeBase> m_callers;
        private List<CallTreeNodeBase> m_callees;
        #endregion
    }

    /// <summary>
    /// AggregateCallTreeNode supports a multi-level caller-callee view.   
    /// 
    /// It does this by allow you to take any 'focus' node (typically a byname node)
    /// and compute a tree of its callers and a tree of its callees.   You do this
    /// by passing the node of interested to either the 'CallerTree' or 'CalleeTrees'.
    /// 
    /// The AggregateCallTreeNode remembers if if is a caller or callee node and its
    /// 'Callees' method returns the children (which may in fact be Callers). 
    /// 
    /// What is nice about 'AggregateCallTreeNode is that it is lazy, and you only 
    /// form the part of the tree you actually explore.     A classic 'caller-callee' 
    /// view is simply the caller and callee trees only explored to depth 1.
    /// </summary>
    public class AggregateCallTreeNode : CallTreeNode, IDisposable
    {
        /// <summary>
        /// Given any node (typically a byName node, but it works on any node), Create a 
        /// tree rooted at 'node' that represents the callers of that node.  
        /// </summary>
        public static CallTreeNode CallerTree(CallTreeNodeBase node)
        {
            var ret = new AggregateCallTreeNode(node, null, 0);

            node.GetTrees(delegate(CallTreeNode tree)
            {
                ret.m_trees.Add(tree);
            });
            ret.CombineByIdSamples(node, true);
            return ret;
        }
        /// <summary>
        /// Given any node (typically a byName node, but it works on any node), Create a 
        /// tree rooted at 'node' that represents the callees of that node.  
        /// </summary>
        public static CallTreeNode CalleeTree(CallTreeNodeBase node)
        {
            var ret = new AggregateCallTreeNode(node, null, -1);

            node.GetTrees(delegate(CallTreeNode tree)
            {
                ret.m_trees.Add(tree);
            });
            ret.CombineByIdSamples(node, true);
            return ret;
        }

        /// <summary>
        /// Calls 'callback' for each distinct call tree in this node.  Note that the same
        /// trees can overlap (in the case of recursive functions), so you need a mechanism
        /// for visiting a tree only once.  
        /// </summary>
        public override void GetTrees(Action<CallTreeNode> callback)
        {
            foreach (var tree in m_trees)
                callback(tree);
        }

        public override string ToString()
        {
            StringWriter sw = new StringWriter();
            sw.Write("<AggregateCallTreeNode");
            base.ToXmlAttribs(sw);
            sw.WriteLine(" CallerOffset=\"{0}\" TreeNodeCount=\"{1}\"/>", m_callerOffset, m_trees.Count);
            return sw.ToString();
        }
        #region private
        public override void Dispose()
        {
            base.Dispose();
            m_trees.Clear();
        }

        protected override List<CallTreeNode> GetCallees()
        {
            var ret = new List<CallTreeNode>();
            if (IsCalleeTree)
            {
                foreach (var tree in m_trees)
                    MergeCallee(tree, ret);

                // By calling MergeCallee on tree, we have walked the entire forest in 'ret'
                // and have set the m_recursion bit if the node contains m_idToExclude.   
                // To avoid having to walk the tree again, we set this m_idToExclude to Invalid
                // for trees that are known not to contain m_idToExclude, which allows us to
                // skip most trees 
                if (m_idToExclude != StackSourceFrameIndex.Invalid)
                {
                    foreach (AggregateCallTreeNode callee in ret)
                        if (!callee.m_recursion)
                            callee.m_idToExclude = StackSourceFrameIndex.Invalid;
                }
            }
            else
            {
                foreach (var tree in m_trees)
                    MergeCaller(tree, ret, m_callerOffset);
            }

            ret.Sort((x, y) => Math.Abs(y.InclusiveMetric).CompareTo(Math.Abs(x.InclusiveMetric)));
#if DEBUG
            // Check that the exc time + children inc time = inc time 
            var incCountChildren = 0.0F;
            var incMetricChildren = 0.0F;

            foreach (var callee in ret)
            {
                incCountChildren += callee.InclusiveCount;
                incMetricChildren += callee.InclusiveMetric;
            }
            if (IsCalleeTree)
            {
                Debug.Assert(Math.Abs(InclusiveCount - (ExclusiveCount + incCountChildren)) <= Math.Abs(InclusiveCount / 1000.0F));
                Debug.Assert(Math.Abs(InclusiveMetric - (ExclusiveMetric + incMetricChildren)) <= Math.Abs(InclusiveMetric / 1000.0F));
            }
            else
            {
                if (ret.Count != 0)
                {
                    // For caller nodes, the root node has no children, but does have inclusive count
                    Debug.Assert(Math.Abs(InclusiveCount - incCountChildren) < Math.Abs(InclusiveCount / 1000.0F));
                    Debug.Assert(Math.Abs(InclusiveMetric - incMetricChildren) < Math.Abs(InclusiveMetric / 1000.0F));
                }
            }
#endif
            return ret;
        }

        /// <summary>
        /// See m_callerOffset and MergeCallee for more.
        /// 
        /// The 'this' node is a AggregateCallTree representing the 'callers' nodes.  Like 
        /// MergeCallee the agregate node represents a list of CallTreeNodes.   Howoever unlike
        /// MergeCallee, the list of CallTreeNodes each represent a sample (a complete call stack)
        /// and 'callerOffset' indicates how far 'up' that stack is the node of interest.  
        /// </summary>
        private void MergeCaller(CallTreeNode treeNode, List<CallTreeNode> callerList, int callerOffset)
        {
            // treeNode represents the sample (the complete call stack), but we want the node 
            // 'callerOffset' up the stack toward the root.  Calculate that here.  
            CallTreeNode treeForNode = treeNode;
            for (int i = 0; i < callerOffset; i++)
                treeForNode = treeForNode.Caller;
            CallTreeNode treeForCaller = treeForNode.Caller;
            if (treeForCaller == null)
                return;

            // Next find or make a node for 'treeForCaller' in the 'callerList' of child nodes
            // we are creating.   
            AggregateCallTreeNode childWithID = FindNodeInList(treeForCaller.ID, callerList);
            if (childWithID == null)
            {
                childWithID = new AggregateCallTreeNode(treeNode, this, callerOffset + 1);
                // TODO breaking abstraction.
                childWithID.m_id = treeForCaller.ID;
                childWithID.m_name = treeForCaller.Name;
                callerList.Add(childWithID);
            }

            // Add this tree to the node we found.
            childWithID.m_trees.Add(treeNode);

            // And compute our statistics.  
            // We pass addExclusive=false to CombindByIdSamples because callers never have exclusive samples
            // associated with them (because all samples occured lower in the stack
            childWithID.CombineByIdSamples(treeNode, true, 1, false);

            // To get the correct inclusive time you also have to subract out the any double counting. 
            if (m_idToExclude != StackSourceFrameIndex.Invalid)
            {
                if (treeNode.Callees != null)
                {
                    foreach (var callee in treeNode.Callees)
                        SubtractOutTrees(callee, m_idToExclude, childWithID);
                }
            }
        }

        /// <summary>
        /// An aggregateCallTreeNode is exactly that, the sum of several callTrees
        /// (each of which represent a number of individual samples).    Thus we had to 
        /// take each sample (which is 'treenode' and merge it into the aggregate.
        /// We do this one at a time.   Thus we call MergeCallee for each calltree 
        /// in our list and we find the 'callees' of each of those nodes, and create 
        /// aggregates for the children (which is in calleeList).   
        /// </summary>
        private void MergeCallee(CallTreeNode treeNode, List<CallTreeNode> calleeList)
        {
            if (treeNode.Callees != null)
            {
                foreach (var treeCallee in treeNode.Callees)
                {
                    // Skip any children we were told to skip.  
                    if (treeCallee.ID == m_idToExclude)
                        continue;

                    AggregateCallTreeNode childWithID = FindNodeInList(treeCallee.ID, calleeList);
                    if (childWithID == null)
                    {
                        childWithID = new AggregateCallTreeNode(treeCallee, this, -1);
                        calleeList.Add(childWithID);
                    }

                    childWithID.m_trees.Add(treeCallee);

                    // Start to the normal inclusive counts
                    childWithID.CombineByIdSamples(treeCallee, true);

                    // Optimization if we know there are not samples to exclude, we don't need to do any ajustment   
                    if (m_idToExclude != StackSourceFrameIndex.Invalid)
                        SubtractOutTrees(treeCallee, m_idToExclude, childWithID);
                }
            }
        }

        /// <summary>
        /// Traverse 'treeCallee' and subtract out the inclusive time for any tree that matches 'idToExclude' from the node 'statsRet'.
        /// This is needed in AggregateCallTrees because the same trees from the focus node are in the list to aggregate, but are also
        /// in the subtree's in various places (and thus are counted twice).   We solve this by walking this subtree (in this routine)
        /// and subtracting out any nodes that match 'idToExclude'.   
        /// 
        /// As an optimization this routine also sets the m_recurision bit 'statsRet' if anywhere in 'treeCallee' we do find an id to 
        /// exclude.  That way in a common case (where there is no instances of 'idToExclude') we don't have to actualy walk the
        /// tree the second time (we simply know that there is no adjustment necessary.   
        /// </summary>
        private static void SubtractOutTrees(CallTreeNode treeCallee, StackSourceFrameIndex idToExclude, AggregateCallTreeNode statsRet)
        {
            if (treeCallee.ID == idToExclude)
            {
                statsRet.m_recursion = true;
                statsRet.CombineByIdSamples(treeCallee, true, -1, false);
                return;
            }
            // Subtract out any times we should have excluded
            if (treeCallee.Callees != null)
            {
                foreach (var callee in treeCallee.Callees)
                    SubtractOutTrees(callee, idToExclude, statsRet);
            }
        }

        private static AggregateCallTreeNode FindNodeInList(StackSourceFrameIndex id, List<CallTreeNode> calleeList)
        {
            foreach (var aggCallee in calleeList)
            {
                if (id == aggCallee.ID)
                    return (AggregateCallTreeNode)aggCallee;
            }
            return null;
        }

        internal AggregateCallTreeNode(CallTreeNodeBase node, AggregateCallTreeNode caller, int callerOffset)
            : base(node.Name, node.ID, caller, node.CallTree)
        {
            // Remember what the samples were by setting m_trees, which contain the actual samples 
            m_trees = new List<CallTreeNode>();
            m_callerOffset = callerOffset;

            if (caller != null)
                m_idToExclude = caller.m_idToExclude;
            else
            {
                m_idToExclude = node.ID;
                // Optimization. we know there is  no recursion for the root node without checking.    
                if (m_idToExclude == CallTree.Root.ID)
                    m_idToExclude = StackSourceFrameIndex.Invalid;
            }
        }

        protected override StackSourceFrameIndex GetExcludeChildID()
        {
            return m_idToExclude;
        }

        protected internal override bool IsCalleeTree { get { return m_callerOffset < 0; } set { Debug.Assert(value); m_callerOffset = -1; } }

        /// <summary>
        /// An AggregateCallTree remembers all its samples by maintaining a list of calltrees 
        /// that actually contain the samples that the Aggregate respresents.  m_trees hold this.   
        /// </summary>
        List<CallTreeNode> m_trees;

        /// <summary>
        /// AggregateCallTreeNode can represent either a 'callers' tree or a 'callees' tree.   For 
        /// the 'callers' tree case the node represented by the aggregate does NOT have same ID as
        /// the tree in the m_trees list.   Instead the aggreegate is some node 'up the chain' toward 
        /// the caller.  m_callerOffset keeps track of this (it is the same number for all elements 
        /// in m_trees).   
        /// 
        /// For callee nodes, this number is not needed.   Thus we use a illegal value (-1) to 
        /// represent that fact that the node is a callee node rather than a caller node.  
        /// </summary>
        internal int m_callerOffset;
        StackSourceFrameIndex m_idToExclude;  // We should exclude any children with this ID as they are already counted.  
        bool m_recursion;                     // Set to true if m_idToExclude does exists in 'm_trees' somewhere 
        #endregion
    }
}
