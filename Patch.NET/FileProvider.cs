using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PatchDotNet
{
    public struct FileFragement
    {
        public long StartPosition;
        public long EndPosition;
        public long ReadPosition;
        public long Length => EndPosition - StartPosition + 1;
        public Stream Stream;
        public int Read(long startPosition, byte[] buffer, int start, int maxCount)
        {
            if(startPosition>EndPosition){
                throw new InvalidOperationException("Cannot read data beyond this fragment");
            }

            // Read overflow check
            if (maxCount > Length)
            {
                maxCount = (int)Length;
            }

            if(Stream==null){
                // Blank region, possibly a pre-allocated block during resize
                return maxCount;
            }
            else{

                Stream.Position = ReadPosition + startPosition - StartPosition;
                return Stream.Read(buffer, start, maxCount);
            }
        }
    }
    public class FileProvider:FileMapper
    {
        Patch Current;
        public FileProvider(string baseFile, bool canWrite, params string[] snapshots):base(File.OpenRead(baseFile))
        {
            for (int i = 0; i < snapshots.Length - 1; i++)
            {
                Console.WriteLine("Reading records from " + snapshots[i]);
                var snapshot = new Patch(snapshots[i], false);
                int read = 0;
                while (snapshot.ReadRecord(out var type, out var vPosOrSize, out var readPos, out var chunkLen))
                {
                    if (type == RecordType.Write)
                    {
                        MapRecord(vPosOrSize, readPos, chunkLen, snapshot.Reader.BaseStream);
                    }
                    else if (type == RecordType.SetLength)
                    {

                    }
                    read++;
                    Console.Write("\rRead " + read + " records");
                }
            }
            Current = new Patch(snapshots[snapshots.Length - 1], canWrite);
        }
        public void Write(long position, byte[] chunk)
        {
            MapRecord(position, Current.Write(position, chunk), chunk.Length, Current.Reader.BaseStream);
        }

        public int Read(byte[] buffer, int startIndex, int count)
        {

            int read = 0;
            int thisRead;
            while (read < count && (thisRead = Fragements[CurrentFragment].Read(Position, buffer, startIndex + read, count - read)) != 0)
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
            }
#if DEBUG
            Console.WriteLine($"Requested {count} bytes, read {read}");
#endif
            return read;
        }
    }
}
