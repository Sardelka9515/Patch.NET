using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PatchDotNet;

namespace UnitTests
{
    internal class FileTest
    {
        public static void Run(string folder = "test")
        {
            Console.WriteLine("Testing FileProvider: ");
            if (Directory.Exists(folder)) { Directory.Delete(folder, true); }
            Directory.CreateDirectory(folder);
            var basePath = Path.Combine(folder, "base");
            var verifyPath = Path.Combine(folder, "verify");
            var patch1 = Path.Combine(folder, "patch1");
            var patch2 = Path.Combine(folder, "patch2");
            Console.WriteLine("Generating test files");
            var bs = File.Create(basePath);
            var rd = new byte[Util.RandInt(1024*1024*30,1024 * 1024 * 50)];
            Util.RandBytes(rd);
            bs.Write(rd, 0, rd.Length);

            var baseHash = Util.Hash(bs);
            bs.Close();
            File.Copy(basePath, verifyPath, true);


            var hash1=RunTest(basePath, verifyPath, patch1);
            RunTest(basePath, verifyPath, patch1, patch2);
            var prov=new FileProvider(basePath,false,patch1);
            if(hash1!=Util.Hash(prov.GetStream())){
                throw new Exception("Hash 2 failed");
            }
            prov.Dispose();

            Console.WriteLine("FileProvider test passed.");
        }
        public static string RunTest(string basePath, string verifyPath, params string[] patches)
        {
            Console.WriteLine($"Running test with {patches.Length} snapshots");
            var prov = new FileProvider(basePath, true, patches);
            var pStream = prov.GetStream();
            var verify = File.Open(verifyPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            Console.WriteLine("Random r/w test");
            for (int i = 0; i < 500; i++)
            {
                var data = new byte[Util.RandInt(1, 4096)];
                if(Util.RandInt(0,10)<3){
                    TestResize();
                }
                TestWrite(Util.RandInt(0, (int)verify.Length), data);
            }

            {

                Console.Write("\rSequential r/w test ");
                for (int i = 0; i < 20; i++)
                {
                    var pos = Util.RandLong(0, pStream.Length);
                    for (int j = 0; j < 20; j++)
                    {
                        var data = new byte[Util.RandInt(1, 4096)];
                        TestWrite(pos, data);
                        pos += data.Length;
                    }
                }
            }

            void TestResize(){

                if (verify.Length != pStream.Length)
                {
                    throw new Exception($"File length mismatch: {verify.Length}, {pStream.Length}");
                }
                var len=Util.RandLong(0,pStream.Length*2);
                pStream.SetLength(len);
                verify.SetLength(len);
                if (verify.Length != pStream.Length)
                {
                    throw new Exception($"File length mismatch: {verify.Length}, {pStream.Length}");
                }
            }

            void TestWrite(long position, byte[] chunk)
            {
                if (verify.Length != pStream.Length)
                {
                    throw new Exception($"File length mismatch: {verify.Length}, {pStream.Length}");
                }
                // Console.WriteLine($"Writing chunk: {position} {chunk.Length}");
                verify.Position = position;
                verify.Write(chunk);

                pStream.Seek(position, SeekOrigin.Begin);
                pStream.Write(chunk, 0, chunk.Length);
                // provider.DumpFragments();
            }

            void TestRead()
            {
                for (int i = 0; i < 100; i++)
                {
                    var pos = Util.RandLong(0, pStream.Length);
                    var data = new byte[Util.RandInt(1, 500)];
                    var data2 = new byte[data.Length];
                    pStream.Position = verify.Position = pos;
                    pStream.Read(data, 0, data.Length);
                    verify.Read(data2, 0, data2.Length);
                    if (!data.SequenceEqual(data2))
                    {
                        throw new Exception("Random read fail");
                    }
                }
            }



            verify.Position = 0;
            pStream.Seek(0, SeekOrigin.Begin);

            Console.WriteLine("Verifying data");
            while (verify.Position < verify.Length)
            {
                if (verify.ReadByte() != pStream.ReadByte())
                {
                    throw new Exception("File corrupted");
                }
            }
            TestRead();

            verify.Dispose();
            prov.Dispose();


            prov = new FileProvider(basePath, true, patches);
            pStream = prov.GetStream();
            verify = File.OpenRead(verifyPath);

            verify.Position = 0;
            pStream.Seek(0, SeekOrigin.Begin);

            Console.WriteLine("Verifying data 2");
            while (verify.Position < pStream.Length)
            {
                if (verify.ReadByte() != pStream.ReadByte())
                {
                    throw new Exception("File corrupted");
                }
            }
            TestRead();


            var hash = Util.Hash(pStream);
            if(hash!=Util.Hash(verify)){throw new Exception("Hash failed");}
            verify.Close();
            verify.Dispose();
            prov.Dispose();
            return hash;
        }
    }
}
