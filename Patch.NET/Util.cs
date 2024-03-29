﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace PatchDotNet
{
    public static class Util
    {
        static Random rand = new Random();
        public static void RandBytes(byte[] bytes)
        {
            rand.NextBytes(bytes);
        }
        public static int RandInt(int min, int max)
        {
            return rand.Next(min, max);
        }
        public static long RandLong(long min, long max)
        {
            return rand.NextInt64(min, max);
        }
        public static int[] RandIntArray(int count, int min, int max)
        {
            var arr = new int[count];
            for (int i = 0; i < count; i++)
            {
                arr[i] = rand.Next(min, max);
            }
            return arr;
        }
        public static string Hash(Stream stream)
        {
            stream.Position = 0;
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
            return string.Join(',', ts);
        }
        public static void ForEach<T>(this IEnumerable<T> ts, Action<T> a)
        {
            foreach (T t in ts)
            {
                a(t);
            }
        }
        public static void Parse(this string[] args, string name, Action<string> callback)
        {
            var ba = args.Where(x => x.StartsWith(name + "="));
            if (ba.Any()) { callback(ba.First().Split("=")[1]); }
        }
        public static string[] SplitWithQuotes(string line)
        {
            var result = Regex.Matches(line, @"[\""].+?[\""]|[^ ]+")
                .Cast<Match>()
                .Select(m => m.Value)
                .ToArray();
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i].StartsWith("\"") && result[i].EndsWith("\"") && result[i].Length != 1)
                {
                    result[i] = result[i].Substring(1, result[i].Length - 2);
                }
            }
            return result;
        }
        public static byte[] ToBytes(this DateTime dt)
        {
            return BitConverter.GetBytes(dt.ToBinary());
        }
        public static DateTime GetDateTime(this byte[] bs)
        {
            return DateTime.FromBinary(BitConverter.ToInt64(bs));
        }
        public static string FormatSize(double len)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
            // show a single decimal place, and no space.
            return string.Format("{0:0.##} {1}", len, sizes[order]);
        }
        public static T ReadJson<T>(string path) where T: new()
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
            }
            catch(FileNotFoundException)
            {
                var t = new T();
                WriteJson(t, path);
                return t;
            }
        }
        public static string JoinLines<T>(this IEnumerable<T> lines)
        {
            return string.Join(Environment.NewLine, lines);
        }
        public static void WriteJson<T>(T obj,string path)
        {
            File.WriteAllText(path,JsonConvert.SerializeObject(obj));
        }
    }
    public class ConsoleCommand
    {
        public readonly Dictionary<string, (Action<string[]>, string, string)> Commands = new();
        public void Run(string line)
        {
            if (line.ToLower() == "help")
            {
                Console.WriteLine(GetText());
                return;
            }
            var cs = Util.SplitWithQuotes(line);
            if (Commands.TryGetValue(cs[0].ToLower(), out var tup))
            {
                tup.Item1(cs.Skip(1).ToArray());
            }
            else
            {
                Console.WriteLine(GetText());
            }
        }
        public string GetText()
        {
            string help = "";
            foreach (var tup in Commands.Values)
            {

                help += tup.Item2+"\n";
                help += "  - "+tup.Item3 + "\n";
            }
            return help;
        }
        public void AddCommand(string name, Action<string[]> callback,string usage,string help)
        {
            Commands.Add(name, (callback, usage, help));
        }
    }
    public class NonSeekableStream : Stream
    {
        Stream m_stream;
        public NonSeekableStream(Stream baseStream)
        {
            m_stream = baseStream;
        }
        public override bool CanRead
        {
            get { return m_stream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return m_stream.CanWrite; }
        }

        public override void Flush()
        {
            m_stream.Flush();
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get
            {
                return m_stream.Position;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return m_stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            m_stream.Write(buffer, offset, count);
        }
    }
}
