using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PatchDotNet
{
    public class FileProvider : FileMapper, IDisposable
    {
        Patch Current;
        List<Patch> Patches = new List<Patch>();
        Dictionary<int, PatchedStream> OpenedStreams = new();
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
        public PatchedStream GetStream()
        {
            lock (OpenedStreams)
            {
                var newHandle = OpenedStreams.Count > 0 ? OpenedStreams.Last().Value.Handle + 1 : 0;
                var s = new PatchedStream(this, newHandle, (h) =>
                {
                    lock (OpenedStreams)
                    {
                        OpenedStreams.Remove(newHandle);
                    }
                });
                OpenedStreams.Add(newHandle, s);
                return s;
            }
        }
        public void Write(long position, byte[] buffer, int index, int count)
        {
            if (position > Length)
            {
                throw new InvalidOperationException("Cannot write data beyond end of the file");
            }
            MapRecord(position, Current.Write(position, buffer, index, count), count, Current.Reader.BaseStream, true);
        }
        public bool Seek(long pos)
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
        public void SetLength(long newLen){
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
        public int Read(byte[] buffer, int startIndex, int count)
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
        public int ReadByte()
        {
            byte[] b = new byte[1];
            Read(b, 0, 1);
            return b[0];
        }
        public void Flush()
        {
            lock (this)
            {
                Current.Writer.Flush();
            }
        }
        public void Dispose()
        {
            Patches.ForEach(p => p.Dispose());
            lock (OpenedStreams)
            {
                foreach (var s in OpenedStreams.Values)
                {
                    s.Invalidate();
                }
            }
            _baseStream?.Close();
            _baseStream?.Dispose();
        }
    }
}
