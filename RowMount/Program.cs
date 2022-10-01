﻿// #define TEST
using PatchDotNet.Win32;
using PatchDotNet;
using DokanNet;
using Newtonsoft.Json;

namespace RoWMount
{
    public class Program
    {
        static FileStore store = null;
        public static void Main(string[] a)
        {
            var commands = new ConsoleCommand();

            commands.AddCommand("select", cs => Select(cs[0]),
                "select fileStorePath", "select the json file representing the FileStore");

            commands.AddCommand("mount", cs => Mount(cs[0], cs[1], bool.Parse(cs[2])),
                "mount mountPoint patchId canWrite", "mount the file in specified mount point");

            commands.AddCommand("list", cs => ListPatches(cs),
                "list [parenId]", "list all patches of selected FileStore or chidren of specified patch");

            commands.AddCommand("create", cs => CreatePatch(cs),
                "create path [parentId]", "Create a new patch based on specified parent or base file");

            commands.AddCommand("clear", cs => Console.Clear(),
                "clear", "clear the console buffer");
            commands.AddCommand("remove", cs => store?.RemovePatch(Guid.Parse(cs[0]), bool.Parse(cs[1]), bool.Parse(cs[2])),
                "remove patchId deleteChildren deleteFile", "remove the specified patch");
#if DEBUG
            Select("test\\defaultstore.json");
#endif
            Console.WriteLine("Avalible commands:");
            Console.WriteLine(commands.GetText());
            while (true)
            {
                try
                {
                    var line = Console.ReadLine();
                    if (line == "exit")
                    {
                        break;
                    }
                    commands.Run(line);
                }
                catch (IndexOutOfRangeException)
                {
                    Console.WriteLine(commands.GetText());
                }
                catch( Exception ex)
                {
                    Console.WriteLine(ex);
#if DEBUG
                    throw;
#endif
                }
            }

        }

        private static void CreatePatch(string[] cs)
        {
            if (store == null)
            {
                Console.WriteLine("No FileStore selected");
                return;
            }
            var path = cs[0];
            var parent = cs.Length > 1 ? cs[1] : null;
            var name = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            if (parent != null)
            {
                store.CreatePatch(Guid.Parse(parent), path,name);
            }
            else
            {
                store.CreatePatch(path,name);
            }
        }

        static void ListPatches(string[] _parent)
        {
            if (store == null)
            {
                Console.WriteLine("No FileStore selected");
                return;
            }
            var parent = _parent.Length > 0 ? _parent[0] : null;
            if (parent != null)
            {
                if (!store.Patches.TryGetValue(Guid.Parse(parent),out var pp))
                {
                    Console.WriteLine("Specified patch was not found");
                    return;
                }
                Show(pp);
            }
            foreach (var p in parent == null ? store.Patches.Values.ToList() : store.Patches[Guid.Parse(parent)].Children)
            {
                if (p.ID == Guid.Empty) { continue; }
                Show(p);
            }
            void Show(PatchNode node)
            {
                Console.WriteLine(new String('=',Console.WindowWidth));
                var patch = new Patch(node.Path,false);
                Console.WriteLine("{0,-20} {1,-50}", "Name:", patch.Name);
                Console.WriteLine("{0,-20} {1,-50}", "Guid:", patch.Guid);
                Console.WriteLine("{0,-20} {1,-50}", "Parent:", patch.Parent);
                Console.WriteLine("{0,-20} {1,-50}", "Full path:", patch.Path);
                Console.WriteLine("{0,-20} {1,-50}", "Size:", Util.FormatSize(patch.Reader.BaseStream.Length));
                Console.WriteLine("{0,-20} {1,-50}", "Defragmented:", patch.LastDefragmented);
                patch.Dispose();
            }
        }
        static void Select(string path)
        {
            store = new FileStore(FileStoreInfo.FromJson(path));

        }
        static void Mount(string mountPoint, string guid, bool canWrite)
        {
            if (store == null)
            {
                Console.WriteLine("No FileStore selected");
                return;
            }
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


            var provider = store.GetProvider(Guid.Parse(guid), canWrite);
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
                    case "unmount": Console.Clear(); goto stop;
                    case "check": provider.Check(); break;
                    case "flush": provider.Flush(); break;
                    case "create": store.CreatePatch(provider, cs[1], cs[2]); break;
                    case "read":
                        var st = provider.GetStream();
                        st.Position = long.Parse(cs[1]);
                        var data = new byte[64];
                        Console.WriteLine($"Read {st.Read(data, 0, data.Length)} bytes:");
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
            dokanInstance.Dispose();
            provider.Dispose();
            Console.WriteLine("File closed");
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