//     Copyright (c) Microsoft Corporation.  All rights reserved.
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// 
using System;
using System.Text;      // For StringBuilder.
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using DeferedStreamLabel = FastSerialization.StreamLabel;

namespace FastSerialization
{
    /// <summary>
    /// A MemoryStreamReader is an implementation of the IStreamReader interface that works over a given byte[] array.  
    /// </summary>
    public class MemoryStreamReader : IStreamReader
    {
        public MemoryStreamReader(byte[] data) : this(data, 0, data.Length) { }
        public MemoryStreamReader(byte[] data, int start, int length)
        {
            bytes = data;
            position = start;
            endPosition = length;
        }
        public byte ReadByte()
        {
            if (position >= endPosition)
                Fill(1);
            return bytes[position++];
        }
        public short ReadInt16()
        {
            if (position + sizeof(short) > endPosition)
                Fill(sizeof(short));
            int ret = bytes[position] + (bytes[position + 1] << 8);
            position += sizeof(short);
            return (short)ret;
        }
        public int ReadInt32()
        {
            if (position + sizeof(int) > endPosition)
                Fill(sizeof(int));
            int ret = bytes[position] + ((bytes[position + 1] + ((bytes[position + 2] + (bytes[position + 3] << 8)) << 8)) << 8);
            position += sizeof(int);
            return ret;
        }
        public StreamLabel ReadLabel()
        {
            return (StreamLabel)ReadInt32();
        }
        public virtual void GotoSuffixLabel()
        {
            Goto((StreamLabel)(Length - sizeof(StreamLabel)));
            Goto(ReadLabel());
        }

        public long ReadInt64()
        {
            if (position + sizeof(long) > endPosition)
                Fill(sizeof(long));
            uint low = (uint)ReadInt32();
            uint high = (uint)ReadInt32();
            return (long)((((ulong)high) << 32) + low);        // TODO find the most efficient way of doing this. 
        }
        public string ReadString()
        {
            if (sb == null)
                sb = new StringBuilder();
            sb.Length = 0;

            int len = ReadInt32();          // Expect first a character inclusiveCountRet.  -1 means null.
            if (len < 0)
            {
                Debug.Assert(len == -1);
                return null;
            }

            Debug.Assert(len < Length);
            while (len > 0)
            {
                int b = ReadByte();
                if (b < 0x80)
                    sb.Append((char) b);
                else if (b < 0xE0)
                {
                    // TODO test this for correctness
                    b = (b & 0x1F);
                    b = b << 6 + (ReadByte() & 0x3F);
                    sb.Append((char)b);
                }
                else
                {
                    // TODO test this for correctness
                    b = (b & 0xF);
                    b = b << 6 + (ReadByte() & 0x3F);
                    b = b << 6 + (ReadByte() & 0x3F);
                    sb.Append((char)b); 
                }
                --len;
            }
            return sb.ToString();
        }
        public virtual void Goto(StreamLabel label)
        {
            Debug.Assert(label != StreamLabel.Invalid);
            position = (int)label;
        }
        public virtual StreamLabel Current
        {
            get
            {
                return (StreamLabel)position;
            }
        }
        public virtual long Length { get { return endPosition; } }
        public virtual void Skip(int byteCount)
        {
            Goto((StreamLabel)((int)Current + byteCount));
        }
        void IDisposable.Dispose() { }
        protected virtual void Fill(int minBytes)
        {
            throw new Exception("Streamreader read past end of buffer");
        }
        protected byte[] bytes;
        protected int position;
        protected int endPosition;
        private StringBuilder sb;
    }

    // TODO is unsafe code worth it?
#if true
    /// <summary>
    /// A StreamWriter is an implementation of the IStreamWriter interface that generates a byte[] array. 
    /// </summary>
    public class MemoryStreamWriter : IStreamWriter
    {
        public MemoryStreamWriter() : this(64) { }
        public MemoryStreamWriter(int initialSize)
        {
            bytes = new byte[initialSize];
        }

        public virtual long Length { get { return endPosition; } }
        public virtual void Clear() { endPosition = 0; }

