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
        public static void Run(string folder="test")
        {
            Console.WriteLine("Testing FileProvider: ");
            if (Directory.Exists(folder)) { Directory.Delete(folder, true); }
            Directory.CreateDirectory(folder);
            var basePath = Path.Combine(folder,"base");
            var verifyPath = Path.Combine(folder, "verify");
            var patch1 = Path.Combine(folder, "patch1");
            var patch2 = Path.Combine(folder, "patch2");
            File.Create(basePath).Close();
            File.Create(verifyPath).Close();


            RunTest(basePath,verifyPath,patch1);
            RunTest(basePath, verifyPath, patch1,patch2);

            Console.WriteLine("FileProvider test passed.");
        }
        public static void RunTest(string basePath,string verifyPath,params string[] patches)
        {
            Console.WriteLine($"Running test with {patches.Length} snapshots");
            var prov = new FileProvider(basePath, true, patches);
            var pStream = prov.GetStream();
            var verify = File.Open(verifyPath,FileMode.OpenOrCreate,FileAccess.ReadWrite);
            Console.WriteLine("Random r/w test");
            for (int i = 0; i < 500; i++)
            {
                var data = new byte[Util.RandInt(1, 4096)];

                Test(Util.RandInt(0, (int)verify.Length), data);
            }

            {

                for(int i = 0; i < 20; i++)
                {
                    Console.Write("\rSequential r/w test "+i);
                    var pos = Util.RandLong(0, pStream.Length);
                    for (int j = 0; j < 20; j++)
                    {
                        var data = new byte[Util.RandInt(1, 4096)];
                        Test(pos, data);
                        pos += data.Length;
                    }
                }
                Console.WriteLine();
            }

            void Test(long position, byte[] chunk)
            {
                if (verify.Length != pStream.Length)
                {
                    throw new Exception($"File length mismatch: {verify.Length}, {pStream.Length}");
                }
                // Console.WriteLine($"Writing chunk: {position} {chunk.Length}");
                verify.Position = position;
                verify.Write(chunk);

                pStream.Seek(position, SeekOrigin.Begin);
                pStream.Write( chunk,0,chunk.Length);
                // provider.DumpFragments();
            }
            verify.Position = 0;
            pStream.Seek(0,SeekOrigin.Begin);

            Console.WriteLine("Verifying data");
            while (verify.Position < verify.Length)
            {
                if (verify.ReadByte() != pStream.ReadByte())
                {
                    throw new Exception("File corrupted");
                }
            }

            verify.Dispose();
            pStream.Dispose();


            pStream = new FileProvider(basePath, true, patches).GetStream();
            verify = File.OpenRead(verifyPath);

            verify.Position = 0;
            pStream.Seek(0,SeekOrigin.Begin);

            Console.WriteLine("Verifying data 2");
            while (verify.Position < pStream.Length)
            {
                if (verify.ReadByte() != pStream.ReadByte())
                {
                    throw new Exception("File corrupted");
                }
            }
            verify.Close();
            verify.Dispose();
            prov.Dispose();
        }
    }
}
