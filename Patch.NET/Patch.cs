using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace PatchDotNet
{
    public enum RecordType
    {
        None,
        Write,
        SetLength
    }
    public class Record
    {
        public RecordType Type;
        public long Position;
        public int Length;
        public long ReadPosition;
    }
    public class Patch : IDisposable
    {
        #region HEADER
        const int Generation = 1;
        public Guid Guid { get; private set; }
        #endregion
        public bool CanWrite => Writer != null;
        private readonly FileStream _stream;
        public readonly BinaryReader Reader;
        public readonly BinaryWriter Writer;
        private long _lastVirtualPosition = -1;
        private int _lastChunkSize;
        private bool _lastResize=false;
        public Patch(string path, bool canWrite)
        {
            _stream = new FileStream(path, FileMode.OpenOrCreate, canWrite ? FileAccess.ReadWrite : FileAccess.Read, FileShare.None);
            _stream.Seek(0, SeekOrigin.Begin);
            Reader = new BinaryReader(_stream);
            Writer = canWrite ? new BinaryWriter(_stream) : null;

            // Initialize file
            if (_stream.Length == 0)
            {
                Writer.Write(Generation);
                Writer.Write((Guid = Guid.NewGuid()).ToString());
            }
            else
            {
                var gen = Reader.ReadInt32();
                if (gen != Generation)
                {
                    throw new NotSupportedException("Unsupported generation or file format: " + gen);
                }
                Guid = Guid.Parse(Reader.ReadString());
            }
        }

        public long Write(long virtualPosition, byte[] buffer,int index,int count)
        {
            // if (Writer == null) { throw new InvalidOperationException("Patch does not support writing"); }

            // sequential write defragmention
            if (virtualPosition > 0 && virtualPosition == _lastVirtualPosition + _lastChunkSize && (_lastChunkSize + (long)count) < int.MaxValue && !_lastResize)
            {
                // Seek to last record for modifying chunk length
                Writer.Seek(-(_lastChunkSize + sizeof(int)), SeekOrigin.End);

#if DETAILED_TRACE
                Console.WriteLine($"merging chunk with previous record: {_lastVirtualPosition}, {_lastChunkSize}, {virtualPosition}");

                // Check
                var recPos = Writer.Seek(-sizeof(long), SeekOrigin.Current);
                if (Reader.ReadInt64() != _lastVirtualPosition || Reader.ReadInt32() != _lastChunkSize)
                {
                    throw new Exception("Data didn't match");
                }
                Writer.Seek(-sizeof(int), SeekOrigin.Current);
#endif

                // Modify chunk size
                Writer.Write(_lastChunkSize = _lastChunkSize + count);

                // Finally, write chunk to the end
                Writer.Seek(0, SeekOrigin.End);
                Writer.Write(buffer,index,count);


#if DETAILED_TRACE
                _stream.Position = recPos;
                ReadRecord(out _, out var pos, out var readPos, out var size);
                if (pos != _lastVirtualPosition || size != _lastChunkSize) 
                { throw new Exception("Error when checking data"); }
                Console.WriteLine("Data OK");
#endif
            }
            else
            {

                Writer.Seek(0, SeekOrigin.End);
                Writer.Write(virtualPosition);
                Writer.Write(count);
                Writer.Write(buffer, index, count);
                _lastVirtualPosition = virtualPosition;
                _lastChunkSize = count;
                _lastResize = false;
            }
            return _stream.Position - count;
        }
        public byte[] ReadBytes(long readPos, int count)
        {
            _stream.Position = readPos;
            return Reader.ReadBytes(count);
        }
        public void WriteResize(long newSize)
        {
            Writer.Seek(0, SeekOrigin.End);
            Writer.Write(newSize);

            // indicates that this is a resize record
            Writer.Write(0);
            _lastResize=true;
        }

        /// <summary>
        /// (Write, position, readPosition, length), (SetLength, newlength, 0, 0)
        /// 
        /// Write record schema:
        /// {long:virtualPosition}{int:chunkLength}{byte[legnth]:chunk}
        /// 
        /// SetLength record schema:
        /// {long:newLength}{int=0}
        /// 
        /// </summary>
        /// <returns></returns>
        public bool ReadRecord(out RecordType type, out long virtualPositionOrNewSize, out long readPosition, out int chunkLength)
        {
            if (_stream.Position==_stream.Length)
            {
                type = RecordType.None;
                virtualPositionOrNewSize = 0;
                readPosition = 0;
                chunkLength = 0;
                return false;
            }


            // virtual file position or new length
            virtualPositionOrNewSize = Reader.ReadInt64();

            // chunk length
            chunkLength = Reader.ReadInt32();

            // SetLength record
            if (chunkLength == 0)
            {
                type = RecordType.SetLength;
                readPosition = 0;
            }
            else
            {
                type = RecordType.Write;
                readPosition = _stream.Position;

                // Seek position for reading next record
                try
                {

                    _stream.Seek(chunkLength, SeekOrigin.Current);
                }
                catch
                {
                    Console.WriteLine($"{chunkLength}, {_stream.Position}/{_stream.Length}");
                    throw;
                }
            }


            return true;
        }

        public void Dispose()
        {
            _stream.Close();
            _stream.Dispose();
        }
    }
}
