//     Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Diagnostics.Tracing;
using Diagnostics.Tracing.Parsers;
using FastSerialization;
using Utilities;
using System.IO;
using System.Diagnostics.Tracing;

namespace Diagnostics.Tracing.Parsers
{
    /// <summary>
    /// This parser knows how to decode Windows Software Trace Preprocessor (WPP) events.  In order to decode
    /// the events it needs access to the TMF files that describe the events (these are created from the PDB at 
    /// build time. 
    /// 
    /// Mostly what you use this for is the 'FormattedMessage' property of the event.  
    /// </summary>
    public sealed class WppTraceEventParser : ExternalTraceEventParser
    {
        public WppTraceEventParser(TraceEventSource source, string TMFDirectory)
            : base(source)
        {
            m_TMFDirectory = TMFDirectory;
        }

        #region private
        /// <summary>
        /// This one is for TraceLog deserialization
        /// </summary>
        public WppTraceEventParser(TraceEventSource source) : base(source) { }

        unsafe protected override DynamicTraceEventData TryLookup(TraceEvent unknownEvent)
        {
            // WPP is always classic 
            if (unknownEvent.ClassicProvider)
            {
                var taskGuid = unknownEvent.taskGuid;
                var tmfPath = GetTmfPathForTaskGuid(taskGuid);
                if (tmfPath != null)
                {
                    var templates = CreateTemplatesForTMFFile(taskGuid, tmfPath);

                    // Register all the templates in the file, and if we found the specific one we are looking for return that one. 
                    DynamicTraceEventData ret = null;
                    foreach (var template in templates)
                    {
                        if (template.eventID == unknownEvent.eventID)
                            ret = template;
                        else
                            RegisterTemplate(template);
                    }
                    // If we fail, remove the file so we don't continually try to parse the file.  
                    if (ret == null)
                        m_tmfDataFilePathsByFileNameBase[taskGuid.ToString()] = null;
                    return ret;
                }
            }
            return null;
        }

        private string GetTmfPathForTaskGuid(Guid taskGuid)
        {
            if (m_tmfDataFilePathsByFileNameBase == null)
            {
                m_tmfDataFilePathsByFileNameBase = new Dictionary<string, string>(64);
                foreach (var path in DirectoryUtilities.GetFiles(m_TMFDirectory))
                {
                    var fileNameBase = Path.GetFileNameWithoutExtension(path);
                    m_tmfDataFilePathsByFileNameBase[fileNameBase] = path;
                }
            }

            string ret;
            m_tmfDataFilePathsByFileNameBase.TryGetValue(taskGuid.ToString(), out ret);
            return ret;
        }