        public void Write(byte value)
        {
            if (endPosition >= bytes.Length)
                MakeSpace();
            bytes[endPosition++] = value;
        }
        public void Write(short value)
        {
            if (endPosition + sizeof(short) > bytes.Length)
                MakeSpace();
            int intValue = value;
            bytes[endPosition++] = (byte)intValue; intValue = intValue >> 8;
            bytes[endPosition++] = (byte)intValue; intValue = intValue >> 8;
        }
        public void Write(int value)
        {
            if (endPosition + sizeof(int) > bytes.Length)
                MakeSpace();
            bytes[endPosition++] = (byte)value; value = value >> 8;
            bytes[endPosition++] = (byte)value; value = value >> 8;
            bytes[endPosition++] = (byte)value; value = value >> 8;
            bytes[endPosition++] = (byte)value; value = value >> 8;
        }
        public void Write(long value)
        {
            if (endPosition + sizeof(long) > bytes.Length)
                MakeSpace();
            bytes[endPosition++] = (byte)value; value = value >> 8;
            bytes[endPosition++] = (byte)value; value = value >> 8;
            bytes[endPosition++] = (byte)value; value = value >> 8;
            bytes[endPosition++] = (byte)value; value = value >> 8;
            bytes[endPosition++] = (byte)value; value = value >> 8;
            bytes[endPosition++] = (byte)value; value = value >> 8;
            bytes[endPosition++] = (byte)value; value = value >> 8;
            bytes[endPosition++] = (byte)value; value = value >> 8;
        }
        public void Write(StreamLabel value)
        {
            Write((int)value);
        }
        public void Write(string value)
        {
            if (value == null)
            {
                Write(-1);          // negative charCount means null. 
            }
            else
            {
                Write(value.Length);
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (c < 128)
                        Write((byte)value[i]);                 // Only need one byte for UTF8
                    else if (c < 2048)
                    {
                        // TODO confirm that this is correct!
                        Write((byte) (0xC0 | (c >> 6)));                // Encode 2 byte UTF8
                        Write((byte) (0x80 | (c & 0x3F)));
                    }
                    else
                    {
                        // TODO confirm that this is correct!
                        Write((byte) (0xE0 | ((c >> 12) & 0xF)));        // Encode 3 byte UTF8
                        Write((byte) (0x80 | ((c >> 6) & 0x3F)));
                        Write((byte) (0x80 | (c & 0x3F)));
                    }
                }
            }
        }
        public virtual StreamLabel GetLabel()
        {
            return (StreamLabel)Length;
        }
        public void WriteSuffixLabel(StreamLabel value)
        {
            // This is guarenteed to be uncompressed, but since we are not compressing anything, we can
            // simply write the value.  
            Write(value);
        }

        public void WriteToStream(Stream outputStream)
        {
            // TODO really big streams will overflow;
            outputStream.Write(bytes, 0, (int)Length);
        }
        // Note that the returned MemoryStreamReader is not valid if more writes are done.  
        public MemoryStreamReader GetReader()
        {
            var readerBytes = bytes;
            if (bytes.Length - endPosition > 500000)
            {
                readerBytes = new byte[endPosition];
                Array.Copy(bytes, readerBytes, endPosition);
            }
            return new MemoryStreamReader(readerBytes, 0, endPosition);
        }
        public void Dispose() { }

        #region private
        protected virtual void MakeSpace()
        {
            byte[] newBytes = new byte[bytes.Length * 3 / 2];
            Array.Copy(bytes, newBytes, bytes.Length);
            bytes = newBytes;
        }
        protected byte[] bytes;
        protected int endPosition;
        #endregion
    }
