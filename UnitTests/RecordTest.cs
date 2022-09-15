using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PatchDotNet;


namespace UnitTests
{
    internal class RecordTest
    {
        public static void Run(string path = @"test\test.patch", int testCount = 1000)
        {
            Patch patch;

            // BASIC-TEST
            {
                Directory.CreateDirectory(Directory.GetParent(path).FullName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                patch = new Patch(path, true);

                Console.WriteLine($"Patch created: {patch.Guid}");
                TestRecord[] tests = new TestRecord[testCount];
                List<long> setLen = new List<long>((int)(testCount * 0.4));
                TestRecord last = new();
                for (int i = 0; i < testCount; i++)
                {

                    // We don't want to test sequential write here
                    tests[i] = new(i != 0 ? (last.Position + last.Data.Length) : 0);

                    last = tests[i];
                    if (i % 100 == 0)
                    {
                        Console.Write("\rInit test data: " + (float)i / testCount);
                    }
                }

                for (int i = 0; i < tests.Length; i++)
                {
                    if (Util.RandInt(0, 3) == 1)
                    {
                        var len = Util.RandLong(0, long.MaxValue);
                        // Console.WriteLine($"SetLength record: "+len);
                        setLen.Add(len);
                        patch.WriteResize(len);
                    }
                    else
                    {
                        patch.Write(tests[i].Position, tests[i].Data,0, tests[i].Data.Length);
                    }
                    if (i % 100 == 0)
                    {
                        Console.Write("\rWrite record test: " + (float)i / testCount);
                    }
                }
                patch.Dispose();

                patch = new Patch(path, true);
                int j = 0;
                for (int i = 0; i < tests.Length; i++)
                {
                    patch.ReadRecord(out var type, out var posOrLen, out var realpos, out var len);
                    if (type == RecordType.Write)
                    {

                        var r = tests[i];
                        if (r.Position != posOrLen)
                        {
                            throw new Exception($"Position fail: {r.Position}, {posOrLen}");
                        }
                        else if (len != r.Data.Length) { throw new Exception("Length fail"); }
                        else if (!r.Data.SequenceEqual(patch.ReadBytes(realpos, len))) { throw new Exception("Data fail"); }
                    }
                    else
                    {
                        if (posOrLen != setLen[j]) { throw new Exception("SetLength fail:" + posOrLen); }
                        j++;
                    }

                    if (i % 100 == 0)
                    {
                        Console.Write("\rRead record test: " + (float)i / testCount);
                    }
                    // Console.WriteLine("Pass " + i);
                }
                patch.Dispose();
            }

            // SEQUENTIAL-WRITE-TEST
            {
                Console.WriteLine("\nTesting sequential write defragmention");

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                List<byte> data = new List<byte>(testCount * 4096);
                patch = new Patch(path, true);
                var rec = new TestRecord(-1);
                for (int i = 0;i< testCount; i++)
                {
                    rec.Next();
                    if (i == 0)
                    {
                        Console.WriteLine("Starting position: " + rec.Position);
                    }
                    patch.Write(rec.Position, rec.Data,0,rec.Data.Length);
                    data.AddRange(rec.Data);
                }
                patch.Dispose();

                patch = new Patch(path, true);
                List<byte> toCheck = new List<byte>(data.Count);
                while(patch.ReadRecord(out _, out var pos,out var readPos,out var len))
                {
                    Console.WriteLine("Read record: "+pos);
                    toCheck.AddRange(patch.ReadBytes(readPos,len));
                }
                if (!data.SequenceEqual(toCheck))
                {
                    throw new Exception("Invalid data");
                }
                patch.Dispose();
                Console.WriteLine("Sequential write test passed");
            }
        }
    }

    struct TestRecord
    {
        static Random random = new Random();
        public long Position;
        public byte[] Data;
        public TestRecord(long dontStartWith=0)
        {
            Data = new byte[random.Next(1, 4096)];
            random.NextBytes(Data);
            again:
            Position = random.NextInt64(long.MaxValue);
            if (Position == dontStartWith)
            {
                goto again;
            }
        }
        public void Next()
        {
            Position += Data.Length;
            Data = new byte[random.Next(1, 4096)];
            random.NextBytes(Data);
        }
        public void Print()
        {
            Console.WriteLine("Position: " + Position);
            Console.WriteLine("Chunk length:" + Data.Length);
            // Console.WriteLine("Data: {" + String.Join(", ", Data)+"}");
        }
    }
}
