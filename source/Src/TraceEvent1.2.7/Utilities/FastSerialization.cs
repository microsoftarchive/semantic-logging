//     Copyright (c) Microsoft Corporation.  All rights reserved.
/* This file is best viewed using outline mode (Ctrl-M Ctrl-O) */
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
/* If you uncomment this line, log.serialize.xml and log.deserialize.xml are created, which allow debugging */
// #define DEBUG_SERIALIZE
using System;
using System.Text;      // For StringBuilder.
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using DeferedStreamLabel = FastSerialization.StreamLabel;
using Utilities;

// see code:#Introduction and code:#SerializerIntroduction
namespace FastSerialization
{
    // #Introduction
    // 
    // Sadly, System.Runtime.Serialization has a serious performance flaw. In the scheme created there, the
    // basic contract between an object and the serializer is fundamentally heavy. For serialziation the
    // contract is for the object to implement code:System.Runtime.Serialization.ISerializable.GetObjectData
    // and this should a series of AddValue() APIs on code:System.Runtime.Serialization.SerializationInfo
    // which are given field names and values. The AddValue APIs box the values and place them in a table, It
    // is then the serializers job to actually send out the bits given this table. The REQUIRED work of
    // serializing an integer, s copying 4 bytes to some output buffer (a few instructions), however the
    // protocol above requires 1000s.
    // 
    // The classes in Serialize.cs are an attempt to create really light weight serialization. At the heart
    // of the design are two interfaces code:IStreamReader and code:IStreamWriter. They are a simplified
    // INTERFACE much like code:System.IO.BinaryReader and code:System.IO.BinaryWriter, that know how to
    // write only the most common data types (integers and strings). They also fundamentally understand that
    // they are a stream of bytes, and thus have the concept of code:StreamLabel which is a 'pointer' to a
    // spot in the stream. This is critically important as it allows the serialized form to create a
    // complicated graph of objects in the stream. While code:IStreamWriter does not hav the ability to seek,
    // the code:IStreamReader does (using code:StreamLabel), because it is expected that the reader will want
    // to follow code:StreamLabel 'pointers' to traverse the serialized data in a more random access way.
    // 
    // However, in general, an object needs more than a code:MemoryStreamWriter to serialize itself. When an object
    // graph could have cycles, it needs a way of remembering which objects it has already serialized. It
    // also needs a way encoding types, because in general the type of an object cannot always be inferred
    // from its context. This is the job of the code:Serializer class. A code:Serializer holds all the state
    // needed to represent a partially serialized object graph, but the most important part of a
    // code:Serializer is its code:Serializer.writer property, which holds the logical output stream.
    // 
    // Simmiarly a code:Deserializer holds all the 'in flight' information needed to deserialize a complete
    // object graph, and its most important property is its code:Deserializer.reader that holds the logical
    // input stream.
    // 
    // An object becomes serializable by doing two things
    //     * implemeting the code:IFastSerializable interface and implemeting the
    //         code:IFastSerializable.ToStream and code:IFastSerializable.FromStream methods.
    //     * implemeting a public constructor with no arguments (default constructor). This is needed because
    //         an object needs to be created before code:IFastSerializable.FromStream can be called.
    // 
    // The code:IFastSerializable.ToStream methodIndex that the object implements is passed a code:Serializer, and
    // the object is free to take advantage of all the facilities (like its serialized object table) to help
    // serialize itself, however at its heart, the ToStream methodIndex tends to fetch the code:Serialier.writer
    // and write out the primitive fields in order. Simmilarly at the heart of the
    // code:IFastSerializable.FromStream methodIndex is fetching the code:Deserializer.reader and reading in a
    // series of primitive types.
    // 
    // Now the basic overhead of serializing a object in the common case is
    // 
    //     * A interface call to code:IFastSerializable.ToStream.
    //     * A fetch of code:IStreamWriter from the code:Serialier.writer field
    //     * a series of code:IStreamWriter.Write operations which is an interface call, plus the logic to
    //         store the actual data to the stream (the real work).
    //         
    // This is MUCH leaner, and now dominated by actual work of copying the data to the output buffer.

    /// <summary>
    /// A StreamLabel is a 32 bit integer that represents a position in a code:IStreamReader or
    /// code:IStreamWriter. During writing it is generated by the code:IStreamWriter.GetLabel methodIndex an
    /// consumed by the code:IStreamWriter.WriteLabel methodIndex. On reading you can use
    /// code:IStreamReader.Current and and code:IStreamReader. 
    /// </summary>
    public enum StreamLabel : uint { Invalid = 0xFFFFFFFF };

    /// <summary>
    /// code:IStreamWriter is meant to be a very simple streaming protocol. You can write integral types,
    /// strings, and labels to the stream itself.  
    /// 
    /// code:IStreamWrite can be thought of a simplified code:System.IO.BinaryWriter, or maybe the writer
    /// part of a System.IO.Stream with a few helpers for primitive types.
    /// 
    /// See also code:IStreamReader
    /// </summary>
    public interface IStreamWriter : IDisposable
    {
        void Write(byte value);
        void Write(short value);
        void Write(int value);
        void Write(long value);
        void Write(StreamLabel value);
        void Write(string value);
        // TODO should we have: void Write(byte[] array, int startIndex, int charCount);
        // TODO you could also imagine having void Write(int value1, int value2, int value3, int value4); if interface overhead is significant
        StreamLabel GetLabel();

        void WriteSuffixLabel(StreamLabel value);
    }

    // TODO do we want these?
    public static class IStreamWriterExentions
    {
        public static StreamLabel AddOffset(this IStreamReader reader, StreamLabel baseLabel, int offset)
        {
            return (StreamLabel)((int)baseLabel + offset);
        }

        public static void Write(this IStreamWriter writer, Guid guid)
        {
            byte[] bytes = guid.ToByteArray();
            for (int i = 0; i < bytes.Length; i++)
                writer.Write(bytes[i]);
        }
        public static Guid ReadGuid(this IStreamReader reader)
        {
            byte[] bytes = new byte[16];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = reader.ReadByte();
            return new Guid(bytes);
        }
    }

    /// code:IStreamReader is meant to be a very simple streaming protocol. You can read integral types,
    /// strings, and labels to the stream itself.  You can also goto labels you have read from the stream. 
    /// 
    /// code:IStreamReader can be thought of a simplified code:System.IO.BinaryReder, or maybe the reader
    /// part of a System.IO.Stream with a few helpers for primitive types.
    /// 
    /// See also code:IStreamWriter
    public interface IStreamReader : IDisposable
    {
        byte ReadByte();
        short ReadInt16();
        int ReadInt32();
        long ReadInt64();
        string ReadString();
        StreamLabel ReadLabel();

        void Goto(StreamLabel label);
        StreamLabel Current { get; }

        /// <summary>
        /// Sometimes information is only known after writting the entire stream.  This information can be put
        /// on the end of the stream, but there needs to be a way of finding it relative to the end, rather
        /// than from the begining.   A IStreamReader, however, does not actually let you go 'backwards' easily
        /// because it does not guarentee the size what it writes out (it might compress).  The solution is
        /// the concept of a 'suffixLabel' which is a StreamLabel that can be written as the last entry in
        /// the stream that IStreamReader knows how to read.  This can point at whatever information needs
        /// to go at the end of the stream.  
        /// </summary>
        void GotoSuffixLabel();
    }

    internal enum Tags : byte
    {
        Error,              // To improve debugabilty, 0 is an illegal tag.  
        NullReference,      // Tag for a null object forwardReference. 
        ObjectReference,    // followed by StreamLabel 
        ForwardReference,   // followed by an index (Int32) into the Forward forwardReference array and a Type object
        BeginObject,        // followed by Type object, ToStream data, tagged EndObject
        BeginPrivateObject, // Like beginObject, but not placed in interning table on deserialiation 
        EndObject,          // placed after an object to mark its end (for V2 fields, and debugability). 
        ForwardDefinition,  // followed by a forward forwardReference index and an Object definition (BeginObject)
        // This is used when a a forward forwardReference is actually defined.  

        // In important invarient is that you must always be able to 'skip' to the next object in the
        // serialization stream.  For the first version, this happens naturally, but if you add
        // additional fields in V2, you want V1 readers to be able to skip these extra fields.  We do
        // this by requiring all post V1 fields that are added to be 'tagged' and requiring that objects
        // always end with the 'EndObject' tag.  After a 'FromStream' call, the deserializer will keep
        // reading objects until it finds an unmatched 'EndObject' tag.  Thus even though the V1
        // FromStream call has no knowledge of the extra fields, they are properly skipped.   For this
        // to work all most V1 fields must be tagged (so we know how to skip them even if we don't
        // understand what they are for).  That is what these tags are for.  
        // 
        // ToStream routines are free to encode all fields with tags, which allows a bit more
        // debuggability, because the object's data can be decoded even if the Deserializer is not
        // available.  
        Byte,
        Int16,
        Int32,
        Int64,
        SkipRegion,
        String,
        Limit,              // Just past the last valid tag, used for asserts.  
    }
    public enum ForwardReference : int { Invalid = -1 };

