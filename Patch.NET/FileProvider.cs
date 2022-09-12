using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PatchDotNet
{
    public class FileFragement
    {
        public long StartPosition;
        public long EndPosition;
        public long ReadPosition;
        public BinaryReader Reader;
    }
    public class FragmentComparer : IComparer<FileFragement>
    {
        public int Compare(FileFragement x, FileFragement y)
        {
            if (x.StartPosition < y.StartPosition)
            {
                return -1;
            }
            else if (x.StartPosition > y.StartPosition)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }
    }
    public class FileProvider
    {
        FileStream BaseStream;
        Patch Current;
        List<FileFragement> Fragements;
        static FragmentComparer Comparer = new FragmentComparer();
        public void Test() {
        }
        public FileProvider(string baseFile, bool canWrite, params string[] snapshots)
        {
            BaseStream = new FileStream(baseFile, FileMode.Open, FileAccess.Read);
            Fragements = new List<FileFragement>() {
                new FileFragement {
                    StartPosition=0,
                    EndPosition=BaseStream.Length,
                    ReadPosition=0,
                    Reader=new BinaryReader(BaseStream)
                }
            };
            for (int i = 0; i < snapshots.Length - 1; i++)
            {
                Console.WriteLine("Reading records from "+ snapshots[i]);
                var snapshot = new Patch(snapshots[i], false);
                int read = 0;
                while (snapshot.ReadRecord(out var type, out var vPosOrSize, out var readPos, out var chunkLen))
                {
                    if (type == RecordType.Write)
                    {
                        MapRecord(vPosOrSize,readPos,chunkLen,snapshot.Reader);
                    }
                    else if (type == RecordType.SetLength)
                    {

                    }
                    read++;
                    Console.Write("\rRead " +read+" records");
                }
            }
            Current = new Patch(snapshots[snapshots.Length - 1], canWrite);
        }
        void MapRecord(long vPos, long readPos, int chunkLen, BinaryReader reader)
        {
            var newFrag = new FileFragement
            {
                StartPosition = vPos,
                EndPosition = vPos + chunkLen,
                ReadPosition = readPos,
                Reader = reader
            };
            var index = Fragements.BinarySearch(newFrag, Comparer);

            // No frag with same starting position
            if (index < 0)
            {
                index = ~index;
                Fragements.Insert(index, newFrag);
                RemoveOverlapped(index);
            }
            else
            {
                Fragements[index] = newFrag;
                RemoveOverlapped(index);
            }
        }
        void RemoveOverlapped(int newFragIndex)
        {
            var newFrag=Fragements[newFragIndex];

            // Remove overlapped parts
            int remove = 0;
            for (int i = newFragIndex + 1; i < Fragements.Count; i++)
            {
                var tocheck = Fragements[i];
                if (tocheck.EndPosition <= newFrag.EndPosition)
                {
                    remove++;
                }
                else if (tocheck.StartPosition <= newFrag.EndPosition)
                {
                    var offset = tocheck.StartPosition - 1 - newFrag.EndPosition;
                    tocheck.StartPosition += offset;
                    tocheck.ReadPosition += offset;
                    break;
                }
                else if (tocheck.StartPosition == newFrag.EndPosition + 1)
                {
                    break;
                }
                else
                {
                    throw new NotImplementedException();
                }

            }
            if (remove > 0)
            {
                Fragements.RemoveRange(newFragIndex + 1, remove);
                Console.WriteLine($"{remove} fragements removed");
            }
        }
        public void Write(long position,byte[] chunk)
        {
            MapRecord(position, Current.Write(position, chunk), chunk.Length, Current.Reader);
        }
    }
}
