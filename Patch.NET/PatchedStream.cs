using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PatchDotNet
{
    public class PatchedStream : Stream
    {
        private readonly FileProvider _provider;
        private readonly Action<int> _free;
        private readonly int _handle;
        private bool _disposed = false;
        public int Handle => _handle;
        internal PatchedStream(FileProvider provider, int fileHandle, Action<int> freeCallback)
        {
            _provider = provider;
            _free = freeCallback;
            _handle = fileHandle;
        }
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite
        {
            get
            {
                _check();
                return _provider.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                _check();
                return _provider.Length;
            }
        }
        public override long Position
        {
            get
            {
                _check();
                return _provider.Position;
            }
            set
            {
                _check();
                _provider.Seek(value);
            }
        }
        public override void Flush()
        {
            _check();
            _provider.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            _check();
            return _provider.Read(buffer, offset, count);
        }
        public override int ReadByte()
        {
            _check();
            return _provider.ReadByte();
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            _check();
            long pos;
            switch (origin)
            {
                case SeekOrigin.Begin: pos = offset; break;
                case SeekOrigin.Current: pos = _provider.Position + offset; break;
                case SeekOrigin.End: pos = _provider.Length + offset; break;
                default: throw new NotSupportedException();
            }
            if (!_provider.Seek(pos))
            {
                throw new InvalidOperationException("Cannot seek to position beyond this stream");
            }
            return pos;
        }

        public override void SetLength(long value)
        {
            _check();
            _provider.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _check();
            _provider.Write(Position, buffer, offset, count);
        }

        public override void Close()
        {
            _check();
            _provider.Flush();
            _disposed = true;
            _free(_handle);
        }
        void _check()
        {
            if (_disposed)
            {
                throw new InvalidOperationException("Cannot access a disposed or closed stream");
            }
        }
        internal void Invalidate()
        {
            _disposed = true;
        }
    }
}
