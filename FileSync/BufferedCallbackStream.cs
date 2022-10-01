using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FileSync
{
    internal class BufferedCallbackStream : Stream
    {
        public delegate void WriteCallback(ReadOnlySpan<byte> data);
        public event WriteCallback OnFlush;
        byte[] _buffer;
        int _index = 0;
        public BufferedCallbackStream(byte[] buffer)
        {
            if (buffer == null) { throw new ArgumentNullException(); }
            _buffer = buffer;

        }
        public BufferedCallbackStream(int bufferSize = 4096) : this(new byte[bufferSize]) { }
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            OnFlush?.Invoke(new(_buffer, 0, _index));
            _index = 0;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            for (int written = 0; written < count; written++)
            {
                _buffer[_index] = buffer[offset + written];
                _index++;
                if (_index >= _buffer.Length)
                {
                    Flush();
                }
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (_buffer != null)
            {
                if (disposing)
                {
                    Flush();
                }

                _buffer = null;
            }

            // Call base class implementation.
            base.Dispose(disposing);
        }
    }
}