#else 
    /// <summary>
    /// A StreamWriter is an implementation of the IStreamWriter interface that generates a byte[] array. 
    /// </summary>
    unsafe class MemoryStreamWriter : IStreamWriter
    {
        public MemoryStreamWriter() : this(64) { }
        public MemoryStreamWriter(int size)
        {
            bytes = new byte[size];
            pinningHandle = System.Runtime.InteropServices.GCHandle.Alloc(bytes, System.Runtime.InteropServices.GCHandleType.Pinned);
            fixed (byte* bytesAsPtr = &bytes[0])
                bufferStart = bytesAsPtr;
            bufferCur = bufferStart;
            bufferEnd = &bufferStart[bytes.Length];
        }

        // TODO 
        public virtual long Length { get { return (int) (bufferCur - bufferStart); } }
        public virtual void Clear() { throw new Exception(); }

        public void Write(byte value)
        {
            if (bufferCur + sizeof(byte) > bufferEnd)
                DoMakeSpace();
            *((byte*)(bufferCur)) = value;
            bufferCur += sizeof(byte);
        }
        public void Write(short value)
        {
            if (bufferCur + sizeof(short) > bufferEnd)
                DoMakeSpace();
            *((short*)(bufferCur)) = value;
            bufferCur += sizeof(short);
        }
        public void Write(int value)
        {
            if (bufferCur + sizeof(int) <= bufferEnd)
            {
                *((int*)(bufferCur)) = value;
                bufferCur += sizeof(int);
            }
            else 
                WriteSlow(value);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteSlow(int value)
        {
            DoMakeSpace();
            Debug.Assert(bufferCur + 8 < bufferEnd);
            Write(value);
        }

        public void Write(long value)
        {
            if (bufferCur + sizeof(long) > bufferEnd)
                DoMakeSpace();
            *((long*)(bufferCur)) = value;
            bufferCur += sizeof(long);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void DoMakeSpace()
        {
            endPosition = (int)(bufferCur - bufferStart);
            MakeSpace();
            bufferCur = &bufferStart[endPosition];
            bufferEnd = &bufferStart[bytes.Length];
        }

        public void Write(StreamLabel value)
        {
            Write((int)value);
        }
        public void Write(string value)
        {
            if (value == null)
            {
                Write(-1);          // negative bufferSize means null. 
            }
            else
            {
                Write(value.Length);
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (c < 128)
                        Write((byte)value[i]);                 // Only need one byte for UTF8
                    else if (c < 2048)
                    {
                        // TODO confirm that this is correct!
                        Write((byte) (0xC0 | (c >> 6)));                // Encode 2 byte UTF8
                        Write((byte) (0x80 | (c & 0x3F)));
                    }
                    else
                    {
                        // TODO confirm that this is correct!
                        Write((byte) (0xE0 | ((c >> 12) & 0xF)));        // Encode 3 byte UTF8
                        Write((byte) (0x80 | ((c >> 6) & 0x3F)));
                        Write((byte) (0x80 | (c & 0x3F)));
                    }
                }
            }
        }
        public virtual StreamLabel GetLabel()
        {
            return (StreamLabel)Length;
        }
        public void WriteSuffixLabel(StreamLabel value)
        {
            // This is guarenteed to be uncompressed, but since we are not compressing anything, we can
            // simply write the value.  
            Write(value);
        }

        public void WriteToStream(Stream outputStream)
        {
            // TODO really big streams will overflow;
            outputStream.Write(bytes, 0, (int)Length);
        }
        // Note that the returned MemoryStreamReader is not valid if more writes are done.  
        public MemoryStreamReader GetReader() { return new MemoryStreamReader(bytes); }
        public void Dispose() { }

    #region private
        protected virtual void MakeSpace()
        {
            byte[] newBytes = new byte[bytes.Length * 3 / 2];
            Array.Copy(bytes, newBytes, bytes.Length);
            bytes = newBytes;
            fixed (byte* bytesAsPtr = &bytes[0])
                bufferStart = bytesAsPtr;
            bufferCur = &bufferStart[endPosition];
            bufferEnd = &bufferStart[bytes.Length];
        }
        protected byte[] bytes;
        protected int endPosition;

        private System.Runtime.InteropServices.GCHandle pinningHandle;
        byte* bufferStart;
        byte* bufferCur;
        byte* bufferEnd;
    #endregion
    }
#endif

    /// <summary>
    /// A IOStreamStreamWriter hooks a MemoryStreamWriter up to an output System.IO.Stream
    /// </summary>
    public class IOStreamStreamWriter : MemoryStreamWriter, IDisposable
    {
        public IOStreamStreamWriter(string fileName) : this(new FileStream(fileName, FileMode.Create)) { }
        public IOStreamStreamWriter(Stream outputStream) : this(outputStream, defaultBufferSize + sizeof(long)) { }
        public IOStreamStreamWriter(Stream outputStream, int bufferSize)
            : base(bufferSize)
        {
            this.outputStream = outputStream;
            streamLength = outputStream.Length;
        }

        public void Flush()
        {
            outputStream.Write(bytes, 0, endPosition);
            streamLength += endPosition;
            endPosition = 0;
            outputStream.Flush();
        }

        /// <summary>
        /// You should avoid using this if at all possible.  
        /// </summary>
        public Stream RawStream { get { return outputStream; } }

        public void Close()
        {
            Flush();
            outputStream.Close();
        }
        public override long Length
        {
            get
            {
                Debug.Assert(streamLength == outputStream.Length);
                return base.Length + streamLength;
            }
        }
        public override StreamLabel GetLabel()
        {
            long len = Length;
            if (len != (uint)len)
                throw new NotSupportedException("Streams larger than 4Gig");
            return (StreamLabel)len;
        }
        public override void Clear()
        {
            outputStream.SetLength(0);
            streamLength = 0;
        }

        #region private
        protected override void MakeSpace()
        {
            Debug.Assert(endPosition > bytes.Length - sizeof(long));
            outputStream.Write(bytes, 0, endPosition);
            streamLength += endPosition;
            endPosition = 0;
        }
        void IDisposable.Dispose()
        {
            Close();
        }

        const int defaultBufferSize = 1024 * 8 - sizeof(long);
        Stream outputStream;
        long streamLength;

        #endregion
    }

    /// <summary>
    /// A IOStreamStreamReader hooks a MemoryStreamReader up to an input System.IO.Stream.  
    /// </summary>
    public class IOStreamStreamReader : MemoryStreamReader, IDisposable
    {
        public IOStreamStreamReader(string fileName) : this(
            new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read|FileShare.Delete)) { }
        public IOStreamStreamReader(Stream inputStream) : this(inputStream, defaultBufferSize) { }
        public IOStreamStreamReader(Stream inputStream, int bufferSize)
            : base(new byte[bufferSize + align], 0, 0)
        {
            Debug.Assert(bufferSize % align == 0);
            this.inputStream = inputStream;
        }
        public override StreamLabel Current
        {
            get
            {
                return (StreamLabel)(positionInStream + position);
            }
        }
        public override void Goto(StreamLabel label)
        {
            uint offset = (uint)label - positionInStream;
            if (offset > (uint)endPosition)
            {
                positionInStream = (uint)label;
                position = endPosition = 0;
            }
            else
                position = (int)offset;
        }

        public override long Length { get { return inputStream.Length; } }
        public void Close()
        {
            inputStream.Close();
        }

        #region private
        protected const int align = 8;        // Needs to be a power of 2
        protected const int defaultBufferSize = 0x4000;  // 16K 

        /// <summary>
        /// Fill the buffer, making sure at least 'minimum' byte are available to read.  Throw an exception
        /// if there are not that many bytes.  
        /// </summary>
        /// <param name="minimum"></param>
        protected override void Fill(int minimum)
        {
            if (endPosition != position)
            {
                int slideAmount = position & ~(align - 1);             // round down to stay aligned.  
                for (int i = slideAmount; i < endPosition; i++)        // Slide everything down.  
                    bytes[i - slideAmount] = bytes[i];
                endPosition -= slideAmount;
                position -= slideAmount;
                positionInStream += (uint)slideAmount;
            }
            else
            {
                positionInStream += (uint)position;
                endPosition = 0;
                position = 0;
                // if you are within one read of the end of file, go backward to read the whole block.  
                uint lastBlock = (uint)(((int)inputStream.Length - bytes.Length + align) & ~(align - 1));
                if (positionInStream >= lastBlock)
                    position = (int)(positionInStream - lastBlock);
                else
                    position = (int)positionInStream & (align - 1);
                positionInStream -= (uint)position;
            }

            Debug.Assert(positionInStream % align == 0);
            lock (inputStream)
            {
                inputStream.Seek(positionInStream + endPosition, SeekOrigin.Begin);
                for (; ; )
                {
                    System.Threading.Thread.Sleep(0);       // allow for Thread.Interrupt
                    int count = inputStream.Read(bytes, endPosition, bytes.Length - endPosition);
                    if (count == 0)
                        break;

                    endPosition += count;
                    if (endPosition == bytes.Length)
                        break;
                }
            }
            if (endPosition - position < minimum)
                throw new Exception("Read past end of stream.");
        }
        void IDisposable.Dispose()
        {
            Close();
        }

        protected Stream inputStream;
        protected uint positionInStream;
        #endregion
    }

    public unsafe sealed class PinnedStreamReader : IOStreamStreamReader
    {
        public PinnedStreamReader(string fileName, int bufferSize=defaultBufferSize)
            : this(new FileStream(fileName, FileMode.Open, FileAccess.Read, 
            FileShare.Read|FileShare.Delete), bufferSize) { }
        public PinnedStreamReader(Stream inputStream, int bufferSize=defaultBufferSize)
            : base(inputStream, bufferSize)
        {
            // Pin the array
            pinningHandle = System.Runtime.InteropServices.GCHandle.Alloc(bytes, System.Runtime.InteropServices.GCHandleType.Pinned);
            fixed (byte* bytesAsPtr = &bytes[0])
                bufferStart = bytesAsPtr;
        }
        public PinnedStreamReader Clone()
        {
            PinnedStreamReader ret = new PinnedStreamReader(inputStream, bytes.Length - align);
            return ret;
        }

        public unsafe byte* GetPointer(StreamLabel Position, int length)
        {
            Goto(Position);
            return GetPointer(length);
        }
        public unsafe byte* GetPointer(int length)
        {
            if (position + length > endPosition)
                Fill(length);
#if DEBUG
            fixed (byte* bytesAsPtr = &bytes[0])
                Debug.Assert(bytesAsPtr == bufferStart, "Error, buffer not pinnned");
            Debug.Assert(position < bytes.Length);
#endif
            return (byte*)(&bufferStart[position]);
        }

        #region private
        private System.Runtime.InteropServices.GCHandle pinningHandle;
        byte* bufferStart;
        #endregion
    }

