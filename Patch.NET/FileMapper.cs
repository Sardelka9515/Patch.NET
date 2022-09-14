global using System.IO;
global using System.Collections.Generic;

namespace PatchDotNet
{
    public class FileMapper
    {
        public long Length => Fragements[Fragements.Count - 1].EndPosition + 1;
        protected Stream _baseStream;
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
            _baseStream = baseStream;
            Fragements.Add(
                new FileFragement
                {
                    StartPosition = 0,
                    EndPosition = baseStream.Length-1,
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
                    EndPosition = length-1,
                    ReadPosition = 0,
                    Stream = null
                }
            );
        }
        protected static FragmentComparer Comparer = new FragmentComparer();

        /// <summary>
        /// Map record to the memory and return the fragment index
        /// </summary>
        /// <param name="vPos"></param>
        /// <param name="readPos"></param>
        /// <param name="chunkLen"></param>
        /// <param name="stream"></param>
        /// <returns></returns>
        public int MapRecord(long vPos, long readPos, int chunkLen, Stream stream, bool advance)
        {
            if (Fragements[CurrentFragment].TryMerge(vPos, readPos, chunkLen, stream))
            {
                RemoveOverlapped(CurrentFragment);
                return CurrentFragment;
            }

            var newFrag = new FileFragement
            {
                StartPosition = vPos,
                EndPosition = vPos + chunkLen - 1,
                ReadPosition = readPos,
                Stream = stream
            };

            var index = Fragements.BinarySearch(newFrag, Comparer);

            if (index < 0)
            {
                index = ~index;
            }
            Fragements.Insert(index, newFrag);
            RemoveOverlapped(index);
            if (advance)
            {
                Position+=chunkLen;
                CurrentFragment = index;
            }
            return index;
        }
        void RemoveOverlapped(int newFragIndex)
        {
            var newFrag = Fragements[newFragIndex];

            int remove = 0;
            // Console.WriteLine("================evaluating overlapping==================");
            // DumpFragments();


            // Find previous fragement and resize/split as needed
            if (newFragIndex > 0)
            {
                var i = newFragIndex - 1;
                var splitted = Fragements[i].Clone();
                if (Fragements[i].EndPosition >= newFrag.StartPosition)
                {
                    Fragements[i].SetEnd(newFrag.StartPosition - 1);
                    // Console.WriteLine($"Set end of previous fragment {i} to " + (newFrag.StartPosition - 1));

                    // Splitted frag
                    if (splitted.EndPosition > newFrag.EndPosition)
                    {
                        splitted.SetStart(newFrag.EndPosition + 1);
                        Fragements.Insert(newFragIndex + 1, splitted);
                        // Console.WriteLine("Frag inserted after newfrag: " + splitted.StartPosition);
                        goto end; // No further check needed
                    }
                }
            }

            for (int i = newFragIndex + 1; i < Fragements.Count; i++)
            {
                // Console.WriteLine("Checking fragment "+i);
                if (Fragements[i].EndPosition <= newFrag.EndPosition)
                {
                    remove++;
                }
                else if (Fragements[i].StartPosition <= newFrag.EndPosition)
                {
                    Fragements[i].SetStart(newFrag.EndPosition + 1);
                    // Console.WriteLine($"Trimmed fragment {i}");
                    break;
                }
                else if (Fragements[i].StartPosition == newFrag.EndPosition + 1)
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
                // Console.WriteLine($"{remove} fragements removed");
            }

        end:
            ;

            // Console.WriteLine("===================================");
        }
        public bool Seek(long pos)
        {
            if (pos > Length)
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
        public void DumpFragments()
        {
            Fragements.ForEach(f => Console.WriteLine($"[{f.StartPosition}, {f.Length}, {f.EndPosition}] => {f.ReadPosition}"));
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