global using System.IO;
global using System.Collections.Generic;

namespace PatchDotNet
{
    public class FileMapper
    {
        protected List<FileFragement> Fragements = new List<FileFragement>();
        /// <summary>
        /// Dummy fragment used to conduct binary search
        /// </summary>
        /// <returns></returns>
        protected FileFragement Seeker = new();
        public long Position { get; protected set; }
        protected int CurrentFragment = 0;

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
                    Stream = baseStream
                }
            );
        }
        /// <summary>
        /// Initilize a mapper with the given empty block
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
                    Stream = null
                }
            );
        }
        static FragmentComparer Comparer = new FragmentComparer();

        public int MapRecord(long vPos, long readPos, int chunkLen, Stream stream)
        {
            var newFrag = new FileFragement
            {
                StartPosition = vPos,
                EndPosition = vPos + chunkLen,
                ReadPosition = readPos,
                Stream = stream
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
            if (pos > Fragements[Fragements.Count - 1].EndPosition)
            {
                return false;
            }
            else if (pos == Position)
            {
                return true;
            }
            else if (pos < Position)
            {
                Seeker.StartPosition = pos;
                CurrentFragment = Fragements.BinarySearch(0, CurrentFragment, Seeker, Comparer);

            }
            else // pos > Position
            {
                // Sequential r/w
                if (pos == Fragements[CurrentFragment].EndPosition + 1)
                {
                    CurrentFragment++;
                }
                else
                {

                    Seeker.StartPosition = pos;
                    CurrentFragment = Fragements.BinarySearch(CurrentFragment, Fragements.Count - CurrentFragment, Seeker, Comparer);

                }

            }
            Position = pos;
            if (CurrentFragment < 0) { CurrentFragment = ~CurrentFragment; CurrentFragment -= 1; }
            return true;
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