    internal class SerializationType : IFastSerializable
    {
        /// <summary>
        /// This is the version represents the version of both the reading
        /// code and the version for the format for this type in serialized form.  
        /// See code:IFastSerializableVersion for more.  
        /// </summary>
        public int Version { get { return version; } }
        /// <summary>
        /// The version the the smallest (oldest) reader code that can read 
        /// this file format.  Readers strictly less than this are rejected.  
        /// This allows support for forward compatbility.   
        /// See code:IFastSerializableVersion for more.  
        /// </summary>
        public int MinimumReaderVersion { get { return minimumReaderVersion; } }
        public string FullName { get { return fullName; } }
        public IFastSerializable CreateInstance()
        {
            return factory();
        }
        public override string ToString()
        {
            return FullName;
        }

        #region private
        internal SerializationType() { }
        internal SerializationType(Type type)
        {
            this.fullName = type.FullName;
        }
        internal SerializationType(string fullName, Deserializer deserializer)
        {
            this.fullName = fullName;
            factory = deserializer.GetFactory(fullName);
        }
        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(version);
            serializer.Write(minimumReaderVersion);
            serializer.Write(fullName);
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            deserializer.Read(out version);
            deserializer.Read(out minimumReaderVersion);
            deserializer.Read(out fullName);
            factory = deserializer.GetFactory(fullName);
            // This is only here for efficiency (you don't have to cast every instance)
            // since most objects won't be versioned.  However it means that you have to
            // opt into versioning or you will break on old formats.  
            if (minimumReaderVersion > 0)
            {
                IFastSerializableVersion instance = factory() as IFastSerializableVersion;

                // File version must meet minimum version requirements. 
                if (instance != null && !(version >= instance.MinimumVersionCanRead))
                    throw new SerializationException(string.Format("File format is version {0} App accepts formats >= {1}.",
                            version, instance.MinimumVersionCanRead));

                int readerVersion = 0;
                if (instance != null)
                    readerVersion = instance.Version;

                if (!(readerVersion >= minimumReaderVersion))
                    throw new SerializationException(string.Format("App is version {0}.  File format accepts apps >= {1}.",
                             readerVersion, minimumReaderVersion));
            }
        }

