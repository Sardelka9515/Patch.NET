using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.AccessControl;
namespace PatchDotNet
{
    public class FileProvider : FileMapper, IDisposable
    {
        public FileInfo FileInfo=new FileInfo(){
          Attributes=FileAttributes.Normal,
          CreationTime=DateTime.MinValue,
          LastAccessTime=DateTime.MinValue,
          LastWriteTime=DateTime.MinValue, 
        };
        Patch Current;
        List<Patch> Patches = new List<Patch>();
        Dictionary<int, RoWStream> _streams = new();
        int _streamHandle = 0;
        public bool CanWrite => Current.CanWrite;
        public FileProvider(string baseFile, bool canWrite, params string[] patches) : base(File.OpenRead(baseFile))
        {
            for (int i = 0; i < patches.Length; i++)
            {
                Console.WriteLine("Reading records from " + patches[i]);
                var patch = new Patch(patches[i], i == patches.Length - 1 && canWrite);
                Patches.Add(patch);
                int read = 0;
                while (patch.ReadRecord(out var type, out var vPosOrSize, out var readPos, out var chunkLen))
                {
                    // Console.WriteLine($"{type} {vPosOrSize} {chunkLen} {readPos}");
                    if (type == RecordType.Write)
                    {
                        MapRecord(vPosOrSize, readPos, chunkLen, patch.Reader.BaseStream, false);
                    }
                    else if (type == RecordType.SetLength)
                    {
                        Resize(vPosOrSize);
                    }
                    read++;

                }
                Console.WriteLine("Read " + read + " records");
            }
            Current = Patches[Patches.Count - 1];
        }
        public RoWStream GetStream()
        {
            lock (_streams)
            {
                var s = new RoWStream(this, _streamHandle);
                _streams.Add(_streamHandle, s);
                Console.WriteLine("Stream created " +_streamHandle);
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
            // Console.WriteLine($"Writing data: {Position}, {buffer.Length}, {count}");
            if (Position > Length)
            {
                throw new InvalidOperationException("Cannot write data beyond end of the file");
            }
            if(count<=0){
                throw new InvalidOperationException("Byte count to write must be greater than zero");
            }
            MapRecord(Position, Current.Write(Position, buffer, index, count), count, Current.Reader.BaseStream, true);
        }
        internal bool Seek(long pos)
        {
            // Console.WriteLine($"Seeking position from {Position} to {pos}");
            if (pos > Length)
            {
                return false;
            }
            else if (pos == Length)
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
        internal void SetLength(long newLen){
            Resize(newLen);
            Current.WriteResize(newLen);
        }

        protected void Resize(long newLength)
        {
            var current = Length;
            if (current == newLength) { return; }
            // Console.WriteLine($"============Resizing file from {current} to {newLength}===================");
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
                var count=Fragments.Count - index;
                Fragments.RemoveRange(index, count);
                // Console.WriteLine($"Removed {count} frags after eof, remaining: {Fragments.Count}");
                if(Fragments.Count==0){Fragments.Add(new FileFragement{StartPosition=0,EndPosition=-1,Stream=null});}
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
            for(int i = 0;i< Fragments.Count; i++)
            {
                if (i > 0)
                {
                    var last = Fragments[i - 1];
                    var frag = Fragments[i];
                    if (last.EndPosition + 1 != frag.StartPosition) { throw new Exception("Corrupted fragment at: "+i); }
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
            Current.Writer.Flush();
        }
        public void Dispose()
        {
            Patches.ForEach(p => p.Dispose());
            lock (_streams)
            {
                foreach (var s in _streams.Values)
                {
                    s.Invalidate();
                }
            }
            _baseStream?.Close();
            _baseStream?.Dispose();
        }
    }
}
