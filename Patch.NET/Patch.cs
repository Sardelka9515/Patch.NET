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
    public class Patch
    {
        public bool CanWrite => Write != null;
        private readonly FileStream _stream;
        public readonly BinaryReader Reader;
        public readonly BinaryWriter Writer;
        private long _lastVirtualPosition = -1;
        private int _lastChunkSize;
        public Patch(string path, bool canWrite)
        {
            _stream = new FileStream(path, FileMode.OpenOrCreate, canWrite ? FileAccess.ReadWrite : FileAccess.Read, FileShare.None);
            Reader = new BinaryReader(_stream);
            Writer = canWrite ? new BinaryWriter(_stream) : null;
        }

        public long Write(long virtualPosition, byte[] chunk)
        {
            // if (Writer == null) { throw new InvalidOperationException("Snapshot does not support writing"); }

            // sequential write defragmention
            if (virtualPosition > 0 && virtualPosition == _lastVirtualPosition + _lastChunkSize)
            {
                // Seek to last record for modifying chunk length
                Writer.Seek(-(_lastChunkSize + sizeof(int)), SeekOrigin.End);

                // TODO: add intergrity check here

                // Modify chunk size
                Writer.Write(_lastChunkSize = _lastChunkSize + chunk.Length);

                // Finally, write chunk to the end
                Writer.Seek(0, SeekOrigin.End);
                Writer.Write(chunk, 0, chunk.Length);
            }
            else
            {

                Writer.Seek(0, SeekOrigin.End);
                Writer.Write(virtualPosition);
                Writer.Write(chunk.Length);
                Writer.Write(chunk, 0, chunk.Length);
                _lastVirtualPosition = virtualPosition;
                _lastChunkSize = chunk.Length;

            }
            return _stream.Position - chunk.Length;
        }
        public void Resize(long newSize)
        {
            Writer.Seek(0, SeekOrigin.End);
            Writer.Write(newSize);

            // indicates that this is a resize record
            Writer.Write(0);
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
            /*
            if (!_stream.CanRead)
            {
                type = RecordType.None;
                virtualPositionOrNewSize = 0;
                readPosition = 0;
                chunkLength = 0;
                return false;
            }
            */

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
                _stream.Seek(chunkLength, SeekOrigin.Current);
            }


            return _stream.CanRead;
        }
    }
}
