//     Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Diagnostics.Tracing;
using Diagnostics.Tracing.Parsers;
using Address = System.UInt64;

namespace Diagnostics.Tracing.Parsers
{
    /// <summary>
    /// Kernel traces have information about images that are loaded, however they don't have enough information
    /// in the events themselves to unambigously look up PDBs without looking at the data inside the images.
    /// This means that symbols can't be resolved unless you are on the same machine on which you gathered the data.
    /// 
    /// XPERF solves this problem by adding new 'synthetic' events that it creates by looking at the trace and then
    /// opening each DLL mentioned and extracting the information needed to look PDBS up on a symbol server (this 
    /// includes the PE file's TimeDateStamp as well as a PDB Guid, and 'pdbAge' that can be found in the DLLs header.
    /// 
    /// These new events are added when XPERF runs the 'merge' command (or -d flag is passed).  It is also exposed 
    /// through the KernelTraceControl.dll!CreateMergedTraceFile API.   
    /// 
    /// SymbolTraceEventParser is a parser for extra events.   
    /// </summary>
    public sealed class SymbolTraceEventParser : TraceEventParser
    {
        public static string ProviderName = "KernelTraceControl";
        public static Guid ProviderGuid = new Guid(0x28ad2447, 0x105b, 0x4fe2, 0x95, 0x99, 0xe5, 0x9b, 0x2a, 0xa9, 0xa6, 0x34);

        public SymbolTraceEventParser(TraceEventSource source)
            : base(source)
        {
        }

