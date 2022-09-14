using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PatchDotNet
{
    public static class Util
    {
        static Random rand=new Random();
        public static void RandBytes(byte[] bytes)
        {
            rand.NextBytes(bytes);
        }
        public static int RandInt(int min,int max)
        {
            return rand.Next(min,max);
        }
        public static long RandLong(long min, long max)
        {
            return rand.NextInt64(min, max);
        }
        public static int[] RandIntArray(int count,int min, int max)
        {
            var arr=new int[count];
            for(int i = 0; i < count; i++)
            {
                arr[i]=rand.Next(min, max);
            }
            return arr;
        }

        public static void RandLongArray(long[] arr, long min, long max)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = rand.NextInt64(min, max);
            }
        }
    }
}
