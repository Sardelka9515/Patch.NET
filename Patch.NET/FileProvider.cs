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
                        SetLength(vPosOrSize);
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
                CurrentFragment = Fragements.Count;
            }
            else if (pos == Position)
            {
                return true;
            }
            else if (pos < Position)
            {
                if (CurrentFragment < Fragements.Count && pos >= Fragements[CurrentFragment].StartPosition)
                {
                    // Same fragment
                }
                else
                {

                    Seeker.StartPosition = pos;
                    CurrentFragment = Fragements.BinarySearch(0, CurrentFragment, Seeker, Comparer);
                }

            }
            else // pos > Position
            {
                Seeker.StartPosition = pos;
                CurrentFragment = Fragements.BinarySearch(CurrentFragment, Fragements.Count - CurrentFragment, Seeker, Comparer);
            }
            Position = pos;

            if (CurrentFragment < 0)
            {
                CurrentFragment = (~CurrentFragment) - 1;
                CheckPosition();
            }
            CheckPosition();
            // Console.WriteLine(CurrentFragment + "/" + Fragements.Count);
            return true;
        }

        public void SetLength(long newLength)
        {
            var current = Length;
            if (current == newLength) { return; }
            if (newLength > current)
            {
                Fragements.Add(new FileFragement
                {
                    StartPosition = current,
                    EndPosition = newLength - 1,
                });
            }
            else
            {
                Seeker.StartPosition = newLength;
                var index = Fragements.BinarySearch(Seeker, Comparer);
                if (index < 0)
                {
                    index = ~index;
                    Fragements[index - 1].SetEnd(newLength - 1);
                }
                Fragements.RemoveRange(index, Fragements.Count - index);
                if (Position > Length) { Position = Length; CurrentFragment = Fragements.Count - 1; }
            }
        }
        public int Read(byte[] buffer, int startIndex, int count)
        {
            if (CurrentFragment > Fragements.Count - 1) { return 0; }
            int read = 0;
            int thisRead;
            CheckPosition();
            while (read < count && (thisRead = Fragements[CurrentFragment].Read(Position, buffer, startIndex + read, count - read, DumpFragments)) != 0)
            {
                Position += thisRead;
                read += thisRead;

                // Proceed to read next fragment
                if (Fragements[CurrentFragment].EndPosition < Position)
                {
                    if (CurrentFragment == Fragements.Count - 1)
                    {
                        // End-of-File

#if DEBUG
                        Console.WriteLine("EoF reached");
#endif

                        break;
                    }
                    else
                    {
                        CurrentFragment++;
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