        /// <summary>
        ///  The DbgIDRSDS event is added by XPERF for every Image load.  It contains the 'PDB signature' for the DLL, 
        ///  which is enough to unambigously look the image's PDB up on a symbol server.  
        /// </summary>
        public event Action<DbgIDRSDSTraceData> DbgIDRSDS
        {
            add
            {
                source.RegisterEventTemplate(new DbgIDRSDSTraceData(value, 0xFFFF, 0, "ImageId", ImageIDTaskGuid, DBGID_LOG_TYPE_RSDS, "DbgID/RSDS", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new NotImplementedException();
            }
        }
        /// <summary>
        /// Every DLL has a Timestamp in the PE file itself that indicates when it is built.  This event dumps this timestamp.
        /// This timestamp is used to be as the 'signature' of the image and is used as a key to find the symbols, however 
        /// this has mostly be superseeded by the DbgID/RSDS event. 
        /// </summary>
        public event Action<ImageIDTraceData> ImageID
        {
            add
            {
                source.RegisterEventTemplate(new ImageIDTraceData(value, 0xFFFF, 0, "ImageId", ImageIDTaskGuid, DBGID_LOG_TYPE_IMAGEID, "Info", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new NotImplementedException();
            }

        }
        /// <summary>
        /// The FileVersion event contains information from the file version resource that most DLLs have that indicated
        /// detailed information about the exact version of the DLL.  (What is in the File->Properties->Version property
        /// page)
        /// </summary>
        public event Action<FileVersionTraceData> FileVersion
        {
            add
            {
                source.RegisterEventTemplate(new FileVersionTraceData(value, 0xFFFF, 0, "ImageId", ImageIDTaskGuid, DBGID_LOG_TYPE_FILEVERSION, "FileVersion", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new NotImplementedException();
            }

        }
        /// <summary>
        /// I don't really care about this one, but I need a defintion in order to exclude it because it
        /// has the same timestamp as a imageLoad event, and two events with the same timestamp confuse the 
        /// assoication between a stack and the event for the stack.  
        /// </summary>
        public event Action<EmptyTraceData> None
        {
            add
            {
                source.RegisterEventTemplate(new EmptyTraceData(value, 0xFFFF, 0, "ImageId", ImageIDTaskGuid, DBGID_LOG_TYPE_NONE, "None", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new NotImplementedException();
            }
        }

        // These are here just so we don't have any unknown providers in a typical PerfView trace
        // They are incomplete (I don't describe the payload properly).  
        public event Action<EmptyTraceData> WinSatWinSPR
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, 0xFFFF, 0, "WinSat", WinSatTaskGuid, 33, "WinSPR", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new NotImplementedException();
            }
        }
        public event Action<EmptyTraceData> WinSatMetrics
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, 0xFFFF, 0, "WinSat", WinSatTaskGuid, 35, "Metrics", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new NotImplementedException();
            }
        }
        public event Action<EmptyTraceData> WinSatSystemConfig
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, 0xFFFF, 0, "WinSat", WinSatTaskGuid, 37, "SystemConfig", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new NotImplementedException();
            }
        }
        // I don't know much about this event at all...
        public event Action<EmptyTraceData> MetaDataOpcode32
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, 0xFFFF, 0, "MetaData", MetaDataGuid, 32, "Opcode(32)", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new NotImplementedException();
            }
        }
        // I don't know much about this event at all...
        public event Action<EmptyTraceData> MetaData1Opcode33
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, 0xFFFF, 0, "MetaData1", MetaData1Guid, 33, "Opcode(33)", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new NotImplementedException();
            }
        }
        // I don't know much about this event at all...
        public event Action<EmptyTraceData> MetaData2Opcode37
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, 0xFFFF, 0, "MetaData2", MetaData2Guid, 37, "Opcode(37)", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new NotImplementedException();
            }
        }

        #region Private
        // These are the Opcode numbers for various events
        public const int DBGID_LOG_TYPE_IMAGEID = 0x00;
        public const int DBGID_LOG_TYPE_NONE = 0x20;
        public const int DBGID_LOG_TYPE_RSDS = 0x24;
        public const int DBGID_LOG_TYPE_FILEVERSION = 0x40;

        // Used to log meta-data about crimson events into the log.  
        internal static Guid MetadataTaskGuid = new Guid(unchecked((int) 0xBBCCF6C1), 0x6CD1, 0x48c4, 0x80, 0xFF, 0x83, 0x94, 0x82, 0xE3, 0x76, 0x71);
        internal static Guid ImageIDTaskGuid = new Guid(unchecked((int) 0xB3E675D7), 0x2554, 0x4f18, 0x83, 0x0B, 0x27, 0x62, 0x73, 0x25, 0x60, 0xDE);
        internal static Guid WinSatTaskGuid = new Guid(unchecked((int) 0xed54dff8), unchecked((short) 0xc409), 0x4cf6, 0xbf, 0x83, 0x05, 0xe1, 0xe6, 0x1a, 0x09, 0xc4);
        internal static Guid MetaDataGuid = new Guid(unchecked((int) 0xbbccf6c1), 0x6cd1, 0x48c4, 0x80, 0xff, 0x83, 0x94, 0x82, 0xe3, 0x76, 0x71);
        internal static Guid MetaData1Guid = new Guid(unchecked((int)0xbf6ef1cb), unchecked((short)0x89b5), 0x490, 0x80, 0xac, 0xb1, 0x80, 0xcf, 0xbc, 0xff, 0x0f);
        internal static Guid MetaData2Guid = new Guid(unchecked((int)0xb3e675d7), 0x2554, 0x4f18, 0x83, 0x0b, 0x27, 0x62, 0x73, 0x25, 0x60, 0xde);
        #endregion 
    }

    public sealed class FileVersionTraceData : TraceEvent
    {   
        public int ImageSize { get { return GetInt32At(0); } }
        public int TimeDateStamp { get { return GetInt32At(4); } }
        public DateTime BuildTime { get { return PEFile.PEHeader.TimeDateStampToDate(TimeDateStamp); } } 
        public string OrigFileName { get { return GetUnicodeStringAt(8); } } 
        public string FileDescription { get { return GetUnicodeStringAt(SkipUnicodeString(8, 1)); } } 
        public string FileVersion { get { return GetUnicodeStringAt(SkipUnicodeString(8, 2)); } } 
        public string BinFileVersion { get { return GetUnicodeStringAt(SkipUnicodeString(8, 3)); } } 
        public string VerLanguage { get { return GetUnicodeStringAt(SkipUnicodeString(8, 4)); }} 
        public string ProductName { get { return GetUnicodeStringAt(SkipUnicodeString(8, 5)); }} 
        public string CompanyName { get { return GetUnicodeStringAt(SkipUnicodeString(8, 6)); }} 
        public string ProductVersion { get { return GetUnicodeStringAt(SkipUnicodeString(8, 7)); }} 
        public string FileId { get { return GetUnicodeStringAt(SkipUnicodeString(8, 8)); }} 
        public string ProgramId { get { return GetUnicodeStringAt(SkipUnicodeString(8, 9)); }} 

        #region Private
        internal FileVersionTraceData(Action<FileVersionTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opCode, string opCodeName, Guid providerGuid, string providerName) :
            base(eventID, task, taskName, taskGuid, opCode, opCodeName, providerGuid, providerName)
        {
            this.action = action;
        }

        protected internal override void Dispatch()
        {
            action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(EventDataLength == SkipUnicodeString(8, 10));
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                {
                    payloadNames = new string[] { "ImageSize", "TimeDateStamp", "BuildTime", "OrigFileName", "FileDescription", "FileVersion",
                        "BinFileVersion", "VerLanguage", "ProductName", "CompanyName", "ProductVersion", "FileId", "ProgramId" };
                }
                    return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ImageSize;
                case 1:
                    return TimeDateStamp;
                case 2:
                    return BuildTime;
                case 3:
                    return OrigFileName;
                case 4:
                    return FileDescription;
                case 5:
                    return FileVersion;
                case 6:
                    return BinFileVersion;
                case 7:
                    return VerLanguage;
                case 8:
                    return ProductName;
                case 9:
                    return CompanyName;
                case 10:
                    return ProductVersion;
                case 11:
                    return FileId;
                case 12:
                    return ProgramId;
                default:
                    Debug.Assert(false, "invalid index");
                    return null;
            }
        }

        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("ImageSize", ImageSize);
            sb.XmlAttribHex("TimeDateStamp", TimeDateStamp);
            sb.XmlAttrib("OrigFileName", OrigFileName);
            sb.XmlAttrib("FileDescription", FileDescription);
            sb.XmlAttrib("FileVersion", FileVersion);
            sb.XmlAttrib("BinFileVersion", BinFileVersion);
            sb.XmlAttrib("VerLanguage", VerLanguage);
            sb.XmlAttrib("ProductName", ProductName);
            sb.XmlAttrib("CompanyName", CompanyName);
            sb.XmlAttrib("ProductVersion", ProductVersion);
            sb.XmlAttrib("FileId", FileId);
            sb.XmlAttrib("ProgramId", ProgramId);
            sb.Append("/>");
            return sb;
        }
        private Action<FileVersionTraceData> action;
        #endregion
    }
    public sealed class DbgIDRSDSTraceData : TraceEvent
    {
        public Address ImageBase { get { return GetHostPointer(0); } }
        // public int ProcessID { get { return GetInt32At(HostOffset(4, 1)); } }    // This seems to be redundant with the ProcessID in the event header
        public Guid GuidSig { get { return GetGuidAt(HostOffset(8, 1)); } }
        public int Age { get { return GetInt32At(HostOffset(24, 1)); } }
        public string PdbFileName { get { return GetUTF8StringAt(HostOffset(28, 1)); } }

        #region Private
        internal DbgIDRSDSTraceData(Action<DbgIDRSDSTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opCode, string opCodeName, Guid providerGuid, string providerName) :
            base(eventID, task, taskName, taskGuid, opCode, opCodeName, providerGuid, providerName)
        {
            this.action = action;
        }

        protected internal override void Dispatch()
        {
            action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(EventDataLength == SkipUTF8String(HostOffset(32, 1)));
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                {
                    payloadNames = new string[] { "ImageBase", "GuidSig", "Age", "PDBFileName" };
                }
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ImageBase;
                case 1:
                    return GuidSig;
                case 2:
                    return Age;
                case 3:
                    return PdbFileName;
                default:
                    Debug.Assert(false, "invalid index");
                    return null;
            }
        }

        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("ImageBase", ImageBase);
            sb.XmlAttrib("GuidSig", GuidSig);
            sb.XmlAttrib("Age", Age);
            sb.XmlAttrib("PdbFileName", PdbFileName);
            sb.Append("/>");
            return sb;
        }
        private Action<DbgIDRSDSTraceData> action;
        #endregion
    }
    public sealed class ImageIDTraceData : TraceEvent
    {
        public Address ImageBase { get { return GetHostPointer(0); } }
        public long ImageSize { get { return GetIntPtrAt(HostOffset(4, 1)); } }
        // Seems to always be 0
        // public int ProcessID { get { return GetInt32At(HostOffset(8, 2)); } }
        public int TimeDateStamp { get { return GetInt32At(HostOffset(12, 2)); } }
        public string OriginalFileName { get { return GetUnicodeStringAt(HostOffset(16, 2)); } }

        #region Private
        internal ImageIDTraceData(Action<ImageIDTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opCode, string opCodeName, Guid providerGuid, string providerName):
            base(eventID, task, taskName, taskGuid, opCode, opCodeName, providerGuid, providerName)
        {
            this.Action = action;
        }
        protected internal override void Dispatch()
        {
            Action(this);
        }
        protected internal override void Validate()
        {
            Debug.Assert(EventDataLength == SkipUnicodeString(HostOffset(16, 2)));
        }

        public override string[] PayloadNames
        {
            get {
                if (payloadNames == null)
                {
                    payloadNames = new string[] { "ImageBase", "ImageSize", "ProcessID", "TimeDateStamp", "OriginalFileName" };
                }
                return payloadNames;

            }
        }
        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ImageBase;                    
                case 1:
                    return ImageSize;                    
                case 2:
                    return 0;
                case 3:
                    return TimeDateStamp;
                case 4:
                    return OriginalFileName;
                default:
                    Debug.Assert(false, "bad index value");
                    return null;
            }
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            sb.XmlAttribHex("ImageBase", ImageBase);
            sb.XmlAttribHex("ImageSize", ImageSize);
            sb.XmlAttribHex("TimeDateStamp", TimeDateStamp);
            sb.XmlAttrib("OriginalFileName", OriginalFileName);
            sb.Append("/>");
            return sb;
        }

        private event Action<ImageIDTraceData> Action;        
        #endregion
    }
}
