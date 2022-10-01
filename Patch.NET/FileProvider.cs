using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.AccessControl;
using System.Reflection.Emit;

namespace PatchDotNet
{
    public class FileProvider : FileMapper, IDisposable
    {
        public Guid CurrentGuid => Current.Guid;
        public FileAttributes Attributes
        {
            get => Current.Attributes | (CanWrite ? 0 : FileAttributes.ReadOnly);
            set => Current.Attributes = value;
        }
        public readonly DateTime CreationTime;
        public DateTime LastAccessTime => Current.LastAccessTime;
        public DateTime LastWriteTime => Current.LastWriteTime;
        public Patch[] Patches => _patches.ToArray();
        private Patch Current;
        private readonly List<Patch> _patches = new();
        private readonly Dictionary<int, RoWStream> _streams = new();
        private int _streamHandle = 0;
        readonly StreamWriter _debug;
        public readonly string BasePath;
        public bool CanWrite => Current?.CanWrite == true;
        public RoWStream[] Streams => _streams.Values.ToArray();
        public FileProvider(BlockMap mapping, DateTime creationTime, StreamWriter debugger = null) : base(mapping.BaseStream)
        {
            Fragments = new(mapping.Fragments);
            _patches = new(mapping.Patches);
            BasePath = (BaseStream as FileStream)?.Name;
            CreationTime = creationTime;
            _debug = debugger;
            Current = _patches.LastOrDefault();
        }
        public FileProvider(string basePath, bool canWrite, StreamWriter debugger, params string[] patches) :
            this(File.Open(basePath,FileMode.Open,FileAccess.Read,FileShare.Read), new FileInfo(basePath).CreationTime, debugger,
                patches.Select(x => new Patch(x, x == patches.Last() && canWrite)).ToArray())
        { }
        public FileProvider(Stream baseStream, DateTime creationTime, StreamWriter debugger = null, params Patch[] patches) : base(baseStream)
        {
            if (patches.Length == 0) { throw new InvalidOperationException("One or more patches must be specified"); }
            BasePath = (baseStream as FileStream)?.Name;
            CreationTime = creationTime;
            _debug = debugger;
            Patch parent = null;
            for (int i = 0; i < patches.Length; i++)
            {
                Console.WriteLine("Reading records from " + patches[i]);
                var patch = patches[i];
                if (!patch.IsChildOf(parent, BaseStream.Length))
                {
                    throw new BrokenPatchChainException($"The patch chain is broken at index {i}. Actual parent is {(Guid)parent}=>{parent?.Length ?? BaseStream.Length}, expecting {patch.Parent}=>{patch.ParentLength}");
                }
                _patches.Add(patch);
                parent = patch;
                patch.ReadAllRecords((pos, readPos, len) => MapRecord(pos, readPos, len, patch.Reader.BaseStream, false),
                    (size) => Resize(size));
            }
            Current = _patches.LastOrDefault();
            Check();
        }

