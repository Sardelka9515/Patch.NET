// #define TEST
using PatchDotNet.Win32;
using PatchDotNet;
using DokanNet;
using Newtonsoft.Json;

namespace RowMount
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                args = new string[] { "mount" };
            }
            switch (args[0])
            {
                case "mount": Mount(args); break;
            }
        }
        public static void Mount(string[] args)
        {
            var mountPoint = @"C:\mount\file.vhdx";
            var store = "DefaultStore.json";
            string patchId = null;
            var _readonly = false;
            args.Parse("mount", x => mountPoint = x);
            args.Parse("store", x => store = x);
            args.Parse("patch", x => patchId = x);

            if (!File.Exists(store))
            {
                Console.WriteLine("Specified store does not exist, generating template");
                File.WriteAllText(store, JsonConvert.SerializeObject(new FileStoreInfo(), Formatting.Indented));
                Console.WriteLine("Please edit the json file and restart the program");
                return;
            }
            var info = JsonConvert.DeserializeObject<FileStoreInfo>(File.ReadAllText(store));
            info.Save = (x) => File.WriteAllText(store, JsonConvert.SerializeObject(x,Formatting.Indented));
            var fileStore = new FileStore(info);

            var dir = Directory.GetParent(mountPoint).FullName;
            Console.WriteLine("Mount point: " + mountPoint);
            Directory.CreateDirectory(dir);
            if (Directory.EnumerateFileSystemEntries(dir).Any())
            {
                throw new InvalidOperationException("Mount directory must be empty");
            }
#if TEST
            patches.ToList().ForEach(x => { if (File.Exists(x)) { File.Delete(x); } });
#endif


            var provider = fileStore.GetProvider(patchId == null ? default : Guid.Parse(patchId), !_readonly);
            var mount = new SingleFileMount(provider, Path.GetFileName(mountPoint), null);
            using var dokan = new Dokan(null);
            var builder = new DokanInstanceBuilder(dokan)
                .ConfigureOptions(options =>
                {
                    options.Options = DokanOptions.MountManager;
                    options.MountPoint = dir;
                });
            var dokanInstance = builder.Build(mount);
#if TEST
            Test("replicated", provider);
#endif

            while (true)
            {
                var cs = Console.ReadLine().Split(' ');
                switch (cs[0])
                {
                    case "clear": Console.Clear(); break;
                    case "dump": provider.DumpFragments(); break;
                    case "exit": Console.Clear(); goto stop;
                    case "check": provider.Check(); break;
                    case "flush": provider.Flush(); break;
                    case "read":
                        var st = provider.GetStream();
                        st.Position = long.Parse(cs[1]);
                        var data = new byte[64];
                        Console.WriteLine($"Read { st.Read(data, 0, data.Length)} bytes:");
                        Console.WriteLine(data.Dump());
                        st.Dispose();
                        break;
                    case "hash":
                        var s = provider.GetStream();
                        Console.WriteLine(Util.Hash(s));
                        s.Dispose();
                        break;
                }
            }
        stop:
            provider.Dispose();
        }

        static void Test(string replicated, FileProvider provider)
        {
            var replicate = File.Create(replicated);
            var prov = provider.GetStream();
            Console.WriteLine("Copying...");
            prov.CopyTo(replicate);
            // Verify();
            for (int i = 0; i < 5000; i++)
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
                var secs = s1.Length / 32;
                var loca = Util.RandLong(0, secs) * 32;
                s1.Position = s2.Position = loca;
                var data = new byte[32];
                Util.RandBytes(data);
                //Console.WriteLine($"Writing chunk: {s1.Position} [{data[0]},{data[1]},{data[2]}]");
                s1.Write(data);
                s2.Write(data);
                Resize();
                if (s1.Position != s2.Position) { throw new Exception($"{s1.Position} {s2.Position}"); }
                Util.RandBytes(data);
                //Console.WriteLine($"Writing chunk: {s1.Position} [{data[0]},{data[1]},{data[2]}]");
                s1.Write(data);
                s2.Write(data);
            }
            void Resize()
            {
                CheckLength();
                var min = prov.Length - 4096 * 1024 * 16;
                if (min < 0) { min = 0; }
                var size = Util.RandLong(min, prov.Length + 1024 * 1024);
                //Console.WriteLine("Resizing to " + size);
                prov.SetLength(size);
                replicate.SetLength(size);
            }
            void CheckLength()
            {
                if (replicate.Length != prov.Length) { throw new Exception("length fail"); }
            }
            void Verify()
            {
                Console.WriteLine("Verifying");
                CheckLength();
                replicate.Position = prov.Position = 0;
                var bufferRep = new byte[64];
                var bufferProv = new byte[bufferRep.Length];

                while (replicate.Position < replicate.Length)
                {
                    if (prov.Position != replicate.Position) { throw new Exception($"{prov.Position} {replicate.Position}"); }
                    // Console.WriteLine("Verifying position: "+prov.Position);
                    int read;
                    if ((read = prov.Read(bufferProv, 0, bufferProv.Length)) != replicate.Read(bufferRep, 0, bufferRep.Length))
                    {
                        throw new Exception("read");
                    }
                    if (!bufferRep.SequenceEqual(bufferProv))
                    {
                        Console.WriteLine($"Wrong data: {replicate.Position - read}, length:{prov.Length}");
                        Console.WriteLine("replicated: " + string.Join(',', bufferRep));

                        Console.WriteLine("provider: " + string.Join(',', bufferProv));
                        var s2 = provider.GetStream();

                        bufferProv = Enumerable.Repeat((byte)123, bufferProv.Length).ToArray(); ;
                        s2.Position = prov.Position - read;
                        s2.Read(bufferProv, 0, bufferProv.Length);
                        Console.WriteLine("provider2: " + string.Join(',', bufferProv));
                        s2.Dispose();
                        Console.ReadLine();
                        // break;
                    }
                }
                Console.WriteLine("Verify done");
                return;

            }
        }
    }
}