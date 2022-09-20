using PatchDotNet;
using PatchDotNet.Win32;
using DokanNet;
using DokanNet.Logging;
namespace UnitTests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Mount();
            return;
            FileTest.Run();
            RecordTest.Run();
        }
        public static void Mount()
        {
            var origin = @"M:\test\test.zip";
            var patch = @"M:\test\patch1";
            var provider = new FileProvider(origin, true, patch);
            var mount = new SingleFileMount(provider, "test.zip", null);
            // var mount=new Mirror(new ConsoleLogger("[Mirror] "),@"C:\");
            using var dokan = new Dokan(null);
            var builder = new DokanInstanceBuilder(dokan)
                .ConfigureOptions(options =>
                {
                    options.Options = DokanOptions.MountManager;
                    options.MountPoint = @"C:\mount";
                });
            var dokanInstance = builder.Build(mount);
            // Test(rep,provider);
            while (true)
            {
                switch (Console.ReadLine())
                {
                    case "clear": Console.Clear(); break;
                    case "dump": provider.DumpFragments(); break;
                    case "exit": Console.Clear(); goto stop;
                }
            }
            stop:
            provider.Dispose();
        }
        static void Test(string replicated, FileProvider provider)
        {
            var replicate = File.Open(replicated, FileMode.Open);
            var prov = provider.GetStream();
            Verify();
            for (int i = 0; i < 100; i++)
            {
                Write(prov, replicate);
            }
            provider.Check();
            Verify();
            replicate.Dispose();
            prov.Dispose();

            void Write(Stream s1, Stream s2)
            {
                CheckLength();
                var secs = s1.Length / 4096;
                var loca = Util.RandLong(0, secs) * 4096;
                Console.WriteLine("Writing chunk: " + loca);
                s1.Position = s2.Position = loca;
                var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                s1.Write(data);
                s2.Write(data);
            }
            void Resize()
            {
                CheckLength();
                var size = Util.RandLong(0, 2 * prov.Length);
                prov.SetLength(size);
                replicate.SetLength(size);
            }
            void CheckLength()
            {
                if (replicate.Length != prov.Length) { throw new Exception("length fail"); }
            }
            void Verify()
            {
                CheckLength();
                replicate.Position = prov.Position = 0;

                while (replicate.Position < replicate.Length)
                {
                    var bufferRep = new byte[32];
                    var bufferProv = new byte[32];
                    if (prov.Read(bufferProv) != replicate.Read(bufferRep))
                    {
                        throw new Exception("read");
                    }
                    if (!bufferRep.SequenceEqual(bufferProv))
                    {
                        Console.WriteLine("Wrong data:" + replicate.Position);
                        Console.WriteLine(string.Join(',', bufferRep));

                        Console.WriteLine(string.Join(',', bufferProv));
                        // break;
                    }
                }
                Console.WriteLine("Verify done");
                return;

            }
        }
    }
}