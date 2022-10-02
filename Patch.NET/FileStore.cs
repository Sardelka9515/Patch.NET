using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace PatchDotNet
{
    public class FileStore
    {
        public string Name { get; set; }
        public readonly string BaseFile;
        public string BaseDirectory => _info.BaseDirectory;
        public readonly PatchNode Root;
        public readonly Dictionary<Guid, PatchNode> Patches = new();
        readonly FileStoreInfo _info;
        public FileStore(FileStoreInfo info, List<string> deletedPatches = null)
        {
            deletedPatches = new();
            _info = info;
            BaseFile = info.ToAbsolute(info.BaseFile);
            Name = _info.Name;
            Root = new PatchNode()
            {
                ID = Guid.Empty,
                Parent = null,
                Name = "Base",
                Path = BaseFile
            };
            Patches.Add(Root.ID, Root);
            foreach (var p in info.Patches)
            {
                var path = _info.ToAbsolute(p);
                try
                {

                    if (!File.Exists(path))
                    {
                        throw new FileNotFoundException("Failed to locate patch: " + path);
                    }
                    var pa = new PatchNode(path);
                    if (Patches.ContainsKey(pa.ID))
                    {
                        var existing = Patches[pa.ID];
                        throw new ArgumentException($"GUID exists: {pa.ID}:{pa.Path}, {existing.Path}");
                    }
                    Patches.Add(pa.ID, pa);
                }
                catch
                {
                    if (deletedPatches != null)
                    {
                        deletedPatches.Add(path);
                        continue;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            RebuildTree(deletedPatches);
            Reflect();
        }
        public void RebuildTree(List<string> deletePatches = null)
        {
            Patches.Values.ForEach(x => x.Children.Clear());
            // Build tree
            foreach (var p in Patches.Values)
            {
                try
                {
                    if (p == Root) { continue; }
                    var parent = Patches[p.ParentID];
                    p.Parent = parent;
                    parent.Children.Add(p);
                }
                catch
                {

                    if (deletePatches != null)
                    {
                        Patches.Remove(p);
                        deletePatches.Add(p.Path);
                        continue;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            if (deletePatches != null && deletePatches.Any()) { Reflect(); }
        }
        public bool Merge(Guid patch, int level, string output, string name, Func<int, int, long, long, bool> mergeConfirm = null)
        {
            var toMerge = BuildChain(patch);
            toMerge = toMerge.Skip(toMerge.Count - level).ToList();
            if (toMerge.Take(toMerge.Count - 1).Where(x => x.Children.Count > 1).Any())
            {
                throw new InvalidOperationException("Cannot merge some patches because there're other patches that depend on them");
            }
            var tempPath = output + ".merging"; 

            var tempStream = File.Create(tempPath);
            var source = Patches[patch];
            var map = source.Path + ".blockmap";
            var children = source.Children.Select(x => new Patch(x.Path, true)).ToArray();
            var parent = toMerge.First().Parent;
            FileProvider prov = null;
            try
            {
                if (File.Exists(map)) { File.Delete(map); } // Delete cached blockmap
                prov = GetProvider(source, false);
                if (!prov.Merge(level, tempStream, name, children, mergeConfirm))
                {
                    prov.Dispose();
                    children.ForEach(x => x.Dispose());
                    tempStream.Dispose();
                    if (File.Exists(tempPath)) { File.Delete(tempPath); }

                    return false;
                }
                children.ForEach(x => x.Dispose());
                prov.Dispose();
                tempStream.Dispose();
                toMerge.ForEach(x =>
                {
                    File.Move(x.Path, x.Path + ".old");
                });
                File.Move(tempPath, output);
                toMerge.ForEach(x =>
                {
                    Patches.Remove(x);
                    File.Delete(x.Path + ".old");
                });
                var n = new PatchNode(output);
                Patches.Add(n, n);
                RebuildTree(new());
            }
            catch
            {
                try { prov?.Dispose(); } catch { }
                try { tempStream?.Dispose(); } catch { }
                if (File.Exists(tempPath)) { try { File.Delete(tempPath); } catch { } }
                throw;
            }
            Reflect();
            return true;
        }
        public bool Optimize(Guid patch, Func<int, int, long, long, bool> mergeConfirm = null)
        {
            var p = Patches[patch];

            if (p.IsRoot) { throw new InvalidOperationException("Invalid patch"); }
            return Merge(patch, 1, p.Path, p.Name, mergeConfirm);
        }
        public FileProvider GetProvider(Guid patchId, bool canWrite)
        {
            if (patchId == default && !Patches.ContainsKey(patchId))
            {
                throw new Exception("Please select a valid patch");
            }
            var chain = BuildChain(patchId);
            if (canWrite && chain[^1].Children.Count > 0)
            {
                throw new InvalidOperationException("Cannot open this patch as r/w because one or more patches depend on it");
            }
            var last = chain.Last();
            var mapping = chain.Last().Path + ".blockmap";
            var info = new FileInfo(BaseFile);
            var bs = info.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            var patches = chain.Select(x => new Patch(x.Path, x == last && canWrite)).ToArray();
            try
            {

                if (File.Exists(mapping))
                {
                    try
                    {
                        var map = new BlockMap(File.OpenRead(mapping), bs, patches);
                        return new FileProvider(map, info.CreationTime);
                    }
                    catch (OutdatedMappingException ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
                return new FileProvider(bs, info.CreationTime, null, patches);
            }
            catch
            {
                bs.Dispose();
                patches.ForEach(x => x.Dispose());
                throw;
            }

        }
        public List<PatchNode> BuildChain(Guid patchId)
        {
            var p = Patches[patchId];
            if (p.IsRoot) { throw new InvalidOperationException("Invalid patch"); }
            List<PatchNode> chain = new() { p };

            // Build patch chain
            while (chain[0].Parent != Root)
            {
                chain.Insert(0, chain[0].Parent);
            }
            return chain;
        }
        public void DisposeProvider(FileProvider p, bool saveMapping)
        {
            if (saveMapping)
            {
                var map = new BlockMap(p);
                var pa = p.Patches.LastOrDefault();
                if (pa != null)
                {
                    map.Save(pa.Path + ".blockmap");
                }
            }
            p.Dispose();
        }

        /// <summary>
        /// Create a new patch and redirect subsequent write to it
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="path"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public PatchNode CreatePatch(FileProvider provider, string path, string name)
        {
            if (File.Exists(path)) { throw new InvalidOperationException("File already exists: " + path); }
            if (Path.GetFullPath(provider.BasePath) != Path.GetFullPath(BaseFile)
                || provider.Patches.Where(x => !Patches.ContainsKey(x.Guid)).Any())
            {
                throw new InvalidOperationException($"Provider does not belong to this {nameof(FileStore)}");
            }
            provider.ChangeCurrent(path, name);
            var node = new PatchNode(path);
            Patches.Add(node.ID, node);
            RebuildTree();
            Reflect();
            return node;
        }

        /// <summary>
        /// Create a child patch from specified parent
        /// </summary>
        /// <param name="parentId"></param>
        /// <param name="path"></param>
        public PatchNode CreatePatch(Guid parentId, string path, string name)
        {
            if (!Patches.TryGetValue(parentId, out var parent)) { throw new KeyNotFoundException("Specified parent does not exist: " + parentId); }
            parent.Update();
            using var p = new Patch(parent.Path, false);
            p.CreateChild(path, name);
            var node = new PatchNode(path);
            Patches.Add(node.ID, node);
            RebuildTree();
            Reflect();
            return node;
        }

        /// <summary>
        /// Create new patch from base file
        /// </summary>
        /// <param name="path"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public PatchNode CreatePatch(string path, string name)
        {
            if (File.Exists(path)) { throw new InvalidOperationException("File already exists: " + path); }
            new Patch(path, true)
            {
                Parent = Guid.Empty,
                Attributes = new FileInfo(BaseFile).Attributes,
                Name = name,
                ParentLength = new FileInfo(BaseFile).Length
            }.Dispose();

            var node = new PatchNode(path);
            Patches.Add(node.ID, node);
            RebuildTree();
            Reflect();
            return node;
        }
        public void RemovePatch(Guid id, bool deleteFile = false, bool deleteChidren = false)
        {
            var p = Patches[id];
            var chidren = p.Children;
            if (chidren.Any())
            {
                if (!deleteChidren) { throw new InvalidOperationException("Cannot delete this patch because one or more patches depend on it."); }
                chidren.ToList().ForEach(x => RemovePatch(x.ID, deleteFile, true));
            }
            Console.WriteLine("Removing patch: " + p.Path);
            if (deleteFile)
            {
                File.Delete(p.Path);
            }
            Patches.Remove(id);
            p.Parent.Children.Remove(p);
            Reflect();
        }
        public void RecoverOrphanPatch(string path)
        {
            var p = new PatchNode(path);
            if (Patches.ContainsKey(p.ID)) { throw new InvalidOperationException("Specified patch already exists in this FileStore"); }
            if (!Patches.ContainsKey(p.ParentID) || p.ID == Guid.Empty)
            {
                throw new KeyNotFoundException("Patch parent not found in this FileStore");
            }
            Patches.Add(p.ID, p);
            RebuildTree();
            Reflect();
        }
        void Reflect()
        {
            _info.Name = Name;
            _info.Patches = Patches.Values.Where(x => !x.IsRoot).Select(x => Path.GetRelativePath(_info.BaseDirectory, x.Path)).ToArray();
            _info.Save?.Invoke(_info);
        }

    }
    public class FileStoreInfo
    {
        public static FileStoreInfo FromJson(string path)
        {
            FileStoreInfo info;
            if (!File.Exists(path))
            {
                Console.WriteLine("Specified store does not exist, generating template");
                info = new FileStoreInfo();
            }
            info = JsonConvert.DeserializeObject<FileStoreInfo>(File.ReadAllText(path));
            info.Name ??= Path.GetFileNameWithoutExtension(path);
            info.Save = (x) => File.WriteAllText(path, JsonConvert.SerializeObject(x, Formatting.Indented));
            info.BaseDirectory ??= Directory.GetParent(path).FullName;
            info.Save(info);

            return info;
        }
        public string Name;
        [JsonIgnore]
        public string BaseDirectory;
        public string BaseFile = @"base.vhdx";
        public string ToAbsolute(string path)
        {
            if (Path.IsPathRooted(path)) { return path; }
            return Path.Combine(BaseDirectory, path);
        }

        public string[] Patches = new string[] { "default.patch" };
        [JsonIgnore]
        public Action<FileStoreInfo> Save;
    }
}