#if PINNEDSTREAMREADER_TESTS
    public static class PinnedStreamTests
    {
        public static void Tests()
        {
            string testOrig = "text.orig";

            Random r = new Random(23);

            for (int j = 0; j < 10; j++)
            {
                for (int fileSize = 1023; fileSize <= 1025; fileSize++)
                {
                    CreateDataFile(testOrig, fileSize);
                    byte[] origData = File.ReadAllBytes(testOrig);

                    for (int bufferSize = 16; bufferSize < 300; bufferSize += 24)
                    {
                        FileStream testData = File.OpenRead(testOrig);
                        PinnedStreamReader reader = new PinnedStreamReader(testData, bufferSize);

                        // Try reading back in various seek positions. 
                        for (int i = 0; i < 100; i++)
                        {
                            int position = r.Next(0, origData.Length);
                            int size = r.Next(0, bufferSize) + 1;

                            reader.Goto((StreamLabel)position);
                            Compare(reader, origData, position, size);
                        }
                        reader.Close();
                    }
                }
                Console.WriteLine("Finished Round " + j);
            }
        }

        static int compareCount = 0;

        private static void Compare(PinnedStreamReader reader, byte[] buffer, int offset, int chunkSize)
        {
            compareCount++;
            if (compareCount == -1)
                Debugger.Break();

            for (int pos = offset; pos < buffer.Length; pos += chunkSize)
            {
                if (pos + chunkSize > buffer.Length)
                    chunkSize = buffer.Length - pos;
                CompareBuffer(reader.GetPointer(chunkSize), buffer, pos, chunkSize);
                reader.Skip(chunkSize);
            }
        }

        private unsafe static bool CompareBuffer(IntPtr ptr, byte[] buffer, int offset, int size)
        {
            byte* bytePtr = (byte*)ptr;

            for (int i = 0; i < size; i++)
            {
                if (buffer[i + offset] != bytePtr[i])
                {
                    Debug.Assert(false);
                    return false;
                }
            }
            return true;
        }
        private static void CreateDataFile(string name, int length)
        {
            FileStream stream = File.Open(name, FileMode.Create);
            byte val = 0;
            for (int i = 0; i < length; i++)
                stream.WriteByte(val++);
            stream.Close();
        }

    }
#endif
}
