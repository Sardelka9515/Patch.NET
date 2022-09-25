using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

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
        public static string Hash(Stream stream){
            stream.Position=0;
            using (SHA256 SHA256 = SHA256.Create())
            {
                return Convert.ToHexString(SHA256.ComputeHash(stream));
            }
        }
        public static void RandLongArray(long[] arr, long min, long max)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = rand.NextInt64(min, max);
            }
        }
        public static string Dump<T>(this IEnumerable<T> ts)
        {
            return string.Join(',',ts);
        }
        public static void ForEach<T>(this IEnumerable<T> ts,Action<T> a)
        {
            foreach(T t in ts)
            {
                a(t);
            }
        }
        public static void Parse(this string[] args, string name, Action<string> callback)
        {
            var ba = args.Where(x => x.StartsWith(name + "="));
            if (ba.Any()) { callback(ba.First().Split("=")[1]); }
        }
    }
}
