// Copyright (c) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.

using System.IO;
using System.Text;

namespace Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
{
    /// <summary>
    /// Represents a file stream writer that keeps a tally of the length of the file.
    /// </summary>
    internal sealed class TallyKeepingFileStreamWriter : StreamWriter
    {
        private long tally;

        /// <summary>
        /// Initializes a new instance of the <see cref="TallyKeepingFileStreamWriter"/> class with a <see cref="FileStream"/>.
        /// </summary>
        /// <param name="stream">The <see cref="FileStream"/> to write to.</param>
        public TallyKeepingFileStreamWriter(FileStream stream)
            : base(stream)
        {
            this.tally = stream.Length;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TallyKeepingFileStreamWriter"/> class with a <see cref="FileStream"/>.
        /// </summary>
        /// <param name="stream">The <see cref="FileStream"/> to write to.</param>
        /// <param name="encoding">The <see cref="Encoding"/> to use.</param>
        public TallyKeepingFileStreamWriter(FileStream stream, Encoding encoding)
            : base(stream, encoding)
        {
            this.tally = stream.Length;
        }

        /// <summary>
        /// Gets the tally of the length of the string.
        /// </summary>
        /// <value>
        /// The tally of the length of the string.
        /// </value>
        public long Tally
        {
            get
            {
                return this.tally;
            }
        }

        /// <summary>
        /// Writes a character to the stream.
        /// </summary>
        /// <param name="value">The character to write to the text stream. </param>
        /// <exception cref="T:System.ObjectDisposedException"><see cref="P:System.IO.StreamWriter.AutoFlush"></see> is true or the <see cref="T:System.IO.StreamWriter"></see> buffer is full, and current writer is closed. </exception>
        /// <exception cref="T:System.NotSupportedException"><see cref="P:System.IO.StreamWriter.AutoFlush"></see> is true or the <see cref="T:System.IO.StreamWriter"></see> buffer is full, and the contents of the buffer cannot be written to the underlying fixed size stream because the <see cref="T:System.IO.StreamWriter"></see> is at the end the stream. </exception>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception><filterpriority>1</filterpriority>
        public override void Write(char value)
        {
            base.Write(value);
            this.tally += Encoding.GetByteCount(new char[] { value });
        }

        /// <summary>
        /// Writes a character array to the stream.
        /// </summary>
        /// <param name="buffer">A character array containing the data to write. If buffer is null, nothing is written. </param>
        /// <exception cref="T:System.ObjectDisposedException"><see cref="P:System.IO.StreamWriter.AutoFlush"></see> is true or the <see cref="T:System.IO.StreamWriter"></see> buffer is full, and current writer is closed. </exception>
        /// <exception cref="T:System.NotSupportedException"><see cref="P:System.IO.StreamWriter.AutoFlush"></see> is true or the <see cref="T:System.IO.StreamWriter"></see> buffer is full, and the contents of the buffer cannot be written to the underlying fixed size stream because the <see cref="T:System.IO.StreamWriter"></see> is at the end the stream. </exception>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception><filterpriority>1</filterpriority>
        public override void Write(char[] buffer)
        {
            base.Write(buffer);
            this.tally += Encoding.GetByteCount(buffer);
        }

        /// <summary>
        /// Writes the specified buffer to the stream.
        /// </summary>
        /// <param name="buffer">A character array containing the data to write.</param>
        /// <param name="index">The index into buffer at which to begin writing.</param>
        /// <param name="count">The number of characters to read from buffer.</param>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs.</exception>
        /// <exception cref="T:System.ObjectDisposedException"><see cref="P:System.IO.StreamWriter.AutoFlush"></see> is true or the <see cref="T:System.IO.StreamWriter"></see> buffer is full, and current writer is closed. </exception>
        /// <exception cref="T:System.NotSupportedException"><see cref="P:System.IO.StreamWriter.AutoFlush"></see> is true or the <see cref="T:System.IO.StreamWriter"></see> buffer is full, and the contents of the buffer cannot be written to the underlying fixed size stream because the <see cref="T:System.IO.StreamWriter"></see> is at the end the stream. </exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">index or count is negative. </exception>
        /// <exception cref="T:System.ArgumentException">The buffer length minus index is less than count. </exception>
        /// <exception cref="T:System.ArgumentNullException">buffer is null. </exception><filterpriority>1</filterpriority>
        public override void Write(char[] buffer, int index, int count)
        {
            base.Write(buffer, index, count);
            this.tally += Encoding.GetByteCount(buffer, index, count);
        }

        /// <summary>
        /// Writes a string to the stream.
        /// </summary>
        /// <param name="value">The string to write to the stream. If value is null, nothing is written. </param>
        /// <exception cref="T:System.ObjectDisposedException"><see cref="P:System.IO.StreamWriter.AutoFlush"></see> is true or the <see cref="T:System.IO.StreamWriter"></see> buffer is full, and current writer is closed. </exception>
        /// <exception cref="T:System.NotSupportedException"><see cref="P:System.IO.StreamWriter.AutoFlush"></see> is true or the <see cref="T:System.IO.StreamWriter"></see> buffer is full, and the contents of the buffer cannot be written to the underlying fixed size stream because the <see cref="T:System.IO.StreamWriter"></see> is at the end the stream. </exception>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception><filterpriority>1</filterpriority>
        public override void Write(string value)
        {
            base.Write(value);
            this.tally += Encoding.GetByteCount(value);
        }
    }
}