        /// <summary>
        /// Change current patch and redirect subsequent records to new patch
        /// </summary>
        /// <param name="p"></param>
        public void ChangeCurrent(string path, string name)
        {
            lock (this)
            {
                Flush();
                Current.CreateChild(path, name);
                var p = new Patch(path, true);
                _patches.Add(p);
                Current.Writer.Flush();
                Current = p;
            }
        }
        public RoWStream GetStream()
        {
            lock (_streams)
            {
                var s = new RoWStream(this, _streamHandle);
                _streams.Add(_streamHandle, s);
                Console.WriteLine("Stream created " + _streamHandle);
                _streamHandle++;
                return s;
            }
        }
        internal void FreeStream(int handle)
        {
            _streams.Remove(handle);
            Console.WriteLine("Stream destroyed " + handle);
        }
        internal void Write(byte[] buffer, int index, int count)
        {
            _debug?.WriteLine($"Writing data: {Position}, {buffer.Length}, {count}");
            if (Position > Length)
            {
                throw new InvalidOperationException("Cannot write data beyond end of the file");
            }
            if (count <= 0)
            {
                throw new InvalidOperationException("Byte count to write must be greater than zero");
            }
            MapRecord(Position, Current.Write(Position, buffer, index, count), count, Current.Reader.BaseStream, true);
        }
        internal bool Seek(long pos)
        {
            if (pos > Length)
            {
                return false;
            }
            _debug?.WriteLine($"Seeking position from {Position} to {pos}, length: {Length}");
            if (pos == Length)
            {
                CurrentFragment = Fragments.Count;
            }
            else if (pos == Position)
            {
                return true;
            }
            else if (pos < Position)
            {
                if (CurrentFragment < Fragments.Count && pos >= Fragments[CurrentFragment].StartPosition)
                {
                    // Same fragment
                }
                else
                {

                    Seeker.StartPosition = pos;
                    CurrentFragment = Fragments.BinarySearch(0, CurrentFragment, Seeker, Comparer);
                }

            }
            else // pos > Position
            {
                Seeker.StartPosition = pos;
                CurrentFragment = Fragments.BinarySearch(CurrentFragment, Fragments.Count - CurrentFragment, Seeker, Comparer);
            }
            Position = pos;

            if (CurrentFragment < 0)
            {
                CurrentFragment = (~CurrentFragment) - 1;
            }
            CheckPosition();
            return true;
        }
        internal void SetLength(long newLen)
        {
            Resize(newLen);
            Current.WriteResize(newLen);
        }

        protected void Resize(long newLength)
        {
            var current = Length;
            if (current == newLength) { return; }
            _debug?.WriteLine($"============Resizing file from {current} to {newLength}===================");
            // DumpFragments();
            if (newLength > current)
            {
                Fragments.Add(new FileFragement
                {
                    StartPosition = current,
                    EndPosition = newLength - 1,
                });
                // Console.WriteLine("Added one frag to end of the file");
            }
            else
            {
                Seeker.StartPosition = newLength;
                var index = Fragments.BinarySearch(Seeker, Comparer);
                if (index < 0)
                {
                    index = ~index;
                    Fragments[index - 1].SetEnd(newLength - 1);
                }
                var count = Fragments.Count - index;
                Fragments.RemoveRange(index, count);
                // Console.WriteLine($"Removed {count} frags after eof, remaining: {Fragments.Count}");
                if (Fragments.Count == 0) { Fragments.Add(new FileFragement { StartPosition = 0, EndPosition = -1, Stream = null }); }
                if (Position > Length) { Position = Length; CurrentFragment = Fragments.Count - 1; }
            }

            // DumpFragments();
        }
        internal int Read(byte[] buffer, int startIndex, int count)
        {
            if (CurrentFragment > Fragments.Count - 1) { return 0; }
            int read = 0;
            int thisRead;
            CheckPosition();
            while (read < count && (thisRead = Fragments[CurrentFragment].Read(Position, buffer, startIndex + read, count - read)) != 0)
            {
                // Console.WriteLine($"Read {thisRead} bytes from frag {CurrentFragment}");
                Position += thisRead;
                read += thisRead;

                // Proceed to read next fragment
                if (Fragments[CurrentFragment].EndPosition < Position)
                {

                    CurrentFragment++;
                    if (CurrentFragment >= Fragments.Count)
                    {
                        // End-of-File

#if DEBUG
                        Console.WriteLine("EoF reached");
#endif

                        break;
                    }
                }
                CheckPosition();
            }
#if DEBUG
            // Console.WriteLine($"Requested {count} bytes, read {read}");
#endif
            return read;
        }
        public void Check()
        {
            for (int i = 0; i < Fragments.Count; i++)
            {
                if (i > 0)
                {
                    var last = Fragments[i - 1];
                    var frag = Fragments[i];
                    if (last.EndPosition + 1 != frag.StartPosition) { DumpFragments(); throw new Exception("Corrupted fragment at: " + i); }
                    else if (frag.EndPosition < frag.StartPosition - 1)
                    {
                        throw new Exception("invalid frag");
                    }
                }
                else if (Fragments[i].StartPosition != 0)
                {
                    throw new Exception("Invalid start fragment");
                }
            }
        }
        internal int ReadByte()
        {
            byte[] b = new byte[1];
            Read(b, 0, 1);
            return b[0];
        }
        public void Flush()
        {
            _debug?.Flush();
            Current.Writer?.Flush();
        }
        public void Dispose()
        {
            _patches.ForEach(p => p.Dispose());
            lock (_streams)
            {
                foreach (var s in _streams.Values)
                {
                    s.Invalidate();
                }
            }
            BaseStream?.Close();
            BaseStream?.Dispose();
            _debug?.Dispose();
        }