        internal int version;
        internal int minimumReaderVersion;
        internal string fullName;
        internal Func<IFastSerializable> factory;
        #endregion
    }

    /// <summary>
    /// #SerializerIntroduction see also code:#StreamLayout
    /// 
    /// The code:Serializer class is a general purpose object graph serializer helper. While it does not have
    /// any knowledge of the serialization format of individual object, it does impose conventions on how to
    /// serialize support information like the header (which holds verisioning information), a trailer (which
    /// holds defered pointer information), and how types are versioned. However these conventions are
    /// intended to be very generic and thus this class can be used for essentially any serialization need.
    /// 
    /// Goals:
    ///     * Allows full range of serialization, including subclassing and cyclic object graphs.
    ///     * Can be serialized and deserialized efficiently sequentially (no seeks MANDATED on read or
    ///         write). This allows the serializer to be used over pipes and other non-seekable devices).
    ///     * Pay for play (thus very efficient in simple cases (no subclassing or cyclic graphs).
    ///     * Ideally self-describing, and debuggable (output as XML if desired?)
    /// 
    /// Versioning:
    ///     * We want the ability for new formats to accept old versions if objects wish to support old
    ///         formats
    ///     * Also wish to allow new formats to be read by OLD version if the new format is just an
    ///         'extension' (data added to end of objects). This makes making new versions almost pain-free.
    ///         
    /// Concepts:
    ///     * No-seek requirement
    ///     
    ///         The serialized form should be such that it can be deseralized efficiently in a serial fasion
    ///         (no seeks). This means all information needed to deserialize has to be 'just in time' (can't
    ///         be some table at the end). Pragmatically this means that type information (needed to create
    ///         instances), has to be output on first use, so it is available for the deserializer.
    ///         
    ///     * Laziness requirement
    ///     
    ///         While is should be possible to read the serialized for sequentially, we should also not force
    ///         it. It should be possible to have a large file that represents a persisted stucture that can
    ///         be lazily brought into memory on demand. This means that all information needed to
    ///         deserialize must also be 'randomly available' and not depend on reading from the begining.
    ///         Pragmatically this means that type information, and forward forwardReference information needs to
    ///         have a table in a well known Location at the end so that it can be found without having to
    ///         search the file sequentially.
    ///     
    ///     * Versioning requirement
    ///         
    ///         To allow OLD code to access NEW formats, it must be the case that the serialized form of
    ///         every instance knows how to 'skip' past any new data (even if it does not know its exact
    ///         size). To support this, objects have 'begin' and 'end' tags, which allows the deserializer to
    ///         skip the next object.
    ///         
    ///     * Polymorphsim requirement
    ///     
    ///         Because the user of a filed may not know the exact instance stored there, in general objects
    ///         need to store the exact type of the instance. Thus they need to store a type identifer, this
    ///         can be folded into the 'begin' tag.
    ///         
    ///     * Arbitrary object graph (cicularity) requirement (Forward references)
    ///     
    ///         The serializer needs to be able to serialize arbirary object graphs, including those with
    ///         cycles in them. While you can do this without forward references, the system is more flexible
    ///         if it has the concept of a forward refernce. Thus whenever a object refernece is required, a
    ///         'forward forwardReference' can be given instead. What gets serialized is simply an unique forward
    ///         refernece index (index into an array), and at some later time that index is given its true
    ///         value. This can either happen with the target object is serialied (see
    ///         code:Serializer.Tags.ForwardDefintion) or at the end of the serialization in a forward
    ///         refernece table (which allows forward references to be resolved without scanning then entire
    ///         file.
    ///         
    ///     * Contract between objects code:IFastSerializable.ToStream:
    ///     
    ///         The heart of the serialization and deserialization process s the code:IFastSerializable
    ///         interface, which implements just two methods: ToStream (for serializing an object), and
    ///         FromStream (for deserializing and object). This intefaces is the mechanism by which objects
    ///         tell the serializer what data to store for an individual instance. However this core is not
    ///         enough. An object that implements code:IFastSerializable must also implement a default
    ///         constructor (constructor with no args), so that that deserializer can create the object (and
    ///         then call FromStream to populated it).
    ///         
    ///         The ToStream methodIndex is only responsible for serializing the data in the object, and by itself
    ///         is not sufficient to serialize an interconnected, polymoriphic graph of objects. It needs
    ///         help from the code:Serializer and code:Deserialize to do this. code:Serializer takes on the
    ///         responsibility to deal with persisting type information (so that code:Deserialize can create
    ///         the correct type before code:IFastSerializable.FromStream is called). It is also the
    ///         serializer's responsibilty to provide the mechanism for dealing with circular object graphs
    ///         and forward references.
    ///     
    ///     * Layout of a serialized object: A serialized object has the following basic format
    ///     
    ///         * If the object is the defintion of a previous forward references, then the defintion must
    ///             begin with a code:Serializer.Tags.ForwardDefintion tag followed by a forward forwardReference
    ///             index which is being defined.
    ///         * code:Serializer.Tags.BeginObject tag
    ///         * A reference to the code:SerializationType for the object. This refernece CANNOT be a
    ///             forward forwardReference because its value is needed during the deserialization process before
    ///             forward references are resolved.
    ///         * All the data that that objects 'code:IFastSerializable.ToStream methodIndex wrote. This is the
    ///             heart of the deserialized data, and the object itself has a lot of control over this
    ///             format.
    ///         * code:Serializer.Tags.EndObject tag. This marks the end of the object. It quickly finds bugs
    ///             in ToStream FromStream mismatches, and also allows for V1 deserializers to skip past
    ///             additional fields added since V1.
    ///         
    ///     * Serializing Object references:
    ///       When an object forwardReference is serialized, any of the following may follow in the stream
    ///       
    ///         * code:Serializer.Tags.NullReference used to encode a null object forwardReference.
    ///         * code:Serializer.Tags.BeginObject or code:Serializer.Tags.ForwardDefintion, which indicates
    ///             that this the first time the target object has been referenced, and the target is being
    ///             serialized on the spot.
    ///         * code:Serializer.Tags.ObjectReference which indicates that the target object has already
    ///             been serialized and what follows is the StreamLabel of where the definition is.
    ///         * code:Serializer.Tags.ForwardReference followed by a new forward forwardReference index. This
    ///             indicates that the object is not yet serialized, but the serializer has choosen not to
    ///             immediately serialize the object. Ultimately this object will be defined, but has not
    ///             happened yet.
    ///            
    ///     * Serializing Types:
    ///       Types are simply objects of type code:SerializationType which contain enough information about
    ///       the type for the Deserializer to do its work (it full name and version number).   They are
    ///       serialized just like all other types.  The only thing special about it is that references to
    ///       types after the BeginObject tag must not be forward references.  
    ///  
    /// #StreamLayout:
    ///     The structure of the file as a whole is simply a list of objects.  The first and last objects in
    ///     the file are part of the serialization infratructure.  
    ///     
    /// Layout Synopsis
    ///     * Signature representing code:Serializer format
    ///     * EntryObject (most of the rest of the file)
    ///         * BeginObject tag
    ///         * Type for This object (which is a object of type code:SerializationType)
    ///             * BeginObject tag
    ///             * Type for code:SerializationType  POSITION1
    ///                 * BeginObject tag
    ///                 * Type for code:SerializationType
    ///                      * ObjectReference tag           // This is how our recursion ends.  
    ///                      * StreamLabel for POSITION1
    ///                 * Version Field for SerializationType
    ///                 * Miniumum Version Field for SerializationType
    ///                 * FullName string for SerializationType                
    ///                 * EndObject tag
    ///             * Version field for EntryObject's type
    ///             * Miniumum Version field for EntryObject's type
    ///             * FullName string for EntryObject's type
    ///             * EndObject tag
    ///         * Field1  
    ///         * Field2 
    ///         * V2_Field (this should be tagged so that it can be skipped by V1 deserializers.  
    ///         * EndObject tag
    ///     * ForwardReferenceTable pseudo-object
    ///         * Count of forward references
    ///         * StreamLabel for forward ref 0
    ///         * StreamLabel for forward ref 1.
    ///         * ...
    ///     * SerializationTrailer pseduo-object
    ///         * StreamLabel ForwardReferenceTable
    ///     * StreamLabel to SerializationTrailer
    ///     * End of stream
    /// </summary>
    public class Serializer : IDisposable
    {
        /// <summary>
        /// Open a serializer to a file (for persistance). 
        /// </summary>
        public Serializer(string filePath, IFastSerializable entryObject) : this(new IOStreamStreamWriter(filePath), entryObject) { }
        // TODO Serializer owns the writer (will close it) is that what we want?
        public Serializer(IStreamWriter writer, IFastSerializable entryObject)
        {
            bool succeeded = false;
            try
            {
                TypesInGraph = new Dictionary<RuntimeTypeHandle, SerializationType>();
                ObjectsInGraph = new Dictionary<IFastSerializable, StreamLabel>();
                ObjectsWithForwardReferences = new Dictionary<IFastSerializable, ForwardReference>();
                this.writer = writer;

                Log("<Serializer>");
                // Write the header. 
                Write("!FastSerialization.1");

                // Write the main object.  This is recurisive and does most of the work. 
                Write(entryObject);

                // Write any forward references. 
                WriteDeferedObjects();

                // Write an unbalenced EndObject tag to represent the end of objects. 
                WriteTag(Tags.EndObject);

                // Write the forward forwardReference table (for random access lookup)  
                StreamLabel forwardRefsLabel = writer.GetLabel();
                Log("<ForwardRefTable StreamLabel=\"0x" + forwardRefsLabel.ToString("x") + "\">");
                if (forwardReferenceDefinitions != null)
                {
                    Write(forwardReferenceDefinitions.Count);
                    for (int i = 0; i < forwardReferenceDefinitions.Count; i++)
                    {
                        Debug.Assert(forwardReferenceDefinitions[i] != StreamLabel.Invalid);
                        Log("<ForwardDefEntry index=\"" + i + "\" StreamLabelRef=\"0x" + forwardReferenceDefinitions[i].ToString("x") + "\"/>");
                        writer.Write(forwardReferenceDefinitions[i]);
                    }
                }
                else
                    Write(0);
                Log("</ForwardRefTable>");

                // Write the trailer currently it has only one item in it, however it is expandable. 
                // items.  
                StreamLabel trailerLabel = writer.GetLabel();
                Log("<Trailer StreamLabel=\"0x" + trailerLabel.ToString("x") + "\">");
                Write(forwardRefsLabel);
                // More stuff goes here in future versions. 
                Log("</Trailer>");

                Log("<WriteSuffixLabel StreamLabelRef=\"0x" + trailerLabel.ToString("x") + "\" StreamLabel=\"0x" + writer.GetLabel().ToString("x") + "\"/>");
                writer.WriteSuffixLabel(trailerLabel);
                Log("</Serializer>");
                succeeded = true;
            }
            finally
            {
                if (!succeeded)
                    writer.Dispose();
            }
        }

        // Convinience functions. 
        public void Write(bool value)
        {
            Write((byte)(value ? 1 : 0));
        }
        public void Write(byte value)
        {
            Log("<Write Type=\"byte\" Value=\"" + value + "\" StreamLabel=\"0x" + writer.GetLabel().ToString("x") + "\"/>");
            writer.Write(value);
        }
        public void Write(short value)
        {
            Log("<Write Type=\"short\" Value=\"" + value + "\" StreamLabel=\"0x" + writer.GetLabel().ToString("x") + "\"/>");
            writer.Write(value);
        }
        public void Write(int value)
        {
            Log("<Write Type=\"int\" Value=\"" + value + "\" StreamLabel=\"0x" + writer.GetLabel().ToString("x") + "\"/>");
            writer.Write(value);
        }
        public void Write(long value)
        {
            Log("<Write Type=\"long\" Value=\"" + value + "\" StreamLabel=\"0x" + writer.GetLabel().ToString("x") + "\"/>");
            writer.Write(value);
        }
        public void Write(Guid value)
        {
            Log("<Write Type=\"Guid\" Value=\"" + value + "\" StreamLabel=\"0x" + writer.GetLabel().ToString("x") + "\"/>");
            writer.Write(value);
        }
        public void Write(string value)
        {
#if DEBUG
            if (value == null)
                Log("<Write Type=\"null string\" StreamLabel=\"0x" + writer.GetLabel().ToString("x") + "\"/>");
            else
                Log("<Write Type=\"string\" Value=" + XmlUtilities.XmlQuote(value) + " StreamLabel=\"0x" + writer.GetLabel().ToString("x") + "\"/>");
#endif
            writer.Write(value);
        }
        public unsafe void Write(float value)
        {
            int* intPtr = (int*)&value;
            writer.Write(*intPtr);
        }
        public unsafe void Write(double value)
        {
            long* longPtr = (long*)&value;
            writer.Write(*longPtr);
        }

        /// <summary>
        /// If the object is potentially aliased (multiple references to it), you should write it with this methodIndex.
        /// </summary>
        public void Write(IFastSerializable obj) { WriteObjectRef(obj, false); }

        // TODO: which to support?
        public void Write(int? value)
        {
            Write(value.HasValue);
            if (value.HasValue)
                Write(value.Value);
        }

        /// <summary>
        /// To tune working set (or disk seeks), or to make the dump of the format more readable, it is
        /// valueable to have control over which of several references to an object will actually cause it to
        /// be serialized (by default the first encountered does it).
        /// 
        /// WriteDeferedReference allows you to write just a forwardReference to an object with the expectation that
        /// somewhere later in the serialization process the object will be serialized. If no call to
        /// WriteObject() occurs, then the object is serialized automatically before the stream is closed
        /// (thus dangling references are impossible).        
        /// </summary>
        public void WriteDefered(IFastSerializable obj) { WriteObjectRef(obj, true); }
        public void WriteDeferedObjects()
        {
            if (ObjectsWithForwardReferences == null)
                return;

            Log("<WriteDeferedObjects>");
            List<IFastSerializable> objs = new List<IFastSerializable>();
            while (ObjectsWithForwardReferences.Count > 0)
            {
                // Copy the objects out because the calls to WriteObjectReference updates the collection.  
                objs.AddRange(ObjectsWithForwardReferences.Keys);
                foreach (IFastSerializable obj in objs)
                {
                    Write(obj);
                    Debug.Assert(!ObjectsWithForwardReferences.ContainsKey(obj));
                }
                objs.Clear();
            }
            Log("</WriteDeferedObjects>");
        }

        /// <summary>
        /// This is an optimized version of code:WriteObjectReference that can be used in some cases.
        /// 
        /// If the object is not aliased (it has an 'owner' and only that owner has references to it (which
        /// implies its lifetime is strictly less than its owners), then the serialzation system does not
        /// need to put the object in the 'interning' table. This saves a space (entries in the intern table
        /// as well as 'SyncEntry' overhead of creating hash codes for object) as well as time (to create
        /// that bookkeeping) for each object that is treated as private (which can add up if because it is
        /// common that many objects are private).  The private instances are also marked in the serialized
        /// format so on reading there is a simmilar bookeeping savings. 
        /// 
        /// The ultimate bits written by code:WritePrivateObject are the same as code:WriteObject.
        /// 
        /// TODO Need a DEBUG mode where we detect if others besides the owner refernence the object.
        /// </summary>
        public void WritePrivate(IFastSerializable obj)
        {
            Log("<WritePrivateObject obj=\"0x" + obj.GetHashCode().ToString("x") +
                "\" StreamLabel=\"0x" + writer.GetLabel().ToString("x") + "\">");
            WriteObjectData(obj, Tags.BeginPrivateObject);
            Log("</WritePrivateObject>");
        }

        public void Write(StreamLabel value)
        {
            Log("<Write Type=\"StreamLabel\" StreamLabelRef=\"0x" + value.ToString("x") + "\" StreamLabel=\"0x" + writer.GetLabel().ToString("x") + "\"/>");
            writer.Write(value);
        }
        public void Write(ForwardReference value)
        {
            Log("<Write Type=\"ForwardReference\" indexRef=\"" + value + "\" StreamLabel=\"0x" + writer.GetLabel().ToString("x") + "\"/>");
            writer.Write((int)value);
        }
        public ForwardReference GetForwardReference()
        {
            if (forwardReferenceDefinitions == null)
                forwardReferenceDefinitions = new List<StreamLabel>();
            ForwardReference ret = (ForwardReference)forwardReferenceDefinitions.Count;
            forwardReferenceDefinitions.Add(StreamLabel.Invalid);
            return ret;
        }
        public void DefineForwardReference(ForwardReference forwardReference)
        {
            forwardReferenceDefinitions[(int)forwardReference] = writer.GetLabel();
        }

        public void WriteCollection<T>(ICollection<T> elems, string name) where T : IFastSerializable
        {
            Log("<WriteColection name=\"" + name + "\" count=\"" + elems.Count + "\">\r\n");
            Write(elems.Count);
            foreach (IFastSerializable elem in elems)
                Write(elem);
            Log("</WriteColection>\r\n");
        }

        // data added after V1 needs to be tagged so that V1 deserializers can skip it.  
        public void WriteTagged(byte value) { WriteTag(Tags.Byte); Write(value); }
        public void WriteTagged(short value) { WriteTag(Tags.Int16); Write(value); }
        public void WriteTagged(int value) { WriteTag(Tags.Int32); Write(value); }
        public void WriteTagged(long value) { WriteTag(Tags.Int64); Write(value); }
        public void WriteTagged(string value) { WriteTag(Tags.String); Write(value); }
        public void WriteTagged(IFastSerializable value)
        {
            WriteTag(Tags.SkipRegion);
            ForwardReference endRegion = GetForwardReference();
            Write(endRegion);        // Allow the reader to skip this. 
            Write(value);            // Write the data we can skip
            DefineForwardReference(endRegion);  // This is where the forward reference refers tol  
        }

        /// <summary>
        /// Retrieve the underlying stream we are writing to.  Generally the Write* methods are enough. 
        /// </summary>
        public IStreamWriter Writer { get { return writer; } }
        /// <summary>
        /// Completes the writing of the stream. 
        /// </summary>
        public virtual void Close()
        {
            writer.Dispose();
            if (log != null)
            {
                log.Close();
                log = null;
            }
        }

        #region protected
        StreamWriter log;
        [Conditional("DEBUG_SERIALIZE")]
        // see also code:Deserializer.Log 
        public void Log(string str)
        {
            if (log == null)
                log = File.CreateText("log.serialize.xml");
            log.WriteLine(str);
        }

        private void WriteTag(Tags tag)
        {
            Log("<WriteTag Type=\"" + tag + "\" Value=\"" + ((int)tag).ToString() + "\" StreamLabel=\"0x" + writer.GetLabel().ToString("x") + "\"/>");
            writer.Write((byte)tag);
        }
        private void WriteObjectRef(IFastSerializable obj, bool defered)
        {

            if (obj == null)
            {
                Log("<WriteNullReference>");
                WriteTag(Tags.NullReference);
                Log("</WriteNullReference>");
                return;
            }

            StreamLabel reference;
            if (ObjectsInGraph.TryGetValue(obj, out reference))
            {
                Log("<WriteReference streamLabelRef=\"0x" + reference.ToString("x") +
                    "\" objRef=\"0x" + obj.GetHashCode().ToString("x") + "\">");
                WriteTag(Tags.ObjectReference);
                Write(reference);
                Log("</WriteReference>");
                return;
            }

            // If we have a forward forwardReference to this, get it. 
            ForwardReference forwardReference;
            if (defered)
            {
                if (ObjectsWithForwardReferences == null)
                    ObjectsWithForwardReferences = new Dictionary<IFastSerializable, ForwardReference>();

                if (!ObjectsWithForwardReferences.TryGetValue(obj, out forwardReference))
                {
                    forwardReference = GetForwardReference();
                    ObjectsWithForwardReferences.Add(obj, forwardReference);
                }
                Log("<WriteForwardReference indexRef=\"0x" + ((int)forwardReference).ToString("x") +
                    "\" objRef=\"0x" + obj.GetHashCode().ToString("x") +
                    "\" type=\"" + obj.GetType().Name + "\">");
                WriteTag(Tags.ForwardReference);

                // Write the forward forwardReference index
                Write((int)forwardReference);
                // And its type. 
                WriteTypeForObject(obj);
                Log("</WriteForwardReference>");
                return;
            }

            // At this point we are writing an actual object and not a reference. 
            // 
            StreamLabel objLabel = writer.GetLabel();
            Log("<WriteObject obj=\"0x" + obj.GetHashCode().ToString("x") +
                "\" StreamLabel=\"0x" + objLabel.ToString("x") +
                "\" type=\"" + obj.GetType().Name + "\">");
            // Have we just defined an object that has a forward forwardReference to it?
            if (ObjectsWithForwardReferences != null &&
                ObjectsWithForwardReferences.TryGetValue(obj, out forwardReference))
            {
                Log("<WriteForwardReferenceDefinition index=\"0x" + ((int)forwardReference).ToString("x") + "\">");
                // OK, tag the definition with the forward forwardReference index
                WriteTag(Tags.ForwardDefinition);
                Write((int)forwardReference);

                // And also put it in the ForwardReferenceTable.  
                forwardReferenceDefinitions[(int)forwardReference] = objLabel;
                // And we can remove it from the ObjectsWithForwardReferences table
                ObjectsWithForwardReferences.Remove(obj);
                Log("</WriteForwardReferenceDefinition>");
            }

            // Add to object graph before calling ToStream (for recursive objects)
            ObjectsInGraph.Add(obj, objLabel);
            WriteObjectData(obj, Tags.BeginObject);
            Log("</WriteObject>");
        }
        private void WriteTypeForObject(IFastSerializable obj)
        {
            // Write the type of the forward forwardReference. 
            RuntimeTypeHandle handle = obj.GetType().TypeHandle;
            SerializationType type;
            if (!TypesInGraph.TryGetValue(handle, out type))
            {
                type = CreateTypeForObject(obj);
                TypesInGraph.Add(handle, type);
            }
            Log("<WriteTypeForObject TypeName=\"" + type + "\">");
            WriteObjectRef(type, false);
            Log("</WriteTypeForObject>");

        }
        private void WriteObjectData(IFastSerializable obj, Tags beginTag)
        {
            Debug.Assert(beginTag == Tags.BeginObject || beginTag == Tags.BeginPrivateObject);
            WriteTag(beginTag);
            WriteTypeForObject(obj);
            obj.ToStream(this);

            WriteTag(Tags.EndObject);
        }
        private SerializationType CreateTypeForObject(IFastSerializable instance)
        {
            Type type = instance.GetType();

            // Special case: the SerializationType for SerializationType itself is null.  This avoids
            // recurision.  
            if (type == typeof(SerializationType))
                return null;

            SerializationType ret = new SerializationType(type);
            IFastSerializableVersion versionInstance = instance as IFastSerializableVersion;
            if (versionInstance != null)
            {
                ret.minimumReaderVersion = versionInstance.MinimumReaderVersion;
                ret.version = versionInstance.Version;
            }
            return ret;
        }
        void IDisposable.Dispose()
        {
            Close();
        }

        internal IStreamWriter writer;
        internal IDictionary<RuntimeTypeHandle, SerializationType> TypesInGraph;
        internal IDictionary<IFastSerializable, StreamLabel> ObjectsInGraph;
        internal IDictionary<IFastSerializable, ForwardReference> ObjectsWithForwardReferences;
        internal List<StreamLabel> forwardReferenceDefinitions;
        #endregion
    };

    /// <summary>
    /// code:Deserializer is a helper class that holds all the information needed to deserialize an object
    /// graph as a whole (things like the table of objects already deserialized, and the list of types in
    /// the object graph.  
    /// 
    /// see code:#SerializerIntroduction for more
    /// </summary>
    public class Deserializer : IDisposable
    {
        public Deserializer(string filePath) : this(new IOStreamStreamReader(filePath), filePath) { }
        public Deserializer(IStreamReader reader, string streamName)
        {
            ObjectsInGraph = new Dictionary<StreamLabel, IFastSerializable>();
            this.reader = reader;
            this.allowLazyDeserialization = true;
            this.factories = new Dictionary<string, Func<IFastSerializable>>();
            RegisterFactory(typeof(SerializationType), delegate { return new SerializationType(); });

            Log("<Deserialize>");
            // We don't do this with ReadString() because it is a likely point of failure (a completely wrong file)
            // and we want to not read garbage if we don't have to
            var expectedSig = "!FastSerialization.1";
            int sigLen = reader.ReadInt32();
            if (sigLen != expectedSig.Length)
                goto ThrowException;
            for (int i = 0; i < sigLen; i++)
                if (reader.ReadByte() != expectedSig[i])
                    goto ThrowException;
            return;
        ThrowException:
            throw new SerializationException("Not a understood file format: " + streamName);
        }
        /// <summary>
        /// On by default.  If off, then you read the whole file from begining to the end and never have to
        /// seek.  This should only be used when you have this requirement (you are reading from an unseekable
        /// stream 
        /// 
        /// TODO remove? we have not tested as tall with AllowLazyDeserialzation==false. 
        /// </summary>
        public bool AllowLazyDeserialization
        {
            get { return allowLazyDeserialization; }
            set { allowLazyDeserialization = value; }
        }
        public void GetEntryObject<T>(out T ret)
        {
            ret = (T)GetEntryObject();
        }

        /// <summary>
        /// Returns the full name of the type of the entry object without actually creating it.
        /// Will return null on failure.  
        /// </summary>  
        public string GetEntryTypeName()
        {
            StreamLabel origPosition = reader.Current;
            SerializationType objType = null;
            try
            {
                Tags tag = ReadTag();
                if (tag == Tags.BeginObject)
                    objType = (SerializationType)ReadObject();
            }
            catch (Exception) { }
            reader.Goto(origPosition);
            if (objType == null)
                return null;
            return objType.FullName;
        }
        public IFastSerializable GetEntryObject()
        {
            if (entryObject == null)
            {
                // If you are going to deserialize the world, better to do it in order, which means defering
                // forward references (since you will get to them eventually).  
                if (!allowLazyDeserialization)
                    deferForwardReferences = true;

                Log("<GetEntryObject deferForwardReferences=\"" + deferForwardReferences + "\">");
                entryObject = ReadObjectDefintion();

                // If we are reading sequentially, read the position of the objects (will be marked by a
                // unmatched EndObject tag. 
                if (!allowLazyDeserialization)
                {
                    for (; ; )
                    {
                        StreamLabel objectLabel = reader.Current;
                        Tags tag = ReadTag();
                        if (tag == Tags.EndObject)
                            break;
                        ReadObjectDefinition(tag, objectLabel);
                    }
                }
                Debug.Assert(unInitializedForwardReferences == null || unInitializedForwardReferences.Count == 0);
                Log("</GetEntryObject>");
            }
            return entryObject;
        }

        // For FromStream methodIndex bodies.  
        public void Read(out bool ret)
        {
#if DEBUG
            StreamLabel label = reader.Current;
#endif
            ret = reader.ReadByte() != 0;
#if DEBUG
            Log("<ReadByte Value=\"" + ret.ToString() + "\" StreamLabel=\"0x" + label.ToString("x") + "\"/>");
#endif
        }
        public void Read(out byte ret)
        {
#if DEBUG
            StreamLabel label = reader.Current;
#endif
            ret = reader.ReadByte();
#if DEBUG
            Log("<ReadByte Value=\"" + ret.ToString() + "\" StreamLabel=\"0x" + label.ToString("x") + "\"/>");
#endif
        }
        public void Read(out short ret)
        {
#if DEBUG
            StreamLabel label = reader.Current;
#endif
            ret = reader.ReadInt16();
#if DEBUG
            Log("<ReadInt16 Value=\"" + ret.ToString() + "\" StreamLabel=\"0x" + label.ToString("x") + "\"/>");
#endif
        }
        public void Read(out int ret)
        {
#if DEBUG
            StreamLabel label = reader.Current;
#endif
            ret = reader.ReadInt32();
#if DEBUG
            Log("<ReadInt32 Value=\"" + ret.ToString() + "\" StreamLabel=\"0x" + label.ToString("x") + "\"/>");
#endif
        }
        public void Read(out long ret)
        {
#if DEBUG
            StreamLabel label = reader.Current;
#endif
            ret = reader.ReadInt64();
#if DEBUG
            Log("<ReadInt64 Value=\"" + ret.ToString() + "\" StreamLabel=\"0x" + label.ToString("x") + "\"/>");
#endif
        }
        public void Read(out Guid ret)
        {
#if DEBUG
            StreamLabel label = reader.Current;
#endif
            ret = reader.ReadGuid();
#if DEBUG
            Log("<ReadGuid Value=\"" + ret.ToString() + "\" StreamLabel=\"0x" + label.ToString("x") + "\"/>");
#endif
        }
        public void Read(out float ret)
        {
            ret = ReadFloat();
        }
        public void Read(out double ret)
        {
            ret = ReadDouble();
        }
        public void Read(out string ret)
        {
#if DEBUG
            StreamLabel label = reader.Current;
#endif
            ret = reader.ReadString();
#if DEBUG
            if (ret == null)
                Log("<ReadString StreamLabel=\"0x" + label.ToString("x") + "\"/>");
            else
                Log("<ReadString Value=" + XmlUtilities.XmlQuote(ret) + " StreamLabel=\"0x" + label.ToString("x") + "\"/>");
#endif
        }
        public void Read(out StreamLabel ret)
        {
#if DEBUG
            StreamLabel label = reader.Current;
#endif
            ret = reader.ReadLabel();
#if DEBUG
            Log("<Read Type=\"StreamLabel\" Value=\"0x" + ret.ToString("x") + "\" StreamLabel=\"0x" + label.ToString("x") + "\"/>");
#endif
        }

        public void Read<T>(out T ret) where T : IFastSerializable
        {
            ret = (T)ReadObject();
        }
        public IFastSerializable ReadObject()
        {
            Log("<ReadObjectReference StreamLabel=\"0x" + reader.Current.ToString("x") + "\">");

            StreamLabel objectLabel = reader.Current;
            Tags tag = ReadTag();
            IFastSerializable ret;
            if (tag == Tags.ObjectReference)
            {
                StreamLabel target = reader.ReadLabel();
                if (!ObjectsInGraph.TryGetValue(target, out ret))
                    ret = ReadObject(target);
            }
            else if (tag == Tags.NullReference)
            {
                ret = null;
            }
            else if (tag == Tags.ForwardReference)
            {
                Log("<ReadForwardRef>");
                ForwardReference forwardReference = ReadForwardReference();
                Log("<ReadForwardReferenceType>");
                SerializationType type = (SerializationType)ReadObject();
                Log("</ReadForwardReferenceType>");

                StreamLabel definition = ResolveForwardReference(forwardReference);
                if (definition != StreamLabel.Invalid)
                {
                    if (!ObjectsInGraph.TryGetValue(definition, out ret))
                    {
                        Log("<FoundDefinedForwardRef StreamLabelRef=\"0x" + definition.ToString("x") + "\">");
                        StreamLabel orig = reader.Current;
                        Goto(definition);
                        ret = ReadObjectDefintion();
                        Goto(orig);
                        Log("</FoundDefinedForwardRef>");
                    }
                }
                else
                {
                    if (unInitializedForwardReferences == null)
                        unInitializedForwardReferences = new Dictionary<ForwardReference, IFastSerializable>();

                    if (!unInitializedForwardReferences.TryGetValue(forwardReference, out ret))
                    {
                        ret = type.CreateInstance();
                        Log("<AddingUninitializedForwardRef indexRef=\"" + forwardReference + "\" objRef=\"0x" + ret.GetHashCode().ToString("x") + "\"/>");
                        unInitializedForwardReferences.Add(forwardReference, ret);
                    }
                    else
                        Log("<FoundExistingForwardRef indexRef=\"" + forwardReference + "\" objRef=\"0x" + ret.GetHashCode().ToString("x") + "\"/>");
                }
                Log("</ReadForwardRef>");
            }
            else
            {
                ret = ReadObjectDefinition(tag, objectLabel);
            }
            Log("<Return objRef=\"0x" + (ret == null ? "0" : ret.GetHashCode().ToString("x")) + "\"" +
                (ret == null ? "" : " type=\"" + ret.GetType().Name + "\"") + "/>");
            Log("</ReadObjectReference>");
            return ret;
        }
        public bool ReadBool()
        {
            return ReadByte() != 0;
        }
        public byte ReadByte()
        {
#if DEBUG
            StreamLabel label = reader.Current;
#endif
            byte ret = reader.ReadByte();
#if DEBUG
            Log("<ReadByte Value=\"" + ret.ToString() + "\" StreamLabel=\"0x" + label.ToString("x") + "\"/>");
#endif
            return ret;
        }
        public short ReadInt16()
        {
#if DEBUG
            StreamLabel label = reader.Current;
#endif
            short ret = reader.ReadInt16();
#if DEBUG
            Log("<ReadInt16 Value=\"" + ret.ToString() + "\" StreamLabel=\"0x" + label.ToString("x") + "\"/>");
#endif
            return ret;
        }
        public int ReadInt()
        {
#if DEBUG
            StreamLabel label = reader.Current;
#endif
            int ret = reader.ReadInt32();
#if DEBUG
            Log("<ReadInt32 Value=\"" + ret.ToString() + "\" StreamLabel=\"0x" + label.ToString("x") + "\"/>");
#endif
            return ret;
        }
        public long ReadInt64()
        {
#if DEBUG
            StreamLabel label = reader.Current;
#endif
            long ret = reader.ReadInt64();
#if DEBUG
            Log("<ReadInt64 Value=\"" + ret.ToString() + "\" StreamLabel=\"0x" + label.ToString("x") + "\"/>");
#endif
            return ret;
        }

        public unsafe float ReadFloat()
        {
            float ret;
            int* intPtr = (int*)&ret;
            *intPtr = reader.ReadInt32();
            return ret;
        }
        public unsafe double ReadDouble()
        {
            double ret;
            long* longPtr = (long*)&ret;
            *longPtr = reader.ReadInt64();
            return ret;
        }
        public string ReadString()
        {
#if DEBUG
            StreamLabel label = reader.Current;
#endif
            string ret = reader.ReadString();
#if DEBUG
            Log("<ReadString Value=\"" + ret.ToString() + "\" StreamLabel=\"0x" + label.ToString("x") + "\"/>");
#endif
            return ret;
        }

        // TODO: how much support for T?
        public void Read(out int? value)
        {
            bool hasVal;
            Read(out hasVal);
            if (hasVal)
            {
                int val; Read(out val);
                value = val;
            }
            else
                value = null;
        }

        /// <summary>
        /// Meant to be called from FromStream.  It returns the version number of the 
        /// type being deserialized.   It can be used so that new code can recognises that it
        /// is reading an old file format and adjust what it reads.   
        /// </summary>
        public int VersionBeingRead { get { return typeBeingRead.Version; } }

        public StreamLabel ReadLabel()
        {
#if DEBUG
            StreamLabel label = reader.Current;
#endif
            StreamLabel ret = reader.ReadLabel();
#if DEBUG
            Log("<ReadLabel StreamLabelRef=\"0x" + ret.ToString("x") + "\" StreamLabel=\"0x" + label.ToString("x") + "\"/>");
#endif
            return ret;
        }
        public ForwardReference ReadForwardReference()
        {
#if DEBUG
            StreamLabel label = reader.Current;
#endif
            ForwardReference ret = (ForwardReference)reader.ReadInt32();
#if DEBUG
            Log("<ReadForwardReference indexRef=\"" + ret + "\" StreamLabel=\"0x" + label.ToString("x") + "\"/>");
#endif
            return ret;
        }
        public StreamLabel ResolveForwardReference(ForwardReference reference)
        {
            return ResolveForwardReference(reference, true);
        }
        public StreamLabel ResolveForwardReference(ForwardReference reference, bool preserveCurrent)
        {
            StreamLabel ret = StreamLabel.Invalid;
            if (forwardReferenceDefinitions == null)
                forwardReferenceDefinitions = new List<StreamLabel>();

            if ((uint)reference < (uint)forwardReferenceDefinitions.Count)
                ret = forwardReferenceDefinitions[(int)reference];

            if (ret == StreamLabel.Invalid && !deferForwardReferences)
            {
                Log("<GetFowardReferenceTable>");
                StreamLabel orig = reader.Current;

                reader.GotoSuffixLabel();
                Log("<Trailer StreamLabel=\"0x" + reader.Current.ToString("x") + "\"/>");
                StreamLabel forwardRefsLabel = reader.ReadLabel();

                Goto(forwardRefsLabel);
                int fowardRefCount = reader.ReadInt32();
                Log("<ForwardReferenceDefinitons StreamLabel=\"0x" + forwardRefsLabel.ToString("x") +
                    "\" Count=\"" + fowardRefCount + "\">");
                for (int i = 0; i < fowardRefCount; i++)
                {
                    StreamLabel defintionLabel = reader.ReadLabel();
                    if (i >= forwardReferenceDefinitions.Count)
                        forwardReferenceDefinitions.Add(defintionLabel);
                    else
                    {
                        Debug.Assert(
                            forwardReferenceDefinitions[i] == StreamLabel.Invalid ||
                            forwardReferenceDefinitions[i] == defintionLabel);
                        forwardReferenceDefinitions[i] = defintionLabel;
                    }
                    Log("<ForwardReference index=\"" + i + "\"  StreamLabelRef=\"0x" + defintionLabel.ToString("x") + "\"/>");
                }
                Log("</ForwardReferenceDefinitons>");
                if (preserveCurrent)
                    Goto(orig);
                ret = forwardReferenceDefinitions[(int)reference];
                Log("</GetFowardReferenceTable>");
            }

            Log("<GetForwardReference indexRef=\"" + reference +
                "\" StreamLabelRef=\"0x" + ret.ToString("x") +
                "\" deferForwardReferences=\"" + deferForwardReferences + "\"/>");
            return ret;
        }

        public void RegisterFactory(Type type, Func<IFastSerializable> factory)
        {
            factories[type.FullName] = factory;
        }
        public void RegisterDefaultFactory(Func<Type, IFastSerializable> defaultFactory)
        {
            this.defaultFactory = defaultFactory;
        }
        // For FromStream methodIndex bodies, reading tagged values (for post V1 field additions)
        // If the item is present, it is read into 'ret' otherwise 'ret' is left unchanged.   
        // If after V1 you always add fields at the end, and you always use WriteTagged() and TryReadTagged()
        // to write and read them, then you always get perfect backward and forward compatibility! 
        // For things like collections you can do
        //
        // in collectionLen = 0;
        // TryReadTagged(ref collectionLen)
        // this.array = new int[collectionLen]; // inititalize the array
        // for(int i =0; i < collectionLenght; i++)
        //      Read(out this.array[i]);
        //
        // notice that the reading the array elements is does not use TryReadTagged, but IS 
        // conditional on the collection length (which is tagged).   
        public bool TryReadTagged(ref byte ret)
        {
            Tags tag = ReadTag();
            if (tag == Tags.Byte)
            {
                Read(out ret);
                return true;
            }
            reader.Goto(Current - 1);
            return false;
        }
        public bool TryReadTagged(ref short ret)
        {
            Tags tag = ReadTag();
            if (tag == Tags.Int16)
            {
                Read(out ret);
                return true;
            }
            reader.Goto(Current - 1);
            return false;
        }
        public bool TryReadTagged(ref int ret)
        {
            Tags tag = ReadTag();
            if (tag == Tags.Int32)
            {
                Read(out ret);
                return true;
            }
            reader.Goto(Current - 1);
            return false;
        }
        public bool TryReadTagged(ref long ret)
        {
            Tags tag = ReadTag();
            if (tag == Tags.Int64)
            {
                Read(out ret);
                return true;
            }
            reader.Goto(Current - 1);
            return false;
        }
        public bool TryReadTagged(ref string ret)
        {
            Tags tag = ReadTag();
            if (tag == Tags.String)
            {
                Read(out ret);
                return true;
            }
            reader.Goto(Current - 1);
            return false;
        }
        public bool TryReadTagged<T>(ref T ret) where T : IFastSerializable
        {
            // Tagged objects always start with a SkipRegion so we don't need to know its size.  
            Tags tag = ReadTag();
            if (tag == Tags.SkipRegion)
            {
                ReadForwardReference();     // Skip the forward reference which is part of SkipRegion 
                ret = (T)ReadObject();      // Read the real object 
                return true; 
            }
            reader.Goto(Current - 1);
            return false;
        }
        public IFastSerializable TryReadTaggedObject()
        {
            // Tagged objects always start with a SkipRegion so we don't need to know its size.  
            Tags tag = ReadTag();
            if (tag == Tags.SkipRegion)
            {
                ReadForwardReference();     // Skip the forward reference which is part of SkipRegion 
                return ReadObject();        // Read the real object 
            }
            reader.Goto(Current - 1);
            return null;
        }

        public void Goto(StreamLabel label)
        {
            Log("<Goto StreamLabelRef=\"0x" + label.ToString("x") + "\"/>");
            reader.Goto(label);
        }
        public void Goto(ForwardReference reference)
        {
            Goto(ResolveForwardReference(reference, false));
        }
        public StreamLabel Current { get { return reader.Current; } }
        public IStreamReader Reader { get { return reader; } }
        public void Dispose()
        {
            reader.Dispose();
            reader = null;
            ObjectsInGraph = null;
            forwardReferenceDefinitions = null;
            unInitializedForwardReferences = null;
            if (log != null)
            {
                Log("</Deserialize>");
                log.Close();
                log = null;
            }
        }

        #region protected
        StreamWriter log;
        [Conditional("DEBUG_SERIALIZE")]
        // see also code:Serializer.Log 
        internal void Log(string str)
        {
            if (log == null)
                log = File.CreateText("log.deserialize.xml");
            log.WriteLine(str);
            log.Flush();
        }

        internal IFastSerializable ReadObject(StreamLabel label)
        {
            StreamLabel orig = reader.Current;
            Goto(label);
            IFastSerializable ret = ReadObject();
            Goto(orig);
            return ret;
        }

        private IFastSerializable ReadObjectDefintion()
        {
            StreamLabel objectLabel = reader.Current;
            Tags tag = ReadTag();
            return ReadObjectDefinition(tag, objectLabel);
        }
        private IFastSerializable ReadObjectDefinition(Tags tag, StreamLabel objectLabel)
        {
            Log("<ReadObjectDefinition StreamLabel=\"0x" + reader.Current.ToString("x") + "\" Tag=\"" + tag + "\">");

            IFastSerializable ret;
            IFastSerializable existingObj = null; ;
            SerializationType type;
            if (tag == Tags.BeginPrivateObject)
            {
                type = (SerializationType)ReadObject();
                ret = type.CreateInstance();
            }
            else
            {
                ForwardReference forwardReference = ForwardReference.Invalid;
                if (tag == Tags.ForwardDefinition)
                {
                    forwardReference = (ForwardReference)reader.ReadInt32();
                    Log("<ForwardDefintion index=\"" + forwardReference + "\"/>");
                    tag = ReadTag();
                }
                if (tag != Tags.BeginObject)
                    throw new SerializationException("Bad serialization tag found when starting object");
                Log("<ReadType>");
                type = (SerializationType)ReadObject();

                // Special case, a null object forwardReference means 'typeof serializationType'
                if (type == null)
                    type = new SerializationType(typeof(SerializationType).FullName, this);
                Log("</ReadType>");

                // Create the instance (or get it from the unInitializedForwardReferences if it was created
                // that way).  
                if (forwardReference != ForwardReference.Invalid)
                {
                    DefineForwardReference(forwardReference, objectLabel);
                    if (unInitializedForwardReferences != null && unInitializedForwardReferences.TryGetValue(forwardReference, out ret))
                    {
                        Log("<RemovingUninitializedForwardRef indexRef=\"" + forwardReference + "\" objRef=\"0x" + ret.GetHashCode().ToString("x") + "\"/>");
                        unInitializedForwardReferences.Remove(forwardReference);
                    }
                    else
                        ret = type.CreateInstance();
                }
                else
                    ret = type.CreateInstance();

                Log("<AddingObjectToGraph StreamLabelRef=\"0x" + objectLabel.ToString("x") +
                    "\" obj=\"0x" + ret.GetHashCode().ToString("x") +
                    "\" type=\"" + ret.GetType().Name +
                    "\"/>");

                if (!ObjectsInGraph.TryGetValue(objectLabel, out existingObj))
                    ObjectsInGraph.Add(objectLabel, ret);
            }

            // Actually initialize the object's fields.
            var saveTypeBeingRead = typeBeingRead;
            typeBeingRead = type;
            ret.FromStream(this);
            typeBeingRead = saveTypeBeingRead;
            FindEndTag(type, ret);

            // TODO in the case where the object already exist, we just created an object just to throw it
            // away just (so we can skip the fields). figure out a better way.
            if (existingObj != null)
            {
                Log("<UseExistingObject/>");
                ret = existingObj;
            }

            Log("<Return obj=\"0x" + (ret == null ? "0" : ret.GetHashCode().ToString("x")) + "\"" +
                (ret == null ? "" : " type=\"" + ret.GetType().Name + "\"") + "/>");
            Log("</ReadObjectDefinition>");
            return ret;
        }

        private void FindEndTag(SerializationType type, IFastSerializable objectBeingDeserialized)
        {
            // Skip any extra fields in the object that I don't understand. 
            Log("<EndTagSearch>");
            int i = 0;
            for (; ; )
            {
                Debug.Assert(i == 0 || type.Version != 0);
                StreamLabel objectLabel = reader.Current;
                // If this fails, the likely culprit is the FromStream of the objectBeingDeserialized. 
                Tags tag = ReadTag();
                int nesting = 0;
                switch (tag)
                {
                    case Tags.Byte:
                        reader.ReadByte();
                        break;
                    case Tags.Int16:
                        reader.ReadInt16();
                        break;
                    case Tags.Int32:
                        reader.ReadInt32();
                        break;
                    case Tags.Int64:
                        reader.ReadInt64();
                        break;
                    case Tags.String:
                        reader.ReadString();
                        break;
                    case Tags.NullReference:
                        break;
                    case Tags.BeginObject:
                    case Tags.BeginPrivateObject:
                        nesting++;
                        break;
                    case Tags.ForwardDefinition:
                    case Tags.ForwardReference:
                        ReadForwardReference();
                        break;
                    case Tags.ObjectReference:
                        reader.ReadLabel();
                        break;
                    case Tags.SkipRegion:
                        // Allow the region to be skipped.  
                        ForwardReference endSkipRef = ReadForwardReference();
                        StreamLabel endSkip = ResolveForwardReference(endSkipRef);
                        reader.Goto(endSkip);
                        break;
                    case Tags.EndObject:
                        --nesting;
                        if (nesting < 0)
                            goto done;
                        break;
                    default:
                        throw new SerializationException("Could not find object end tag for object of type " + objectBeingDeserialized.GetType().Name + " at stream offset 0x" + ((int)objectLabel).ToString("x"));
                }
                i++;
            }
        done:
            Log("</EndTagSearch>");
            // TODO would like some redundancy, so that failure happen close to the cause.  
        }

        private void DefineForwardReference(ForwardReference forwardReference, StreamLabel definitionLabel)
        {
            Log("<DefineForwardReference indexRef=\"" + forwardReference + "\" StreamLableRef=\"0x" + definitionLabel.ToString("x") + "\"/>");

            if (forwardReferenceDefinitions == null)
                forwardReferenceDefinitions = new List<StreamLabel>();

            int idx = (int)forwardReference;
            while (forwardReferenceDefinitions.Count <= idx)
                forwardReferenceDefinitions.Add(StreamLabel.Invalid);

            // If it is already defined, it better match! 
            Debug.Assert(forwardReferenceDefinitions[idx] == StreamLabel.Invalid ||
                forwardReferenceDefinitions[idx] == definitionLabel);

            // Define the forward forwardReference
            forwardReferenceDefinitions[idx] = definitionLabel;
        }

        private Tags ReadTag()
        {
#if DEBUG
            StreamLabel label = reader.Current;
#endif
            Tags tag = (Tags)reader.ReadByte();
#if DEBUG
            Log("<ReadTag Type=\"" + tag + "\" Value=\"" + ((int)tag).ToString() + "\" StreamLabel=\"0x" + label.ToString("x") + "\"/>");
#endif
            Debug.Assert(Tags.Error < tag && tag < Tags.Limit);
            return tag;
        }

        private SerializationType typeBeingRead;
        internal IStreamReader reader;
        internal IFastSerializable entryObject;
        internal IDictionary<StreamLabel, IFastSerializable> ObjectsInGraph;
        internal IDictionary<ForwardReference, IFastSerializable> unInitializedForwardReferences;
        internal List<StreamLabel> forwardReferenceDefinitions;
        internal bool allowLazyDeserialization;
        /// <summary>
        /// When we encounter a forward reference, we can either go to the forward reference table immediately and resolve it 
        /// (deferForwardReferences == false), or simply remember that that position needs to be fixed up and continue with
        /// the deserialization.   This later approach allows 'no seek' deserialization.   This variable which scheme we do. 
        /// </summary>
        internal bool deferForwardReferences;
        private Dictionary<string, Func<IFastSerializable>> factories;
        Func<Type, IFastSerializable> defaultFactory;
        #endregion
        internal Func<IFastSerializable> GetFactory(string fullName)
        {
            Func<IFastSerializable> ret;
            if (factories.TryGetValue(fullName, out ret))
                return ret;

            Type type = Type.GetType(fullName);      // TODO need assembly Info
            if (type == null)
                throw new TypeLoadException("Could not find type " + fullName);
            return delegate
            {
                // If we have a default factory, use it.  
                if (defaultFactory != null)
                {
                    IFastSerializable instance = defaultFactory(type);
                    if (instance != null)
                        return instance;
                }
                // Factory of last resort.  
                try
                {
                    return (IFastSerializable)Activator.CreateInstance(type);
                }
                catch (MissingMethodException)
                {
                    throw new SerializationException("Failure deserializing " + type.FullName +
                        ".\r\nIt must either have a parameterless constructor or been registered with the serializer.");
                }
            };
        }
    };


    /// <summary>
    /// #DeferedRegionOverview. 
    /// 
    /// A DeferedRegion help make 'lazy' objects. You will have a DeferedRegion for each block of object you
    /// wish to independently decide whether to deserialize lazily (typically you have on per object however
    /// in the limit you can have one per field, it is up to you).
    /// 
    /// When you call code:DeferedRegion.Write you give it a delegate that will write all the defered fields.
    /// The Write operation will place a forward reference in the stream that skips all the fields written,
    /// then the fields themselves, then define the forward reference. This allows readers to skip the
    /// defered fields.
    /// 
    /// When you call code:DeferedRegion.Read  you also give it a delegate that reads all the defered fields.
    /// However when 'Read' instead of reading the fields it
    /// 
    ///     * remembers the deseializer, stream position, and reading delegate.
    ///     * it uses the forward refernece to skip the region.
    ///     
    /// When code:DeferedRegion.FinishRead is called, it first checks if the region was already restored. 
    /// If not it used the information to read in the defered region and returns.  Thus this FinishRead
    /// should be called before any deferred field is used.  
    /// </summary>
    public struct DeferedRegion
    {
        /// <summary>
        /// see code:#DeferedRegionOverview.  
        /// TODO more 
        /// </summary>
        public void Write(Serializer serializer, Action toStream)
        {
            serializer.Log("<DeferedRegion>\r\n");
            // We actually don't use the this pointer!  We did this for symetry with code:Read
            ForwardReference endRegion = serializer.GetForwardReference();
            serializer.Write(endRegion);        // Allow the reader to skip this. 
            toStream();                         // Write the defered data. 
            serializer.DefineForwardReference(endRegion);
            serializer.Log("</DeferedRegion>\r\n");
        }
        /// <summary>
        /// see code:#DeferedRegionOverview.  
        /// fromStream can be null, if FinishRead is never used.  
        /// TODO more 
        /// </summary>
        public void Read(Deserializer deserializer, Action fromStream)
        {
            Debug.Assert(this.fromStream == null);      // For now, don't call this more than once. 
            deserializer.Log("<DeferRegionRead StreamLabel=\"0x" + deserializer.Current.ToString("x") + "\">");
            ForwardReference endReference = deserializer.ReadForwardReference();
            this.deserializer = deserializer;
            this.startPosition = deserializer.Current;
            this.fromStream = fromStream;
            deserializer.Goto(endReference);
            deserializer.Log("</DeferRegionRead>");
        }
        public void FinishRead()
        {
            if (fromStream != null)
                FinishReadHelper();
        }
        public bool IsFinished { get { return fromStream == null; } }
        public void Dispose()
        {
            if (deserializer != null)
            {
                deserializer.Dispose();
                deserializer = null;
            }
        }

        public Deserializer Deserializer { get { return this.deserializer; } }
        public StreamLabel StartPosition { get { return this.startPosition; } }
        #region private
        /// <summary>
        /// This helper is just here to insure that FinishRead gets inlined 
        /// </summary>
        private void FinishReadHelper()
        {
            deserializer.Log("<DeferRegionFinish StreamLabelRef=\"0x" + startPosition.ToString("x") + "\">");
            deserializer.Goto(startPosition);
            fromStream();
            deserializer.Log("</DeferRegionFinish>");
            fromStream = null;      // Indicates we ran it. 
        }

        internal Deserializer deserializer;
        internal StreamLabel startPosition;
        internal Action fromStream;
        #endregion
    }

    /// <summary>
    /// A type can opt into being serializable by implementing code:IFastSerializable and a default constructor
    /// (constructor that takes not arguments).
    /// 
    /// Conceputally all clients of code:IFastSerializable also implement code:IFastSerializableVersion
    /// however the serializer will assume a default implementation of code:IFastSerializableVersion (that
    /// returns version 1 and assumes all versions are allowed to deserialize it.  
    /// </summary>
    public interface IFastSerializable
    {
        /// <summary>
        /// Given a Serializer, write youself to the output stream. Conceptually this routine is NOT
        /// responsible for serializing its type information but only its field values. However it is
        /// conceptually responsible for the full transitive closure of its fields.
        /// 
        /// * For primitive fields, the choice is easy, simply call code:Serializer.Write
        /// * For object fields there is a choice
        ///     * If is is only referneces by the enclosing object (eg and therefore field's lifetime is
        ///         identical to referencing object), then the code:Serialize.WritePrivateObject can be
        ///         used.  This skips placing the object in the interning table (that insures it is written
        ///         exactly once).  
        ///     * Otherwise call code:Serialize.WriteObject
        /// * For value type fields (or collections of structs), you serialize the component fields.  
        /// * For collections, typically you serialize an integer inclusiveCountRet followed by each object. 
        /// </summary>
        void ToStream(Serializer serializer);
        /// <summary>
        /// 
        /// Given a reader, and a 'this' instance, made by calling the default constructor, create a fully
        /// initialized instance of the object from the reader stream.  The deserializer provides the extra
        /// state needed to do this for cyclic object graphs.  
        /// 
        /// Note that it is legal for the instance to cache the deserializer and thus be 'lazy' about when
        /// the actual deserialization happens (thus large persisted strucutre on the disk might stay on the
        /// disk).  
        /// 
        /// Typically the FromStream implementation is an exact mirror of the ToStream implementation, where
        /// there is a Read() for every Write(). 
        /// </summary>
        void FromStream(Deserializer deserializer);
    }

    // TODO fix the versioning so you don't have to create an instance of the type on serialization. 
    /// <summary>
    /// Objects implement code:IFastSerializableVersion to indicate what the current version is for writing
    /// and which readers can read the curent version.   If this interface is not implemented a default is
    /// provided (assuming version 1 for writing and MinimumVersion = 0).  
    /// 
    /// By default code:Serializer.WriteObject will place marks when the object ends and always skip to the
    /// end even if the FromStream did not read all the object data.   This allows considerable versioning
    /// flexibilty.  Simply by placing the new data at the end of the existing serialization, new versions
    /// of the type can be read by OLD deserializers (new fields will have the value determined by the
    /// default constructor (typically 0 or null).  This makes is relatively easy to keep MinimumVersion = 0
    /// (the ideal case).  
    /// </summary>
    public interface IFastSerializableVersion
    {
        /// <summary>
        /// This is the version number for the serialization format.  It should be incremented whenever a
        /// changes is made to code:IFastSerializable.ToStream and the format is publicly diseminated.  It
        /// must not vary from instance to instance.  This is pretty straightforward.  
        /// </summary>
        int Version { get; }

        /// <summary>
        /// At some point typically you give up allowing new versions of the read to read old wire formats
        /// This is the Minimum version of the serialized data that this reader can deserialize.   Trying
        /// to read wire formats strictly smaller (older) than this will fail.   Setting this to the current
        /// version indicates that you don't care about ever reading data generated with an older version
        /// of the code.  
        /// 
        /// If you set this to something other than your current version, you are obligated to insure that
        /// your FromStream() method can handle all formats >= than this number. 
        ///
        /// You can achive this if you simply use the 'WriteTagged' and 'ReadTagged' APIs in your 'ToStream' 
        /// and 'FromStream' after your V1 AND you always add new fields to the end of your class.   
        /// This is the best practice.   Thus  
        /// 
        ///     void IFastSerializable.ToStream(Serializer serializer)
        ///     {
        ///         serializer.Write(Ver_1_Field1);
        ///         serializer.Write(Ver_1_Field2);
        ///         // ...
        ///         serializer.WriteTagged(Ver_2_Field1);   
        ///         serializer.WriteTagged(Ver_2_Field2);
        ///         // ...
        ///         serializer.WriteTagged(Ver_3_Field1);
        ///     }
        /// 
        ///     void IFastSerializable.FromStream(Deserializer deserializer)
        ///     {
        ///         deserializer.Read(out Ver_1_Field1);
        ///         deserializer.Read(out Ver_1_Field2);
        ///         // ...
        ///         deserializer.ReadTagged(ref Ver_2_Field1);  // If data no present (old format) then Ver_2_Field1 not set.
        ///         deserializer.ReadTagged(ref Ver_2_Field2);  // ditto...
        ///         // ...
        ///         deserializer.ReadTagged(ref Ver_3_Field1);    
        ///     } 
        /// 
        /// Tagging outputs a byte tag in addition to the field itself.   If that is a problem you can also use the
        /// VersionBeingRead to find out what format is being read and write code that explicitly handles it.  
        /// Note however that this only gets you Backward compatibility (new readers can read the old format, but old readers 
        /// will still not be able to read the new format), which is why this is not the preferred method.  
        /// 
        ///     void IFastSerializable.FromStream(Deserializer deserializer)
        ///     {
        ///         // We assme that MinVersionCanRead == 4
        ///         // Deserialize things that are common to all versions (4 and earlier) 
        ///         
        ///         if (deserializer.VersionBeingRead >= 5)
        ///         {
        ///             deserializer.Read(AVersion5Field);
        ///             if (deserializer.VersionBeingRead >= 5)
        ///                 deserializer.ReadTagged(AVersion6Field);    
        ///         }
        ///     }
        /// </summary>
        int MinimumVersionCanRead { get; }
        /// <summary>
        /// This is the minimum version of a READER that can read this format.   If you don't support forward
        /// compatibilty (old readers reading data generated by new readers) then this should be set to 
        /// the current version.  
        /// 
        /// If you set this to something besides the current version you are obligated to insure that your
        /// ToStream() method ONLY adds fields at the end, AND that all of those added fields use the WriteTagged()
        /// operations (which tags the data in a way that old readers can skip even if they don't know what it is)
        /// In addition your FromStream() method must read these with the ReadTagged() deserializer APIs.  
        /// 
        /// See the comment in front of MinimumVersionCanRead for an example of using the WriteTagged() and ReadTagged() 
        /// methods. 
        /// </summary>
        int MinimumReaderVersion { get; }
    }

    public class SerializationException : Exception
    {
        public SerializationException(string message)
            : base(message)
        {
        }
    }

}
