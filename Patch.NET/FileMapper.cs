global using System.IO;
global using System.Collections.Generic;

namespace PatchDotNet
{
    public class FileMapper
    {
        public long Length => Fragments[Fragments.Count - 1].EndPosition + 1;
        public Stream BaseStream { get;private set; }
        internal List<FileFragement> Fragments = new List<FileFragement>();
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
            BaseStream = baseStream;
            Fragments.Add(
                new FileFragement
                {
                    StartPosition = 0,
                    EndPosition = baseStream.Length - 1,
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
            Fragments.Add(
                new FileFragement
                {
                    StartPosition = 0,
                    EndPosition = length - 1,
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
            
            if (CurrentFragment > 0 && Fragments[CurrentFragment - 1].TryMerge(vPos, readPos, chunkLen, stream))
            {
                Position+=chunkLen;
                RemoveOverlapped(CurrentFragment - 1);
                CheckPosition();
                return CurrentFragment - 1;
            }
            
            var newFrag = new FileFragement
            {
                StartPosition = vPos,
                EndPosition = vPos + chunkLen - 1,
                ReadPosition = readPos,
                Stream = stream
            };

            var index = Fragments.BinarySearch(newFrag, Comparer);

            if (index < 0)
            {
                index = ~index;
            }
            Fragments.Insert(index, newFrag);
            RemoveOverlapped(index);
            if (advance)
            {
                Position += chunkLen;
                CurrentFragment = index + 1;
                CheckPosition();

            }
            return index;
        }
        /// <summary>
        /// Check if position matches current fragment, only used for debugging
        /// </summary>
        protected void CheckPosition()
        {
#if DEBUG

            // Console.WriteLine(CurrentFragment+"/"+Fragements.Count);
            // Console.WriteLine(Position+"/"+Length);
            if (CurrentFragment >= Fragments.Count)
            {
                if (Position != Length)
                {
                    DumpFragments();

                    throw new Exception("Position eof: "+Position);
                }
            }
            else if (Position == Length)
            {
                if (CurrentFragment != Fragments.Count)
                {
                    throw new Exception("Position eof");

                }
            }
            else if (Fragments[CurrentFragment].StartPosition > Position || Fragments[CurrentFragment].EndPosition < Position)
            {
                DumpFragments();
                throw new Exception($"Fragment position mismatch {Fragments[CurrentFragment].StartPosition}, {Position}, {Fragments[CurrentFragment].EndPosition}");
            }
#endif

        }
        void RemoveOverlapped(int newFragIndex)
        {
            var newFrag = Fragments[newFragIndex];

            int remove = 0;
            // Console.WriteLine("================evaluating overlapping==================");
            // DumpFragments();


            // Find previous fragement and resize/split as needed
            if (newFragIndex > 0)
            {
                var i = newFragIndex - 1;
                var splitted = Fragments[i].Clone();
                if (Fragments[i].EndPosition >= newFrag.StartPosition)
                {
                    Fragments[i].SetEnd(newFrag.StartPosition - 1);
                    // Console.WriteLine($"Set end of previous fragment {i} to " + (newFrag.StartPosition - 1));

                    // Splitted frag
                    if (splitted.EndPosition > newFrag.EndPosition)
                    {
                        splitted.SetStart(newFrag.EndPosition + 1);
                        Fragments.Insert(newFragIndex + 1, splitted);
                        // Console.WriteLine("Frag inserted after newfrag: " + splitted.StartPosition);
                        goto end; // No further check needed
                    }
                }
            }

            for (int i = newFragIndex + 1; i < Fragments.Count; i++)
            {
                // Console.WriteLine("Checking fragment "+i);
                if (Fragments[i].EndPosition <= newFrag.EndPosition)
                {
                    remove++;
                }
                else if (Fragments[i].StartPosition <= newFrag.EndPosition)
                {
                    Fragments[i].SetStart(newFrag.EndPosition + 1);
                    // Console.WriteLine($"Trimmed fragment {i}");
                    break;
                }
                else if (Fragments[i].StartPosition == newFrag.EndPosition + 1)
                {
                    break;
                }
                else
                {
                    DumpFragments();
                    throw new NotImplementedException("Mapping failure occurred at fragment " + i);
                }

            }
            if (remove > 0)
            {
                Fragments.RemoveRange(newFragIndex + 1, remove);
                // Console.WriteLine($"{remove} fragements removed");
            }

        end:
            ;

            // Console.WriteLine("===================================");
        }
        /// <summary>
        /// Print all fragments, only used for debugging
        /// </summary>
        public void DumpFragments()
        {
            for (int i = 0; i < Fragments.Count; i++)
            {
                var f = Fragments[i];
                Console.WriteLine($"{i}[{f.StartPosition}, {f.Length}, {f.EndPosition}] => {f.ReadPosition} : {(f.Stream as FileStream)?.Name}");
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