        /// <summary>
        /// Defragment and merge one or multiple patches
        /// </summary>
        /// <param name="level">Specify the level to merge, e.g. 1 to defragment current patch only.</param>
        /// <param name="output">The path to save the merged patch. Changes will not be applied to current patches</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
        public bool Merge(int level, string output, string name, Patch[] children, Func<int, int,long,long, bool> mergeConfirm = null)
        {
            lock (this)
            {
                var segments = GetMergeResult(level);
                var patches = _patches.Skip(_patches.Count - level);
                var mergedCount = segments.Count + 1;
                var currentCount = patches.Sum(x => x.RecordsCount);
                Console.WriteLine($"Scanning done, records count after merge: {mergedCount}");
                if (mergeConfirm?.Invoke(currentCount, mergedCount,patches.Sum(x=>x.Length),segments.Sum(x=>(long)(x.Item2 + sizeof(long) + sizeof(int)) + sizeof(long) + sizeof(int) + Patch.HeaderSize)) == false)
                {
                    return false;
                }
                else if (patches.Count() == 1 && currentCount <= mergedCount)
                {
                    Console.WriteLine("Last: " + patches.Last().Path);
                    Console.WriteLine($"There's no need to defragment, records count are the same or less: {currentCount}, {mergedCount}");
                    return false;
                }
                Console.WriteLine("Saving merge results...");
                if (File.Exists(output))
                {
                    throw new Exception("Output file already exists");
                }
                var pIndex = _patches.Count - level - 1;
                using var patch = new Patch(output, true);
                if (pIndex >= 0)
                {
                    _patches[pIndex].CreateChild(patch, name);
                }
                else
                {
                    // Parent is Base
                    patch.Name = name;
                    patch.Attributes = BasePath==null?FileAttributes.Archive:new FileInfo(BasePath).Attributes;
                    patch.Parent = Guid.Empty;
                    patch.ParentLength = BaseStream.Length;
                }
                patch.LastDefragmented = DateTime.Now;
                if (level == 1) { patch.Guid = Current.Guid; }

                patch.WriteResize(Length);
                foreach (var s in segments)
                {
                    var buffer = new byte[s.Item2];
                    Seek(s.Item1);
                    Read(buffer, 0, s.Item2);
                    patch.Write(s.Item1, buffer, 0, buffer.Length);
                }

                // Change ParentLength for chidren
                foreach (var ch in children)
                {
                    ch.ParentLength = patch.Length;
                }
                Console.WriteLine("Merge done");
                return true;
            }


        }
        public List<(long, int)> GetMergeResult(int level)
        {
            lock (this)
            {

                if (level <= 0)
                {
                    throw new InvalidOperationException("Merge level must be greater than zero");
                }
                var patches = _patches.Skip(_patches.Count - level);
                Console.WriteLine($"Scanning mergeable fragments with {patches.Count()} patches");
                Check();
                HashSet<Stream> streams = new(patches.Select(x => x.Reader.BaseStream));
                List<(long, int)> segments = new();
                for (int i = 0; i < Fragments.Count; i++)
                {
                    if (Fragments[i].Length > int.MaxValue || !streams.Contains(Fragments[i].Stream)) { continue; }
                    var start = Fragments[i].StartPosition;
                    long end = Fragments[i].EndPosition;
                    for (; i < Fragments.Count; i++)
                    {
                        if (streams.Contains(Fragments[i].Stream) && (Fragments[i].EndPosition - start + 1) <= int.MaxValue)
                        {
                            end = Fragments[i].EndPosition;
                        }
                        else
                        {
                            break;
                        }
                    }
                    segments.Add((start, (int)(end - start + 1)));
                }
                return segments;
            }
        }
    }
}
