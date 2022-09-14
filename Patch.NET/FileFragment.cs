﻿using System;
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
        public long Length => EndPosition - StartPosition + 1;
        public Stream Stream;
        public FileFragement Clone()
        {
            return new FileFragement
            {
                StartPosition = StartPosition,
                EndPosition = EndPosition,
                ReadPosition = ReadPosition,
            };
        }
        public void SetStart(long pos)
        {
            var offset = pos - StartPosition;
            StartPosition += offset;
            ReadPosition += offset;
        }
        public void SetEnd(long pos)
        {
            EndPosition = pos;
        }
        public int Read(long startPosition, byte[] buffer, int start, int maxCount)
        {
            if (startPosition > EndPosition)
            {
                throw new InvalidOperationException("Cannot read data beyond this fragment");
            }

            // Read overflow check
            if (maxCount > Length)
            {
                maxCount = (int)Length;
            }

            if (Stream == null)
            {
                // Blank region, possibly a pre-allocated block during resize
                return maxCount;
            }
            else
            {

                Stream.Position = ReadPosition + startPosition - StartPosition;
                return Stream.Read(buffer, start, maxCount);
            }
        }
        /// <summary>
        /// Attemps to merge a subsequent record to this one
        /// </summary>
        /// <returns></returns>
        public bool TryMerge(long pos, long readPos, int chunkLen, Stream stream)
        {
            if (pos == EndPosition + 1 && stream == Stream && readPos == ReadPosition + Length)
            {
                EndPosition += chunkLen;

#if DEBUG
                // Console.WriteLine($"Merged record: [{StartPosition},{EndPosition}]");
#endif
                return true;
            }
            return false;
        }
    }
}