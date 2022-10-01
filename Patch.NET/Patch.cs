using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

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
        public const int HeaderSize = 4096;

        [HeaderOffset(0)]
        const int Generation = 2;

        [HeaderOffset(4)]
        public Guid Guid
        {
            get => new(GetHeader(4, 16));
            set => SetHeader(4, value.ToByteArray());
        }

        [HeaderOffset(20)]
        public Guid Parent
        {
            get => new(GetHeader(20, 16));
            set => SetHeader(20, value.ToByteArray());
        }

        [HeaderOffset(36)]
        public DateTime LastDefragmented
        {
            get => DateTime.FromBinary(BitConverter.ToInt64(GetHeader(36, 8))).ToLocalTime();
            set => SetHeader(36, BitConverter.GetBytes(value.ToUniversalTime().ToBinary()));
        }

        [HeaderOffset(44)]
        public FileAttributes Attributes
        {
            get => (FileAttributes)BitConverter.ToInt32(GetHeader(44, 4));
            set => SetHeader(44, BitConverter.GetBytes((int)value));
        }
        [HeaderOffset(48)]
        public long ParentLength
        {
            get => BitConverter.ToInt64(GetHeader(48, 8));
            set => SetHeader(48, BitConverter.GetBytes(value));
        }

        [HeaderOffset(1024)]
        public string Name
        {
            get => GetHeaderString(1024);
            set => SetHeader(1024, value);
        }
        #endregion

        public string Path => (_stream as FileStream).Name;
        public bool CanWrite => Writer != null;
        public DateTime CreationTime => _info.CreationTime;
        public DateTime LastAccessTime => _info.LastAccessTime;
        public DateTime LastWriteTime => _info.LastWriteTime;
        public DateTime CreationTimeUtc => _info.CreationTimeUtc;
        public DateTime LastAccessTimeUtc => _info.LastAccessTimeUtc;
        public DateTime LastWriteTimeUtc => _info.LastWriteTimeUtc;
        private Stream _stream;
        public BinaryReader Reader;
        public BinaryWriter Writer;
        private long _lastVirtualPosition = -1;
        private int _lastChunkSize;
        private bool _lastWrite = false;
        private FileInfo _info => new(Path);
        /// <summary>
        /// Gets the total amount of records in this patch, will only be valid after calling <see cref="ReadAllRecords(Action{long, long, int}, Action{long})"/> 
        /// </summary>
        public int RecordsCount { get; private set; }
        public long Length => _stream.Length;
        public Patch(string path, bool canWrite) : this(new FileStream(path, FileMode.OpenOrCreate, canWrite ? FileAccess.ReadWrite : FileAccess.Read, canWrite ? FileShare.Read : FileShare.ReadWrite))
        {
        }
        public Patch(Stream stream)
        {
            _stream = stream;
            var canWrite = _stream.CanWrite;
            InitStream(stream);
            // Initialize file
            if (_stream.Length == 0)
            {
                if (!canWrite)
                {
                    throw new FileNotFoundException("Specified patch was not found or empty.");
                }
                // offset 0
                Writer.Write(Generation);
                Guid = Guid.NewGuid();
                Parent = new Guid();
                LastDefragmented = DateTime.MinValue;
                _stream.SetLength(HeaderSize);
            }
            else
            {
                var gen = Reader.ReadInt32();
                if (gen != Generation)
                {
                    throw new NotSupportedException("Unsupported generation or file format: " + gen);
                }
            }
            ResetPointer();
        }
        internal void SetHeader(long offset, byte[] data)
        {
            lock (this)
            {
                var pos = _stream.Position;
                _stream.Seek(offset, SeekOrigin.Begin);
                _stream.Write(data, 0, data.Length);
                _stream.Position = pos;
            }
        }
        internal void SetHeader(long offset, string s, int maxBytes = 256)
        {
            lock (this)
            {

                var pos = _stream.Position;
                maxBytes -= sizeof(int);
                _stream.Seek(offset, SeekOrigin.Begin);
                var bytes = Encoding.UTF8.GetBytes(s);
                if (bytes.Length > maxBytes)
                {
                    throw new InvalidDataException($"String too large, must be less than {maxBytes} bytes.");
                }
                Writer.Write(bytes.Length);
                Writer.Write(bytes);
                _stream.Position = pos;
            }
        }
        internal byte[] GetHeader(long offset, int count)
        {
            lock (this)
            {
                var pos = _stream.Position;
                _stream.Seek(offset, SeekOrigin.Begin);
                var data = Reader.ReadBytes(count);
                _stream.Position = pos;
                return data;
            }
        }
        /// <summary>
        /// Set stream position to <see cref="HeaderSize"/> for reading records
        /// </summary>
        public void ResetPointer()
        {
            _stream.Position = HeaderSize;
        }
        internal string GetHeaderString(long offset)
        {
            lock (this)
            {
                var pos = _stream.Position;
                _stream.Seek(offset, SeekOrigin.Begin);
                var data = Encoding.UTF8.GetString(Reader.ReadBytes(Reader.ReadInt32()));
                _stream.Position = pos;
                return data;
            }
        }
        public void CreateChild(string path, string name)
        {
            if (File.Exists(path)) { throw new InvalidOperationException("File already exists: " + path); }
            var p = new Patch(path, true);
            CreateChild(p, name);
            p.Dispose();
        }
        public void CreateChild(Patch p, string name)
        {
            p.Parent = Guid;
            p.Attributes = Attributes;
            p.Name = name;
            p.ParentLength = Length;
        }
        public bool IsChildOf(Patch parent, long baseLength)
        {
            return parent == Parent && ParentLength == (parent?.Length ?? baseLength);
        }
        public void ReadAllRecords(Action<long, long, int> writeCallback, Action<long> resizeCallback, Func<long, long, bool> corruptConfirm = null)
        {
            lock (this)
            {
                RecordsCount = 0;
                long lastIntacPos = _stream.Position = HeaderSize;
                try
                {

                    while (ReadRecord(out var type, out var vPosOrSize, out var readPos, out var chunkLen))
                    {
                        // Console.WriteLine($"{read} {type} {vPosOrSize} {chunkLen} {readPos}");
                        if (type == RecordType.Write)
                        {
                            writeCallback?.Invoke(vPosOrSize, readPos, chunkLen);
                        }
                        else if (type == RecordType.SetLength)
                        {
                            resizeCallback?.Invoke(vPosOrSize);
                        }
                        RecordsCount++;
                        lastIntacPos = _stream.Position;
                    }
                }
                catch (Exception ex)
                {
                    if (corruptConfirm?.Invoke(lastIntacPos, _stream.Length)==true)
                    {
                        if (!_stream.CanWrite)
                        {
                            if (_stream is FileStream fs)
                            {
                                var path = fs.Name;
                                fs.Dispose();
                                InitStream(File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read));
                            }
                        }
                        else
                        {
                            throw new NotSupportedException("Stream does not support writing");
                        }
                        _stream.SetLength(lastIntacPos);
                        _stream.Position = lastIntacPos;
                    }
                    else
                    {
                        throw new PatchCorrutpedException(lastIntacPos, "Corrupted record after " + lastIntacPos, ex);
                    }
                }
                Console.WriteLine("Read " + RecordsCount + " records");
            }
        }
        void InitStream(Stream s)
        {
            _stream = s;
            Writer = s.CanWrite ? new(s) : null;
            Reader = new(s);
        }
        public long Write(long virtualPosition, byte[] buffer, int index, int count)
        {
            lock (this)
            {
                // if (Writer == null) { throw new InvalidOperationException("Patch does not support writing"); }

                // sequential write defragmention
                if (virtualPosition > 0 && virtualPosition == _lastVirtualPosition + _lastChunkSize && (_lastChunkSize + (long)count) < int.MaxValue && _lastWrite)
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
                    Writer.Write(buffer, index, count);


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
                    _lastWrite = true;
                    RecordsCount++;
                }
                return _stream.Position - count;
            }
        }
        public byte[] ReadBytes(long readPos, int count)
        {
            lock (this)
            {
                _stream.Position = readPos;
                return Reader.ReadBytes(count);
            }
        }
        public void WriteResize(long newSize)
        {
            lock (this)
            {

                Writer.Seek(0, SeekOrigin.End);
                Writer.Write(newSize);

                // indicates that this is a resize record
                Writer.Write(0);
                _lastWrite = false;
                RecordsCount++;
            }
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
            lock (this)
            {
                if (_stream.Position == _stream.Length)
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
        }

        public void Dispose()
        {
            _stream.Close();
            _stream.Dispose();
        }
        public static implicit operator Guid(Patch p)
        {
            return p?.Guid ?? Guid.Empty;
        }
    }


    public class PatchNode
    {
        public bool IsRoot = false;
        public Guid ID;
        public string Name;
        public string Path;
        public PatchNode Parent;
        public Guid ParentID;
        public DateTime LastDefragmented = DateTime.MinValue;
        public List<PatchNode> Children = new();
        public FileAttributes Attributes;
        private bool _needUpdateRecords = true;
        private long _recordsCount;
        public PatchNode(string path)
        {
            Path = path;
            Update();
        }
        public PatchNode() { IsRoot = true; }
        public long GetRecordsCount(Func<long, long, bool> corruptConfirm = null)
        {
            if (_needUpdateRecords)
            {
                _needUpdateRecords = false;
                if (IsRoot) { return 0; }
                using var p = new Patch(Path, false);
                p.ReadAllRecords(null, null, corruptConfirm);
                p.Dispose();
                return _recordsCount=p.RecordsCount;
            }
            else
            {
                return _recordsCount;
            }
        }
        public void Update(bool dispose = true)
        {
            if (IsRoot) { return; }
            var p = new Patch(Path, false);
            ID = p.Guid;
            Path = p.Path;
            Name = p.Name;
            ParentID = p.Parent;
            LastDefragmented = p.LastDefragmented;
            Attributes = p.Attributes;
            if (dispose)
            {
                p.Dispose();
            }
            _needUpdateRecords = true;
        }
        public static implicit operator Guid(PatchNode node)
        {
            return node?.ID ?? Guid.Empty;
        }
    }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class HeaderOffset : Attribute
    {
        public long Offset { get; private set; }
        public HeaderOffset(long offset)
        {
            Offset = offset;
        }
    }
}
