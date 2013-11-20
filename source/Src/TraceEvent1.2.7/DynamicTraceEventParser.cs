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
    /// A DynamicTraceEventParser is a parser that understands how to read the embedded manifests that occur in the 
    /// dataStream (System.Diagnostics.Tracing.EventSources do this).   
    /// 
    /// See also code:TDHDynamicTraceEventParser which knows how to read the manifest that are registered globally with
    /// the machine.   
    /// </summary>
    public class DynamicTraceEventParser : TraceEventParser
    {
        public DynamicTraceEventParser(TraceEventSource source)
            : base(source)
        {
            if (source == null)         // Happens during deserialization.  
                return;

            // Try to retieve persisted state 
            state = (DynamicTraceEventParserState)StateObject;
            if (state == null)
            {
                StateObject = state = new DynamicTraceEventParserState();
                dynamicManifests = new Dictionary<Guid, DynamicManifestInfo>();

                this.source.RegisterUnhandledEvent(delegate(TraceEvent data)
                {
                    if (data.ID != (TraceEventID)0xFFFE)
                        return data;

                    // Look up our information. 
                    DynamicManifestInfo dynamicManifest;
                    if (!dynamicManifests.TryGetValue(data.ProviderGuid, out dynamicManifest))
                    {
                        dynamicManifest = new DynamicManifestInfo();
                        dynamicManifests.Add(data.ProviderGuid, dynamicManifest);
                    }

                    ProviderManifest provider = dynamicManifest.AddChunk(data);
                    // We have a completed manifest, add it to our list.  
                    if (provider != null)
                    {
                        AddDynamicProvider(provider);
                        //SLAB update
                        this.ManifestReceived(provider);
                    }

                    return data;
                });
            }
            else if (allCallbackCalled)
            {
                foreach (ProviderManifest provider in state.providers.Values)
                    provider.AddProviderEvents(source, allCallback);
            }
        }

        public override event Action<TraceEvent> All
        {
            add
            {
                if (state != null)
                {
                    foreach (ProviderManifest provider in state.providers.Values)
                        provider.AddProviderEvents(source, value);
                }
                allCallback += value;
                allCallbackCalled = true;
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }

        //SLAB update
        //notify when a new manifest is received
        public event Action<ProviderManifest> ManifestReceived;

        /// <summary>
        /// Returns a list of providers (their manifest) that this TraceParser knows about.   
        /// </summary>
        public IEnumerable<ProviderManifest> DynamicProviders
        {
            get
            {
                return state.providers.Values;
            }
        }

        /// <summary>
        /// Given a manifest describing the provider add its information to the parser.  
        /// </summary>
        /// <param name="providerManifest"></param>
        public void AddDynamicProvider(ProviderManifest providerManifest)
        {
            // Remember this serialized information.
            state.providers[providerManifest.Guid] = providerManifest;

            // If someone as asked for callbacks on every event, then include these too. 
            if (allCallbackCalled)
                providerManifest.AddProviderEvents(source, allCallback);
        }

        /// <summary>
        /// Utility method that stores all the manifests known to the DynamicTraceEventParser to the directory 'directoryPath'
        /// </summary>
        public void WriteAllManifests(string directoryPath)
        {
            Directory.CreateDirectory(directoryPath);
            foreach (var providerManifest in DynamicProviders)
            {
                var filePath = Path.Combine(directoryPath, providerManifest.Name + ".manifest.xml");
                providerManifest.WriteToFile(filePath);
            }
        }

        /// <summary>
        /// Utility method that read all the manifests the directory 'directoryPath' into the parser.  
        /// </summary>        
        public void ReadAllManifests(string directoryPath)
        {
            foreach (var fileName in Directory.GetFiles(directoryPath, "*.manifest.xml"))
            {
                AddDynamicProvider(new ProviderManifest(fileName));
            }
            foreach (var fileName in Directory.GetFiles(directoryPath, "*.man"))
            {
                AddDynamicProvider(new ProviderManifest(fileName));
            }
        }

        #region private
        // This allows protected members to avoid the normal initization.  
        protected DynamicTraceEventParser(TraceEventSource source, bool noInit) : base(source) { }

        private class DynamicManifestInfo
        {
            internal DynamicManifestInfo() { }

            byte[][] Chunks;
            int ChunksLeft;
            ProviderManifest provider;
            byte majorVersion;
            byte minorVersion;
            ManifestEnvelope.ManifestFormats format;

            // SLAB update
            static readonly TimeSpan ChunkReceptionStalePeriod = TimeSpan.FromMinutes(5);
            DateTime? lastChunkReceptionTime;

            internal unsafe ProviderManifest AddChunk(TraceEvent data)
            {
                //SLAB update
                //removed check to allow recomputing new manifest on any new incoming chunk 
                //if (provider != null)
                //    return null;

                // TODO 
                if (data.EventDataLength <= sizeof(ManifestEnvelope) || data.GetByteAt(3) != 0x5B)  // magic number 
                    return null;

                ushort totalChunks = (ushort)data.GetInt16At(4);
                ushort chunkNum = (ushort)data.GetInt16At(6);
                if (chunkNum >= totalChunks || totalChunks == 0)
                    return null;

                // SLAB update
                if (Chunks == null || (lastChunkReceptionTime.HasValue && lastChunkReceptionTime.Value.Add(ChunkReceptionStalePeriod) < DateTime.Now))
                {
                    format = (ManifestEnvelope.ManifestFormats)data.GetByteAt(0);
                    majorVersion = (byte)data.GetByteAt(1);
                    minorVersion = (byte)data.GetByteAt(2);
                    ChunksLeft = totalChunks;
                    Chunks = new byte[ChunksLeft][];
                }
                else
                {
                    // Chunks have to agree with the format and version information. 
                    if (format != (ManifestEnvelope.ManifestFormats)data.GetByteAt(0) ||
                        majorVersion != data.GetByteAt(1) || minorVersion != data.GetByteAt(2))
                        return null;
                }

                if (Chunks[chunkNum] != null)
                    return null;

                byte[] chunk = new byte[data.EventDataLength - 8];
                Chunks[chunkNum] = data.EventData(chunk, 0, 8, chunk.Length);
                --ChunksLeft;
                lastChunkReceptionTime = DateTime.Now;  // SLAB update

                if (ChunksLeft > 0)
                    return null;

                // OK we have a complete set of chunks
                byte[] serializedData = Chunks[0];
                if (Chunks.Length > 1)
                {
                    int totalLength = 0;
                    for (int i = 0; i < Chunks.Length; i++)
                        totalLength += Chunks[i].Length;

                    // Concatinate all the arrays. 
                    serializedData = new byte[totalLength];
                    int pos = 0;
                    for (int i = 0; i < Chunks.Length; i++)
                    {
                        Array.Copy(Chunks[i], 0, serializedData, pos, Chunks[i].Length);
                        pos += Chunks[i].Length;
                    }
                }
                Chunks = null;
                lastChunkReceptionTime = null;  // SLAB update
                // string str = Encoding.UTF8.GetString(serializedData);
                provider = new ProviderManifest(serializedData, format, majorVersion, minorVersion);
                provider.ISDynamic = true;
                return provider;
            }
        }

        DynamicTraceEventParserState state;
        private Dictionary<Guid, DynamicManifestInfo> dynamicManifests;
        Action<TraceEvent> allCallback;
        bool allCallbackCalled;
        #endregion
    }

    /// <summary>
    /// A ProviderManifest represents the XML manifest associated with teh provider.    
    /// </summary>
    public class ProviderManifest : IFastSerializable
    {
        // create a manifest from a stream or a file
        /// <summary>
        /// Read a ProviderManifest from a stream
        /// </summary>
        public ProviderManifest(Stream manifestStream, int manifestLen = int.MaxValue)
        {
            format = ManifestEnvelope.ManifestFormats.SimpleXmlFormat;
            int len = Math.Min((int)(manifestStream.Length - manifestStream.Position), manifestLen);
            serializedManifest = new byte[len];
            manifestStream.Read(serializedManifest, 0, len);
        }
        /// <summary>
        /// Read a ProviderManifest from a file. 
        /// </summary>
        public ProviderManifest(string manifestFilePath)
        {
            format = ManifestEnvelope.ManifestFormats.SimpleXmlFormat;
            serializedManifest = File.ReadAllBytes(manifestFilePath);
        }

        // write a manifest to a stream or a file.  
        /// <summary>
        /// Writes the manifest to 'outputStream' (as UTF8 XML text)
        /// </summary>
        public void WriteToStream(Stream outputStream)
        {
            outputStream.Write(serializedManifest, 0, serializedManifest.Length);
        }
        /// <summary>
        /// Writes the manifest to a file 'filePath' (as a UTF8 XML)
        /// </summary>
        /// <param name="filePath"></param>
        public void WriteToFile(string filePath)
        {
            using (var stream = File.Create(filePath))
                WriteToStream(stream);
        }

        /// <summary>
        ///  Set if this manifest came from the ETL data stream file.  
        /// </summary>
        public bool ISDynamic { get; internal set; }
        // Get at the data in the manifest.   
        public string Name { get { if (!inited) Init(); return name; } }
        public Guid Guid { get { if (!inited) Init(); return guid; } }
        /// <summary>
        /// Retrieve manifest as one big string.  
        /// </summary>
        public string Manifest { get { if (!inited) Init(); return Encoding.UTF8.GetString(serializedManifest); } }
        /// <summary>
        /// Retrieve the manifest as XML
        /// </summary>
        public XmlReader ManifestReader
        {
            get
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.IgnoreComments = true;
                settings.IgnoreWhitespace = true;

                // TODO FIX NOW remove 
                // var manifest = System.Text.Encoding.UTF8.GetString(serializedManifest);
                // Trace.WriteLine(manifest);

                System.IO.MemoryStream stream = new System.IO.MemoryStream(serializedManifest);
                return XmlReader.Create(stream, settings);
            }
        }

        #region private
        internal ProviderManifest(byte[] serializedManifest, ManifestEnvelope.ManifestFormats format, byte majorVersion, byte minorVersion)
        {
            this.serializedManifest = serializedManifest;
            this.majorVersion = majorVersion;
            this.minorVersion = minorVersion;
            this.format = format;
        }

        internal void AddProviderEvents(ITraceParserServices source, Action<TraceEvent> callback)
        {
            if (Error != null)
                return;
            if (!inited)
                Init();
            try
            {
                Dictionary<string, int> opcodes = new Dictionary<string, int>();
                opcodes.Add("win:Info", 0);
                opcodes.Add("win:Start", 1);
                opcodes.Add("win:Stop", 2);
                opcodes.Add("win:DC_Start", 3);
                opcodes.Add("win:DC_Stop", 4);
                opcodes.Add("win:Extension", 5);
                opcodes.Add("win:Reply", 6);
                opcodes.Add("win:Resume", 7);
                opcodes.Add("win:Suspend", 8);
                opcodes.Add("win:Send", 9);
                opcodes.Add("win:Receive", 240);
                Dictionary<string, TaskInfo> tasks = new Dictionary<string, TaskInfo>();
                Dictionary<string, TemplateInfo> templates = new Dictionary<string, TemplateInfo>();
                Dictionary<string, IDictionary<long, string>> maps = null;
                Dictionary<string, string> strings = new Dictionary<string, string>();
                IDictionary<long, string> map = null;
                List<EventInfo> events = new List<EventInfo>();
                bool alreadyReadMyCulture = false;            // I read my culture some time in the past (I can igore things)
                string cultureBeingRead = null;
                while (reader.Read())
                {
                    // TODO I currently require opcodes,and tasks BEFORE events BEFORE templates.  
                    // Can be fixed by going multi-pass. 
                    switch (reader.Name)
                    {
                        case "event":
                            {
                                int taskNum = 0;
                                Guid taskGuid = Guid;
                                string taskName = reader.GetAttribute("task");
                                if (taskName != null)
                                {
                                    TaskInfo taskInfo;
                                    if (tasks.TryGetValue(taskName, out taskInfo))
                                    {
                                        taskNum = taskInfo.id;
                                        taskGuid = taskInfo.guid;
                                    }
                                }
                                else
                                    taskName = "";

                                int eventID = int.Parse(reader.GetAttribute("value"));
                                int opcode = 0;
                                string opcodeName = reader.GetAttribute("opcode");
                                if (opcodeName != null)
                                {
                                    opcodes.TryGetValue(opcodeName, out opcode);
                                    // Strip off any namespace prefix.  TODO is this a good idea?
                                    int colon = opcodeName.IndexOf(':');
                                    if (colon >= 0)
                                        opcodeName = opcodeName.Substring(colon + 1);
                                }
                                else
                                {
                                    opcodeName = "";
                                    // opcodeName = "UnknownEvent" + eventID.ToString();
                                }

                                DynamicTraceEventData eventTemplate = new DynamicTraceEventData(
                                callback, eventID, taskNum, taskName, taskGuid, opcode, opcodeName, Guid, Name);
                                events.Add(new EventInfo(eventTemplate, reader.GetAttribute("template")));

                                // This will be looked up in the string table in a second pass.  
                                eventTemplate.MessageFormat = reader.GetAttribute("message");
                            } break;
                        case "template":
                            {
                                string templateName = reader.GetAttribute("tid");
                                Debug.Assert(templateName != null);
#if DEBUG
                                try
                                {
#endif
                                    templates.Add(templateName, ComputeFieldInfo(reader.ReadSubtree(), maps));
#if DEBUG
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine("Error: Exception during processing template {0}: {1}", templateName, e.ToString());
                                    throw;
                                }
#endif
                            } break;
                        case "opcode":
                            // TODO use message for opcode if it is available so it is localized.  
                            opcodes.Add(reader.GetAttribute("name"), int.Parse(reader.GetAttribute("value")));
                            break;
                        case "task":
                            {
                                TaskInfo info = new TaskInfo();
                                info.id = int.Parse(reader.GetAttribute("value"));
                                string guidString = reader.GetAttribute("eventGUID");
                                if (guidString != null)
                                    info.guid = new Guid(guidString);
                                tasks.Add(reader.GetAttribute("name"), info);
                            } break;
                        case "valueMap":
                            map = new Dictionary<long, string>();    // value maps use dictionaries
                            goto DoMap;
                        case "bitMap":
                            map = new SortedList<long, string>();    // Bitmaps stored as sorted lists
                            goto DoMap;
                        DoMap:
                            string name = reader.GetAttribute("name");
                            var mapValues = reader.ReadSubtree();
                            while (mapValues.Read())
                            {
                                if (mapValues.Name == "map")
                                {
                                    string keyStr = reader.GetAttribute("value");
                                    long key;
                                    if (keyStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                                        key = long.Parse(keyStr.Substring(2), System.Globalization.NumberStyles.AllowHexSpecifier);
                                    else
                                        key = long.Parse(keyStr);
                                    string value = reader.GetAttribute("message");
                                    map[key] = value;
                                }
                            }
                            if (maps == null)
                                maps = new Dictionary<string, IDictionary<long, string>>();
                            maps[name] = map;
                            break;
                        case "resources":
                            {
                                if (!alreadyReadMyCulture)
                                {
                                    string desiredCulture = System.Globalization.CultureInfo.CurrentCulture.Name;
                                    if (cultureBeingRead != null && string.Compare(cultureBeingRead, desiredCulture, StringComparison.OrdinalIgnoreCase) == 0)
                                        alreadyReadMyCulture = true;
                                    cultureBeingRead = reader.GetAttribute("culture");
                                }
                            } break;
                        case "string":
                            if (!alreadyReadMyCulture)
                                strings[reader.GetAttribute("id")] = reader.GetAttribute("value");
                            break;
                    }
                }

                // localize strings for maps.
                if (maps != null)
                {
                    foreach (IDictionary<long, string> amap in maps.Values)
                    {
                        foreach (var keyValue in new List<KeyValuePair<long, string>>(amap))
                        {
                            Match m = Regex.Match(keyValue.Value, @"^\$\(string\.(.*)\)$");
                            if (m.Success)
                            {
                                string newValue;
                                if (strings.TryGetValue(m.Groups[1].Value, out newValue))
                                    amap[keyValue.Key] = newValue;
                            }
                        }
                    }
                }

                // Register all the events
                foreach (var eventInfo in events)
                {
                    var event_ = eventInfo.eventTemplate;
                    // Set the template if there is any. 
                    if (eventInfo.templateName != null)
                    {
                        var templateInfo = templates[eventInfo.templateName];
                        event_.payloadNames = templateInfo.payloadNames.ToArray();
                        event_.payloadFetches = templateInfo.payloadFetches.ToArray();
                    }
                    else
                    {
                        event_.payloadNames = new string[0];
                        event_.payloadFetches = new DynamicTraceEventData.PayloadFetch[0];
                    }

                    // before registering, localize any message format strings.  
                    string message = event_.MessageFormat;
                    if (message != null)
                    {
                        // Expect $(STRINGNAME) where STRINGNAME needs to be looked up in the string table
                        // TODO currently we just ignore messages without a valid string name.  Is that OK?
                        event_.MessageFormat = null;
                        Match m = Regex.Match(message, @"^\$\(string\.(.*)\)$");
                        if (m.Success)
                            strings.TryGetValue(m.Groups[1].Value, out event_.MessageFormat);
                    }

                    //SLAB update try/catch section
                    try
                    {
                        source.RegisterEventTemplate(event_);
                    }
                    catch (NullReferenceException e)
                    {
                        // This error may be thrown when disposing source. 
                        Error = e;
                        inited = false;     // If we call it again, start over from the begining.  
                        return;
                    }
                }

                //SLAB comment:
                // The below code was commented out to allow receiving manifest updates.  
                // Note that this registration will set the manifest event as handled and will always send
                // the initial manifest (no updates) so this behavior will be useless for manifest caching scenarios
                // where stale manifest should be avoided by receiving new updates.
                // To recap, avoiding manifest event registration will force to get unhandled events on each new manifest event.

                //// Create an event for the manifest event itself so it looks pretty in dumps.  
                //source.RegisterEventTemplate(new DynamicManifestTraceEventData(callback, this));
            }
            catch (Exception e)
            {
                // TODO FIX NOW, log this!
                Debug.Assert(false, "Exception during manifest parsing");
#if DEBUG
                Console.WriteLine("Error: Exception during processing of in-log manifest for provider {0}.  Symbolic information may not be complete.", Name);
#endif
                Error = e;
            }

            inited = false;     // If we call it again, start over from the begining.  
        }

        private class EventInfo
        {
            public EventInfo(DynamicTraceEventData eventTemplate, string templateName)
            {
                this.eventTemplate = eventTemplate;
                this.templateName = templateName;
            }
            public DynamicTraceEventData eventTemplate;
            public string templateName;
        };

        private class TaskInfo
        {
            public int id;
            public Guid guid;
        };

        private class TemplateInfo
        {
            public List<string> payloadNames;
            public List<DynamicTraceEventData.PayloadFetch> payloadFetches;
        };

        private TemplateInfo ComputeFieldInfo(XmlReader reader, Dictionary<string, IDictionary<long, string>> maps)
        {
            var ret = new TemplateInfo();

            ret.payloadNames = new List<string>();
            ret.payloadFetches = new List<DynamicTraceEventData.PayloadFetch>();
            ushort offset = 0;
            while (reader.Read())
            {
                if (reader.Name == "data")
                {
                    string inType = reader.GetAttribute("inType");
                    Type type = GetTypeForManifestTypeName(inType);
                    ushort size = DynamicTraceEventData.SizeOfType(type);
                    // Strings are weird in that they are encoded multiple ways.  
                    if (type == typeof(string) && inType == "win:AnsiString")
                        size = DynamicTraceEventData.NULL_TERMINATED | DynamicTraceEventData.IS_ANSI;

                    ret.payloadNames.Add(reader.GetAttribute("name"));
                    IDictionary<long, string> map = null;
                    string mapName = reader.GetAttribute("map");
                    if (mapName != null && maps != null)
                        maps.TryGetValue(mapName, out map);
                    ret.payloadFetches.Add(new DynamicTraceEventData.PayloadFetch(offset, size, type, map));
                    if (offset != ushort.MaxValue)
                    {
                        Debug.Assert(size != 0);
                        if (size < DynamicTraceEventData.SPECIAL_SIZES)
                            offset += size;
                        else
                            offset = ushort.MaxValue;
                    }
                }
            }
            return ret;
        }

        private static Type GetTypeForManifestTypeName(string manifestTypeName)
        {
            switch (manifestTypeName)
            {
                // TODO do we want to support unsigned?
                case "win:Pointer":
                case "trace:SizeT":
                    return typeof(IntPtr);
                case "win:Boolean":
                    return typeof(bool);
                case "win:UInt8":
                case "win:Int8":
                    return typeof(byte);
                case "win:UInt16":
                case "win:Int16":
                case "trace:Port":
                    return typeof(short);
                case "win:UInt32":
                case "win:Int32":
                case "trace:IPAddr":
                case "trace:IPAddrV4":
                    return typeof(int);
                case "trace:WmiTime":
                case "win:UInt64":
                case "win:Int64":
                    return typeof(long);
                case "win:Double":
                    return typeof(double);
                case "win:Float":
                    return typeof(float);
                case "win:AnsiString":
                case "win:UnicodeString":
                    return typeof(string);
                case "win:GUID":
                    return typeof(Guid);
                case "win:FILETIME":
                    return typeof(DateTime);
                default:
                    throw new Exception("Unsupported type " + manifestTypeName);
            }
        }

        #region IFastSerializable Members

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(majorVersion);
            serializer.Write(minorVersion);
            serializer.Write((int)format);
            int count = 0;
            if (serializedManifest != null)
                count = serializedManifest.Length;
            serializer.Write(count);
            for (int i = 0; i < count; i++)
                serializer.Write(serializedManifest[i]);
        }

        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            deserializer.Read(out majorVersion);
            deserializer.Read(out minorVersion);
            format = (ManifestEnvelope.ManifestFormats)deserializer.ReadInt();
            int count = deserializer.ReadInt();
            serializedManifest = new byte[count];
            for (int i = 0; i < count; i++)
                serializedManifest[i] = deserializer.ReadByte();
            Init();
        }

        private void Init()
        {
            try
            {
                reader = ManifestReader;
                while (reader.Read())
                {
                    if (reader.Name == "provider")
                    {
                        guid = new Guid(reader.GetAttribute("guid"));
                        name = reader.GetAttribute("name");
                        fileName = reader.GetAttribute("resourceFileName");
                        break;
                    }
                }

                if (name == null)
                    throw new Exception("No provider element found in manifest");
            }
            catch (Exception e)
            {
                Debug.Assert(false, "Exception during manifest parsing");
                name = "";
                Error = e;
            }
            inited = true;
        }

        #endregion
        private XmlReader reader;
        private byte[] serializedManifest;
        private byte majorVersion;
        private byte minorVersion;
        ManifestEnvelope.ManifestFormats format;
        private Guid guid;
        private string name;
        private string fileName;
        private bool inited;

        //SLAB update
        //exposed error to be consumed from caller
        public Exception Error { get; private set; }

        #endregion
    }

    #region internal classes
    /// <summary>
    /// DynamicTraceEventData is an event that knows how to take runtime information to parse event fields (and payload)
    /// 
    /// This meta-data is distilled down to a array of field names and an array of PayloadFetches which contain enough
    /// information to find the field data in the payload blob.   This meta-data is used in the 
    /// code:DynamicTraceEventData.PayloadNames and code:DynamicTraceEventData.PayloadValue methods.  
    /// </summary>
    public class DynamicTraceEventData : TraceEvent, IFastSerializable
    {
        internal DynamicTraceEventData(Action<TraceEvent> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            Action = action;
        }

        internal protected event Action<TraceEvent> Action;
        protected internal override void Dispatch()
        {
            if (Action != null)
            {
                Action(this);
            }
        }
        public override string[] PayloadNames
        {
            get { Debug.Assert(payloadNames != null); return payloadNames; }
        }
        public override object PayloadValue(int index)
        {
            Type type = payloadFetches[index].type;
            if (type == null)
                return "[CANT PARSE]";
            int offset = payloadFetches[index].offset;
            if (offset == ushort.MaxValue)
                offset = SkipToField(index);

            if ((uint)offset >= 0x10000)
                return "[CANT PARSE OFFSET]";
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.String:
                    {
                        var size = payloadFetches[index].size;
                        var isAnsi = false;
                        if (size >= SPECIAL_SIZES)
                        {
                            isAnsi = ((size & IS_ANSI) != 0);

                            var format = (size & ~IS_ANSI);
                            if (format == NULL_TERMINATED)
                            {
                                if ((size & IS_ANSI) != 0)
                                    return GetUTF8StringAt(offset);
                                else
                                    return GetUnicodeStringAt(offset);
                            }
                            else if (format == DynamicTraceEventData.COUNT32_PRECEEDS)
                                size = (ushort)GetInt32At(offset - 4);
                            else if (format == DynamicTraceEventData.COUNT16_PRECEEDS)
                                size = (ushort)GetInt16At(offset - 2);
                            else
                                return "[CANT PARSE STRING]";
                        }
                        else if (size > 0x8000)
                        {
                            size -= 0x8000;
                            isAnsi = true;
                        }
                        if (isAnsi)
                            return GetFixedAnsiStringAt(size, offset);
                        else
                            return GetFixedUnicodeStringAt(size / 2, offset);
                    }
                case TypeCode.Boolean:
                    return GetByteAt(offset) != 0;
                case TypeCode.Byte:
                    return (byte)GetByteAt(offset);
                case TypeCode.SByte:
                    return (SByte)GetByteAt(offset);
                case TypeCode.Int16:
                    return GetInt16At(offset);
                case TypeCode.UInt16:
                    return (UInt16)GetInt16At(offset);
                case TypeCode.Int32:
                    return GetInt32At(offset);
                case TypeCode.UInt32:
                    return (UInt32)GetInt32At(offset);
                case TypeCode.Int64:
                    return GetInt64At(offset);
                case TypeCode.UInt64:
                    return (UInt64)GetInt64At(offset);
                case TypeCode.Single:
                    return GetSingleAt(offset);
                case TypeCode.Double:
                    return GetDoubleAt(offset);
                default:
                    if (type == typeof(IntPtr))
                    {
                        if (PointerSize == 4)
                            return (UInt32)GetInt32At(offset);
                        else
                            return (UInt64)GetInt64At(offset);
                    }
                    else if (type == typeof(Guid))
                        return GetGuidAt(offset);
                    else if (type == typeof(DateTime))
                        return DateTime.FromFileTime(GetInt64At(offset));
                    else if (type == typeof(byte[]))
                    {
                        int size = payloadFetches[index].size;
                        if (size >= DynamicTraceEventData.SPECIAL_SIZES)
                        {
                            if (payloadFetches[index].size == DynamicTraceEventData.COUNT32_PRECEEDS)
                                size = GetInt32At(offset - 4);
                            else if (payloadFetches[index].size == DynamicTraceEventData.COUNT16_PRECEEDS)
                                size = (ushort)GetInt16At(offset - 2);
                            else
                                return "[CANT PARSE]";
                        }
                        var ret = new byte[size];
                        EventData(ret, 0, offset, ret.Length);
                        return ret;
                    }

                    return "[UNSUPPORTED TYPE]";
            }
        }
        public override string PayloadString(int index)
        {
            object value = PayloadValue(index);
            var map = payloadFetches[index].map;
            string ret = null;
            long asLong;

            if (map != null)
            {
                asLong = (long)((IConvertible)value).ToInt64(null);
                if (map is SortedList<long, string>)
                {
                    StringBuilder sb = new StringBuilder();
                    // It is a bitmap, compute the bits from the bitmap.  
                    foreach (var keyValue in map)
                    {
                        if (asLong == 0)
                            break;
                        if ((keyValue.Key & asLong) != 0)
                        {
                            if (sb.Length != 0)
                                sb.Append('|');
                            sb.Append(keyValue.Value);
                            asLong &= ~keyValue.Key;
                        }
                    }
                    if (asLong != 0)
                    {
                        if (sb.Length != 0)
                            sb.Append('|');
                        sb.Append(asLong);
                    }
                    else if (sb.Length == 0)
                        sb.Append('0');
                    ret = sb.ToString();
                }
                else
                {
                    // It is a value map, just look up the value
                    map.TryGetValue(asLong, out ret);
                }
            }

            if (ret != null)
                return ret;

            // Print large long values as hex by default.   
            if (value is long)
            {
                asLong = (long)value;
                goto PrintLongAsHex;
            }
            else if (value is ulong)
            {
                asLong = (long)(ulong)value;
                goto PrintLongAsHex;
            }
            var asByteArray = value as byte[];
            if (asByteArray != null)
            {
                StringBuilder sb = new StringBuilder();
                var limit = Math.Min(asByteArray.Length, 16);
                for (int i = 0; i < limit; i++)
                {
                    var b = asByteArray[i];
                    sb.Append(HexDigit((b / 16)));
                    sb.Append(HexDigit((b % 16)));
                }
                if (limit < asByteArray.Length)
                    sb.Append("...");

                return sb.ToString();
            }
            return value.ToString();
        PrintLongAsHex:
            if ((int)asLong != asLong)
                return "0x" + asLong.ToString("x");
            return asLong.ToString();
        }

        private static char HexDigit(int digit)
        {
            if (digit < 10)
                return (char)('0' + digit);
            else
                return (char)('A' - 10 + digit);
        }
        public override string FormattedMessage
        {
            get
            {
                if (MessageFormat == null)
                    return null;

                // TODO is this error handling OK?  
                // Replace all %N with the string value for that parameter.  
                return Regex.Replace(MessageFormat, @"%(\d+)", delegate(Match m)
                {
                    int index = int.Parse(m.Groups[1].Value) - 1;
                    if ((uint)index < (uint)PayloadNames.Length)
                        return PayloadString(index);
                    else
                        return "<<Out Of Range>>";
                });
            }
        }

        #region private
        private int SkipToField(int index)
        {
            // Find the first field that has a fixed offset. 
            int offset = 0;
            int cur = index;
            while (0 < cur)
            {
                --cur;
                offset = payloadFetches[cur].offset;
                if (offset != ushort.MaxValue)
                    break;
            }

            // TODO it probably does pay to remember the offsets in a particular instance, since otherwise the
            // algorithm is N*N
            while (cur < index)
            {
                ushort size = payloadFetches[cur].size;
                if (size >= SPECIAL_SIZES)
                {
                    if (size == NULL_TERMINATED)
                        offset = SkipUnicodeString(offset);
                    else if (size == (NULL_TERMINATED | IS_ANSI))
                        offset = SkipUTF8String(offset);
                    else if (size == POINTER_SIZE)
                        offset += PointerSize;
                    else if ((size & ~IS_ANSI) == COUNT32_PRECEEDS)
                        offset += GetInt32At(offset - 4);
                    else if ((size & ~IS_ANSI) == COUNT16_PRECEEDS)
                        offset += (ushort)GetInt16At(offset - 2);
                    else
                        return -1;
                }
                else
                    offset += size;
                cur++;
            }
            return offset;
        }
        internal static ushort SizeOfType(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.String:
                    return NULL_TERMINATED;
                case TypeCode.SByte:
                case TypeCode.Byte:
                    return 1;
                case TypeCode.UInt16:
                case TypeCode.Int16:
                    return 2;
                case TypeCode.UInt32:
                case TypeCode.Int32:
                case TypeCode.Boolean:      // We follow windows conventions and use 4 bytes for bool.  
                case TypeCode.Single:
                    return 4;
                case TypeCode.UInt64:
                case TypeCode.Int64:
                case TypeCode.Double:
                case TypeCode.DateTime:
                    return 8;
                default:
                    if (type == typeof(Guid))
                        return 16;
                    if (type == typeof(IntPtr))
                        return POINTER_SIZE;
                    throw new Exception("Unsupported type " + type.Name); // TODO 
            }
        }

        internal const ushort IS_ANSI = 1;                              // A bit mask that represents the string is ASCII 
        // NULL_TERMINATD | IS_ANSI == MaxValue 
        internal const ushort NULL_TERMINATED = ushort.MaxValue - 1;
        // COUNT32_PRECEEDS | IS_ANSI == MaxValue - 2
        internal const ushort COUNT32_PRECEEDS = ushort.MaxValue - 3;   // Count is a 32 bit int directly before
        // COUNT16_PRECEEDS | IS_ANSI == MaxValue -4
        internal const ushort COUNT16_PRECEEDS = ushort.MaxValue - 5;   // Count is a 16 bit uint directly before

        internal const ushort POINTER_SIZE = ushort.MaxValue - 14;      // It is the pointer size of the target machine. 
        internal const ushort UNKNOWN_SIZE = ushort.MaxValue - 15;      // Generic unknown.
        internal const ushort SPECIAL_SIZES = ushort.MaxValue - 15;     // Some room for growth.  

        internal struct PayloadFetch
        {
            public PayloadFetch(ushort offset, ushort size, Type type, IDictionary<long, string> map = null)
            {
                this.offset = offset;
                this.size = size;
                this.type = type;
                this.map = map;
            }

            public ushort offset;       // offset == MaxValue means variable size.  
            // TODO come up with a real encoding for variable sized things
            public ushort size;         // See special encodeings above (also size > 0x8000 means fixed lenght ANSI).  
            public IDictionary<long, string> map;
            public Type type;
        };

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write((int)eventID);
            serializer.Write((int)task);
            serializer.Write(taskName);
            serializer.Write(taskGuid);
            serializer.Write((int)opcode);
            serializer.Write(opcodeName);
            serializer.Write(providerGuid);
            serializer.Write(providerName);
            serializer.Write(MessageFormat);
            serializer.Write(lookupAsWPP);

            serializer.Write(payloadNames.Length);
            foreach (var payloadName in payloadNames)
                serializer.Write(payloadName);

            serializer.Write(payloadFetches.Length);
            foreach (var payloadFetch in payloadFetches)
            {
                serializer.Write((short)payloadFetch.offset);
                serializer.Write((short)payloadFetch.size);
                if (payloadFetch.type == null)
                    serializer.Write((string)null);
                else
                    serializer.Write(payloadFetch.type.FullName);
                serializer.Write((IFastSerializable)null);     // This is for the map (eventually)
            }
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            eventID = (TraceEventID)deserializer.ReadInt();
            task = (TraceEventTask)deserializer.ReadInt();
            deserializer.Read(out taskName);
            deserializer.Read(out taskGuid);
            opcode = (TraceEventOpcode)deserializer.ReadInt();
            deserializer.Read(out opcodeName);
            deserializer.Read(out providerGuid);
            deserializer.Read(out providerName);
            deserializer.Read(out MessageFormat);
            deserializer.Read(out lookupAsWPP);
            int count;
            deserializer.Read(out count);
            payloadNames = new string[count];
            for (int i = 0; i < count; i++)
                deserializer.Read(out payloadNames[i]);
            deserializer.Read(out count);
            payloadFetches = new PayloadFetch[count];
            for (int i = 0; i < count; i++)
            {
                payloadFetches[i].offset = (ushort)deserializer.ReadInt16();
                payloadFetches[i].size = (ushort)deserializer.ReadInt16();
                var typeName = deserializer.ReadString();
                if (typeName != null)
                    payloadFetches[i].type = Type.GetType(typeName);
                IFastSerializable dummy;
                deserializer.Read(out dummy);           // For map when we use it.  
            }
        }

        // Fields
        internal PayloadFetch[] payloadFetches;
        internal string MessageFormat; // This is in ETW conventions (%N)
        #endregion
    }

    /// <summary>
    /// This class is only used to pretty-print the manifest event itself.   It is pretty special purpose
    /// </summary>
    class DynamicManifestTraceEventData : DynamicTraceEventData
    {
        internal DynamicManifestTraceEventData(Action<TraceEvent> action, ProviderManifest manifest)
            : base(action, 0xFFFE, 0, "ManifestData", Guid.Empty, 0, null, manifest.Guid, manifest.Name)
        {
            this.manifest = manifest;
            payloadNames = new string[] { "Format", "MajorVersion", "MinorVersion", "Magic", "TotalChunks", "ChunkNumber" };
            payloadFetches = new PayloadFetch[] {
            new PayloadFetch(0, 1, typeof(byte)),
            new PayloadFetch(1, 1, typeof(byte)),
            new PayloadFetch(2, 1, typeof(byte)),
            new PayloadFetch(3, 1, typeof(byte)),
            new PayloadFetch(4, 2, typeof(ushort)),
            new PayloadFetch(6, 2, typeof(ushort)),
        };
            Action += action;
        }

        public override StringBuilder ToXml(StringBuilder sb)
        {
            int totalChunks = GetInt16At(4);
            int chunkNumber = GetInt16At(6);
            if (chunkNumber + 1 == totalChunks)
            {
                StringBuilder baseSb = new StringBuilder();
                base.ToXml(baseSb);
                sb.AppendLine(XmlUtilities.OpenXmlElement(baseSb.ToString()));
                sb.Append(manifest.Manifest);
                sb.Append("</Event>");
                return sb;
            }
            else
                return base.ToXml(sb);
        }
        #region private
        ProviderManifest manifest;
        #endregion
    }

    /// <summary>
    /// DynamicTraceEventParserState represents the state of a  DynamicTraceEventParser that needs to be
    /// serialied to a log file.  It does NOT include information about what events are chosen but DOES contain
    /// any other necessary information that came from the ETL data file.  
    /// </summary>
    class DynamicTraceEventParserState : IFastSerializable
    {
        public DynamicTraceEventParserState() { providers = new Dictionary<Guid, ProviderManifest>(); }

        internal Dictionary<Guid, ProviderManifest> providers;

        #region IFastSerializable Members

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(providers.Count);
            foreach (ProviderManifest provider in providers.Values)
                serializer.Write(provider);
        }

        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            int count;
            deserializer.Read(out count);
            for (int i = 0; i < count; i++)
            {
                ProviderManifest provider;
                deserializer.Read(out provider);
                providers.Add(provider.Guid, provider);
            }
        }

        #endregion
    }
}
    #endregion