        private List<DynamicTraceEventData> CreateTemplatesForTMFFile(Guid taskGuid, string tmfPath)
        {
            List<DynamicTraceEventData> templates = new List<DynamicTraceEventData>();
            List<Type> parameterTypes = new List<Type>();

            using (StreamReader tmfData = File.OpenText(tmfPath))
            {
                string taskName = null;
                string providerName = null;
                Guid providerGuid = Guid.Empty;
                Match m;
                for (; ; )
                {
                    var line = tmfData.ReadLine();
                    if (line == null)
                        break;

                    if (providerGuid == Guid.Empty)
                    {
                        m = Regex.Match(line, @"PDB: .*?(\w+)\.pdb\s*$", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            // We use the name of the mof file (which is the same as the PDB file) as the provider name.
                           if (string.IsNullOrEmpty(providerName))
                                providerName = m.Groups[1].Value;

                            string mofFilePath;
                            if (m_tmfDataFilePathsByFileNameBase.TryGetValue(providerName, out mofFilePath))
                            {
                                if (mofFilePath.EndsWith(".mof", StringComparison.OrdinalIgnoreCase))
                                {
                                    using (var mofFile = File.OpenText(mofFilePath))
                                    {
                                        for (; ; )
                                        {
                                            var mofLine = mofFile.ReadLine();
                                            if (mofLine == null)
                                                break;
                                            m = Regex.Match(mofLine, @"guid\(.{(.*)}.\)", RegexOptions.IgnoreCase);
                                            if (m.Success)
                                            {
                                                try { providerGuid = new Guid(m.Groups[1].Value); }
                                                catch (Exception) { }
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (taskName == null)
                    {
                        // 7113b9e1-a0cc-d313-1eab-57efe9d7e56c build.server // SRC=TTSEngineCom.cpp MJ= MN=
                        m = Regex.Match(line, @"^\w+-\w+-\w+-\w+-\w+\s+(\S+)");
                        if (m.Success)
                            taskName = m.Groups[1].Value;
                    }
                    else
                    {
                        // #typev  ttstracing_cpp78 13 "%0%10!s! Error happens in Initializing %11!s!!" //   LEVEL=TRACE_LEVEL_ERROR FLAGS=TTS_Trace_Engine_Initialization FUNC=CTTSTracingHelper::LogComponentInitialization
                        m = Regex.Match(line, "^#typev\\s+(\\S*?)(\\d+)\\s+(\\d+)\\s+\"(.*)\"");
                        if (m.Success)
                        {
                            var fileName = m.Groups[1].Value;
                            var lineNum = int.Parse(m.Groups[2].Value);
                            var eventId = int.Parse(m.Groups[3].Value);
                            var formatStr = m.Groups[4].Value;

                            var eventProviderName = taskName;
                            if (providerName != null)
                                eventProviderName = providerName + "/" + eventProviderName;

                            var template = new DynamicTraceEventData(null, eventId, 0, fileName + "/" + m.Groups[2].Value, taskGuid, 0, "", providerGuid, eventProviderName);
                            template.lookupAsWPP = true;                // Use WPP lookup convetions. 
                            formatStr = formatStr.Replace("%0", "");    // TODO What is this?  Why is it here?  
                            formatStr = Regex.Replace(formatStr, @"%(\d+)!.!", delegate(Match match) { return "%" + (int.Parse(match.Groups[1].Value) - 9).ToString(); });
                            template.MessageFormat = formatStr;

                            parameterTypes.Clear();
                            for (; ; )
                            {
                                line = tmfData.ReadLine();
                                if (line == null)
                                    break;
                                if (line.Trim() == "}")
                                    break;
                                // szPOSHeader, ItemString -- 10
                                m = Regex.Match(line, @"^\S+, Item(\w+) -- (\d+)");
                                if (m.Success)
                                {
                                    var typeStr = m.Groups[1].Value;
                                    Type type = null;
                                    if (typeStr == "String")
                                        type = typeof(string);
                                    else if (typeStr == "Long")
                                        type = typeof(int);
                                    else if (typeStr == "Double")
                                        type = typeof(double);

                                    if (type != null)
                                        parameterTypes.Add(type);
                                }
                            }
                            template.payloadNames = new string[parameterTypes.Count];
                            template.payloadFetches = new DynamicTraceEventData.PayloadFetch[parameterTypes.Count];
                            ushort offset = 0;
                            for (int i = 0; i < parameterTypes.Count; i++)
                            {
                                template.payloadNames[i] = "Arg" + (i + 1).ToString();
                                template.payloadFetches[i].type = parameterTypes[i];
                                template.payloadFetches[i].offset = offset;
                                var size = DynamicTraceEventData.SizeOfType(parameterTypes[i]);
                                if (template.payloadFetches[i].type == typeof(string))
                                    size |= DynamicTraceEventData.IS_ANSI;
                                template.payloadFetches[i].size = size;
                                if (size >= DynamicTraceEventData.SPECIAL_SIZES)
                                    offset = ushort.MaxValue;           // Indicate that the offset must be computed at run time.
                                else
                                    offset += size;
                            }
                            templates.Add(template);
                        }
                    }
                }
            }
            return templates;
        }

        string m_TMFDirectory;
        Dictionary<string, string> m_tmfDataFilePathsByFileNameBase;       
        #endregion
    }

#if false // Example use
    public class Test
    {
        static void RunTest()
        {
            var tmfDirectory = ".";
            var sessionName = "My Real Time Session";

            TraceEventSession session = null;
            ETWTraceEventSource source = null;
            bool started = false;
            // Start a thread to listen for incoming events in real time. 
            var listenThread = new System.Threading.Thread(delegate()
            {
                using (session = new TraceEventSession(sessionName, null))
                {
                    session.StopOnDispose = true;
                    using (source = new ETWTraceEventSource(sessionName, TraceEventSourceType.Session))
                    {
                        session.EnableProvider(WPPProviderGuid1, (TraceEventLevel)200, ulong.MaxValue);
                        session.EnableProvider(WPPProviderGuid2, (TraceEventLevel)200, ulong.MaxValue);

                        started = true;
                        // This is my callback.  Right now I just print.  
                        Action<TraceEvent> print = delegate(TraceEvent data) { Console.WriteLine(data.ToString()); };

                        // Wire up callbacks 
#if false               // Other parsers you could enable
                        var dynamicParser = source.Dynamic;         // EventSources
                        dynamicParser.ReadAllManifests(".");        // If we have explicit manifests we wish to use (but not register with the OS).  
                        dynamicParser.All += print;   

                        var registeredParser = new RegisteredTraceEventParser(source);      // OS build in events
                        registeredParser.All += print;
#endif
                        var wppParser = new WppTraceEventParser(source, tmfDirectory);      // WPP where we have the TMF files in 'tmfDirectory'
                        wppParser.All += print;

                        source.UnhandledEvent += print;     // Optional.  Shows events you don't recognize.  probably worth investigating. 
                        source.Process();   // listen for incomming events. 
                    }
                }
            });

            // Wait for startup
            while (!started)
                System.Threading.Thread.Sleep(1);

            Console.WriteLine("Listening for 1 min");
            System.Threading.Thread.Sleep(60000);

            // To stop listening
            Console.WriteLine("Stopping listening");
            source.StopProcessing();
            source.Dispose();
            session.Dispose();

            Console.WriteLine("Done");
        }
    }
#endif
}
