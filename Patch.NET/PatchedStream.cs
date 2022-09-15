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
        private long _position;
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
                if (_position > Length) { _position = Length; }
                return _position;
            }
            set
            {
                _check();
                if (value > Length)
                {
                    throw new InvalidOperationException("Cannot set position after eof");
                }
                _position = value;
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
            lock (_provider)
            {

                _provider.Seek(Position);
                var read = _provider.Read(buffer, offset, count);
                Position += read;
                return read;
            }
        }
        public override int ReadByte()
        {
            _check();
            lock (_provider)
            {
                _provider.Seek(Position);
                var i = _provider.ReadByte();
                _position++;
                return i;
            }
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            _check();
            switch (origin)
            {
                case SeekOrigin.Begin: Position = offset; break;
                case SeekOrigin.Current: Position += offset; break;
                case SeekOrigin.End: Position = Length + offset; break;
                default: throw new NotSupportedException();
            }
            return Position;
        }

        public override void SetLength(long value)
        {
            _check();
            lock(_provider){
                _provider.SetLength(value);
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _check();
            lock(_provider){
                _provider.Seek(Position);
                _provider.Write(buffer, offset, count);
            }
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
