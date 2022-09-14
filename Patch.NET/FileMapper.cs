global using System.IO;
global using System.Collections.Generic;

namespace PatchDotNet
{
    public class FileMapper
    {
        List<FileFragement> Fragements = new List<FileFragement>();
        private long Position = 0;
        private int CurrentIndex = 0;

        /// <summary>
        /// Initialize a mapper with given base stream
        /// </summary>
        /// <param name="baseStream"></param>
        public FileMapper(Stream baseStream)
        {
            Fragements.Add(
                new FileFragement
                {
                    StartPosition = 0,
                    EndPosition = baseStream.Length,
                    ReadPosition = 0,
                    Reader = new BinaryReader(baseStream)
                }
            );
        }
        /// <summary>
        /// Initilize a mapper with a given empty
        /// </summary>
        /// <param name="length"></param>
        public FileMapper(long length)
        {
            Fragements.Add(
                new FileFragement
                {
                    StartPosition = 0,
                    EndPosition = length,
                    ReadPosition = 0,
                    Reader = null
                }
            );
        }
        static FragmentComparer Comparer = new FragmentComparer();

        public int MapRecord(long vPos, long readPos, int chunkLen, BinaryReader reader)
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
            return index;
        }
        void RemoveOverlapped(int newFragIndex)
        {
            var newFrag = Fragements[newFragIndex];

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
        public bool Seek(long pos)
        {
            if (pos > Fragements.Last().EndPosition)
            {
                return false;
            }
            else if (pos == Position)
            {
                return true;
            }

        }
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

}