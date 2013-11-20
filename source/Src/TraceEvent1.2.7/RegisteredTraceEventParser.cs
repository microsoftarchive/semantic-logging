//     Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.RegularExpressions;
using FastSerialization;
using System.Diagnostics.Eventing;

namespace Diagnostics.Tracing.Parsers
{
    /// <summary>
    /// RegisteredTraceEventParser uses the standard windows provider database (what gets registered with wevtutil)
    /// to find the names of events and fields of the events).   
    /// </summary>
    public unsafe sealed class RegisteredTraceEventParser : ExternalTraceEventParser
    {
        public RegisteredTraceEventParser(TraceEventSource source)
            : base(source) { }

        #region private
        protected override DynamicTraceEventData TryLookup(TraceEvent unknownEvent)
        {
            DynamicTraceEventData ret = null;
            // TODO react if 4K is not big enough, cache the buffer?, handle more types, handle structs...
            int buffSize = 4096;
            byte* buffer = (byte*)System.Runtime.InteropServices.Marshal.AllocHGlobal(buffSize);
            int status = TdhGetEventInformation(unknownEvent.eventRecord, 0, null, buffer, &buffSize);
            if (status == 0)
            {
                TRACE_EVENT_INFO* eventInfo = (TRACE_EVENT_INFO*)buffer;
                EVENT_PROPERTY_INFO* propertyInfos = &eventInfo->EventPropertyInfoArray;

                string taskName = null;
                if (eventInfo->TaskNameOffset != 0)
                    taskName = (new string((char*)(&buffer[eventInfo->TaskNameOffset]))).Trim();

                string opcodeName = null;
                if (eventInfo->OpcodeNameOffset != 0)
                {
                    opcodeName = (new string((char*)(&buffer[eventInfo->OpcodeNameOffset]))).Trim();
                    if (opcodeName.StartsWith("win:"))
                        opcodeName = opcodeName.Substring(4);
                }

                string providerName = "UnknownProvider";
                if (eventInfo->ProviderNameOffset != 0)
                    providerName = new string((char*)(&buffer[eventInfo->ProviderNameOffset]));

                var eventID = unknownEvent.ClassicProvider ? TraceEventID.Illegal : unknownEvent.eventID;
                var newTemplate = new DynamicTraceEventData(null, (int)eventID, (int)unknownEvent.task, taskName,
                    unknownEvent.taskGuid, (int)unknownEvent.Opcode, opcodeName, unknownEvent.ProviderGuid, providerName);

                newTemplate.payloadNames = new string[eventInfo->TopLevelPropertyCount];
                newTemplate.payloadFetches = new DynamicTraceEventData.PayloadFetch[eventInfo->TopLevelPropertyCount];
                ushort offset = 0;
                for (int i = 0; i < eventInfo->TopLevelPropertyCount; i++)
                {
                    var propertyInfo = &propertyInfos[i];
                    var propertyName = new string((char*)(&buffer[propertyInfo->NameOffset]));
                    // Remove anything that does not look like an ID (.e.g space)
                    newTemplate.payloadNames[i] = Regex.Replace(propertyName, "[^A-Za-z0-9_]", "");
                    newTemplate.payloadFetches[i].type = GetTypeForTdhInType(propertyInfo->InType);

                    // Determine whether the size variable or not, and set 'size' based on that. 
                    ushort size = DynamicTraceEventData.UNKNOWN_SIZE;
                    // is this dynamically sized with another field specifying the length?
                    if ((propertyInfo->Flags & PROPERTY_FLAGS.ParamLength) != 0)
                    {
                        if (propertyInfo->LengthOrLengthIndex == i - 1)
                        {
                            if (propertyInfos[i - 1].LengthOrLengthIndex == 4)
                                size = DynamicTraceEventData.COUNT32_PRECEEDS;
                            else if (propertyInfos[i - 1].LengthOrLengthIndex == 2)
                                size = DynamicTraceEventData.COUNT16_PRECEEDS;
                            else
                                Trace.WriteLine("WARNING: Unexpected dynamic length, giving up");
                        }

                        if (size != DynamicTraceEventData.UNKNOWN_SIZE && propertyInfo->InType == TdhInputType.AnsiString)
                            size |= DynamicTraceEventData.IS_ANSI;
                    }
                    else
                    {
                        if (propertyInfo->InType == TdhInputType.AnsiString)
                            size = DynamicTraceEventData.NULL_TERMINATED | DynamicTraceEventData.IS_ANSI;
                        else if (propertyInfo->InType == TdhInputType.UnicodeString)
                            size = DynamicTraceEventData.NULL_TERMINATED;
                        else if (propertyInfo->InType == TdhInputType.Pointer)
                            size = DynamicTraceEventData.POINTER_SIZE;
                        else
                        {
                            // No, then it it fixed size (but give up if it is too big)  
                            var fixedSize = propertyInfo->CountOrCountIndex * propertyInfo->LengthOrLengthIndex;
                            if (fixedSize < 0x7FF0)
                            {
                                size = (ushort)fixedSize;
                                if (propertyInfo->InType == TdhInputType.AnsiString)
                                    size += 0x8000;
                            }
                        }
                    }

                    // Currently we give up on any other flags (arrays, structs). 
                    if ((propertyInfo->Flags & ~PROPERTY_FLAGS.ParamLength) != 0)
                        size = DynamicTraceEventData.UNKNOWN_SIZE;

                    newTemplate.payloadFetches[i].size = (ushort)size;
                    newTemplate.payloadFetches[i].offset = offset;
                    if (size >= DynamicTraceEventData.SPECIAL_SIZES)
                        offset = ushort.MaxValue;           // Indicate that the offset must be computed at run time. 
                    else if (offset != ushort.MaxValue)
                    {
                        Debug.Assert(offset + size < ushort.MaxValue);
                        offset += size;
                    }
                }
                ret = newTemplate;      // return this as the event template for this lookup. 
            }

            System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)buffer);
            return ret;
        }

        private static Type GetTypeForTdhInType(TdhInputType tdhInType)
        {
            switch (tdhInType)
            {
                // TODO unsigned case, aslo offsets can overflow.  
                case TdhInputType.UnicodeString:
                case TdhInputType.AnsiString:
                    return typeof(string);
                case TdhInputType.Int8:
                case TdhInputType.UInt8:
                    return typeof(byte);
                case TdhInputType.Int16:
                case TdhInputType.UInt16:
                    return typeof(short);
                case TdhInputType.Int32:
                case TdhInputType.UInt32:
                case TdhInputType.HexInt32:
                    return typeof(int);
                case TdhInputType.Int64:
                case TdhInputType.UInt64:
                case TdhInputType.HexInt64:
                    return typeof(long);
                case TdhInputType.Float:
                    return typeof(float);
                case TdhInputType.Double:
                    return typeof(double);
                case TdhInputType.Boolean:
                    return typeof(bool);
                case TdhInputType.Guid:
                    return typeof(Guid);
                case TdhInputType.Pointer:
                case TdhInputType.SizeT:
                    return typeof(IntPtr);
                case TdhInputType.Binary:
                    return typeof(byte[]);
            }
            return null;
        }

        [DllImport("tdh.dll"), SuppressUnmanagedCodeSecurityAttribute]
        internal static extern int TdhGetEventInformation(
            TraceEventNativeMethods.EVENT_RECORD* pEvent,
            uint TdhContextCount,
            void* pTdhContext,
            byte* pBuffer,
            int* pBufferSize);

        // TODO use or remove 
        [DllImport("tdh.dll"), SuppressUnmanagedCodeSecurityAttribute]
        internal static extern int TdhGetEventMapInformation(
            TraceEventNativeMethods.EVENT_RECORD* pEvent,
            string pMapName,
            EVENT_MAP_INFO* info,
            ref int infoSize
        );

        [StructLayout(LayoutKind.Sequential)]
        internal struct EVENT_MAP_INFO
        {
#if false
            ULONG           NameOffset;
            MAP_FLAGS       Flag;
  ULONG           EntryCount;
  union {
    MAP_VALUETYPE MapEntryValueType;
    ULONG         FormatStringOffset;
  };
  EVENT_MAP_ENTRY MapEntryArray[ANYSIZE_ARRAY];
#endif
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct TRACE_EVENT_INFO
        {
            public Guid ProviderGuid;
            public Guid EventGuid;
            public EventDescriptor EventDescriptor;
            public int DecodingSource;
            public int ProviderNameOffset;
            public int LevelNameOffset;
            public int ChannelNameOffset;
            public int KeywordsNameOffset;
            public int TaskNameOffset;
            public int OpcodeNameOffset;
            public int EventMessageOffset;
            public int ProviderMessageOffset;
            public int BinaryXmlOffset;
            public int BinaryXmlSize;
            public int ActivityIDNameOffset;
            public int RelatedActivityIDNameOffset;
            public int PropertyCount;
            public int TopLevelPropertyCount;
            public int Flags;
            public EVENT_PROPERTY_INFO EventPropertyInfoArray;  // Actually an array, this is the first element.  
        }

        internal struct EVENT_PROPERTY_INFO
        {
            public PROPERTY_FLAGS Flags;
            public int NameOffset;

            // These are valid if Flags & Struct not set. 
            public TdhInputType InType;
            public ushort OutType;             // Really TdhOutputType
            public int MapNameOffset;

            // These are valid if Flags & Struct is set.  
            public int StructStartIndex
            {
                get
                {
                    System.Diagnostics.Debug.Assert((Flags & PROPERTY_FLAGS.Struct) != 0);
                    return (ushort)InType;
                }
            }
            public int NumOfStructMembers
            {
                get
                {
                    System.Diagnostics.Debug.Assert((Flags & PROPERTY_FLAGS.Struct) != 0);
                    return (ushort)OutType;
                }
            }

            // Normally Count is 1 (thus every field in an array, it is just htat most array have fixed size of 1)
            public ushort CountOrCountIndex;                // Flags & ParamFixedLength determines if it count, otherwise countIndex 
            // Normally Length is the size of InType (thus is fixed), but can be variable for blobs.
            public ushort LengthOrLengthIndex;              // Flags & ParamLength determines if it lengthIndex otherwise it is the length InType
            public int Reserved;
        }

        [Flags]
        internal enum PROPERTY_FLAGS
        {
            None = 0,
            Struct = 0x1,
            ParamLength = 0x2,
            ParamCount = 0x4,
            WbemXmlFragment = 0x8,
            ParamFixedLength = 0x10
        }

        public enum TdhInputType : ushort
        {
            Null,
            UnicodeString,
            AnsiString,
            Int8,
            UInt8,
            Int16,
            UInt16,
            Int32,
            UInt32,
            Int64,
            UInt64,
            Float,
            Double,
            Boolean,
            Binary,
            Guid,
            Pointer,
            FileTime,
            SystemTime,
            SID,
            HexInt32,
            HexInt64,  // End of winmeta intypes
            CountedString = 300, // Start of TDH intypes for WBEM
            CountedAnsiString,
            ReversedCountedString,
            ReversedCountedAnsiString,
            NonNullTerminatedString,
            NonNullTerminatedAnsiString,
            UnicodeChar,
            AnsiChar,
            SizeT,
            HexDump,
            WbemSID
        };
        #endregion
    }

    /// <summary>
    /// ExternalTraceEventParser is an abstract class that acts as a parser for any 'External' resolution
    /// This include the TDH (RegisteredTraceEventParser) as well as the WPPTraceEventParser.   
    /// </summary>
    public abstract unsafe class ExternalTraceEventParser : TraceEventParser
    {
        public ExternalTraceEventParser(TraceEventSource source)
            : base(source)
        {
            if (source == null)
                return;

            m_state = (ExternalTraceEventParserState)StateObject;
            if (m_state == null)
            {
                StateObject = m_state = new ExternalTraceEventParserState();
                m_state.m_templates = new List<DynamicTraceEventData>();
                this.source.RegisterUnhandledEvent(delegate(TraceEvent unknown)
                {
                    var ret = TryLookup(unknown);
                    if (ret != null)
                    {
                        RegisterTemplate(ret);
                        ret.source = unknown.source;
                        ret.eventRecord = unknown.eventRecord;
                        ret.userData = unknown.userData;
                        return ret;
                    }
                    return unknown;
                });
            }
            else if (m_allCallbackCalled)
            {
                foreach (var template in m_state.m_templates)
                {
                    this.source.RegisterEventTemplate(template);
                    template.Action += m_allCallback;
                }
            }
        }

        public override event Action<TraceEvent> All
        {
            add
            {
                if (m_state != null)
                {
                    // TODO FIX NOW, think about how this might call something twice 
                    foreach (var template in m_state.m_templates)
                    {
                        source.RegisterEventTemplate(template);
                        template.Action += value;
                    }
                }
                m_allCallback += value;
                m_allCallbackCalled = true;
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }

        #region private
        /// <summary>
        /// Register 'template so that it is known to this TraceEventParser.  
        /// </summary>
        protected void RegisterTemplate(DynamicTraceEventData template)
        {
            this.source.RegisterEventTemplate(template);
            if (m_allCallbackCalled)
                template.Action += m_allCallback;
            m_state.m_templates.Add(template);
        }

        protected abstract DynamicTraceEventData TryLookup(TraceEvent unknownEvent);

        ExternalTraceEventParserState m_state;
        Action<TraceEvent> m_allCallback;
        bool m_allCallbackCalled;
        #endregion
    }


    #region internal classes
    /// <summary>
    /// TDHDynamicTraceEventParserState represents the state of a  TDHDynamicTraceEventParser that needs to be
    /// serialied to a log file.  It does NOT include information about what events are chosen but DOES contain
    /// any other necessary information that came from the ETL data file or the OS TDH APIs.  
    /// </summary>
    public class ExternalTraceEventParserState : IFastSerializable
    {
        public ExternalTraceEventParserState() { }

        internal List<DynamicTraceEventData> m_templates;

        #region IFastSerializable Members

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(m_templates.Count);
            foreach (var template in m_templates)
                serializer.Write(template);
        }

        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            int count;
            deserializer.Read(out count);
            m_templates = new List<DynamicTraceEventData>(count);
            for (int i = 0; i < count; i++)
            {
                DynamicTraceEventData template;
                deserializer.Read(out template);
                m_templates.Add(template);
            }
        }
        #endregion
    }

    #endregion
}