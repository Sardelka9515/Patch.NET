using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PatchDotNet
{
    public class FileStore : IDisposable
    {
        public readonly string BaseFile;
        public readonly Dictionary<Guid, Patch> Patches = new();
        readonly FileStoreInfo _info;
        public FileStore(FileStoreInfo info)
        {
            _info = info;
            BaseFile = info.BaseFile;
            foreach (var p in info.Patches)
            {
                var patch = new Patch(p, false);
                Patches.Add(patch.Guid, patch);
            }
        }
        public FileProvider GetProvider(Guid patchId, bool canWrite)
        {
            if (patchId == default)
            {
                patchId = Patches.First().Key;
            }

            List<Patch> chain = new() { Patches[patchId] };

            // Build patch chain
            while (chain[0].Parent != Guid.Empty)
            {
                chain.Insert(0, Patches[chain[0].Parent]);
            }
            if (canWrite && Patches.Where(x => x.Value.Parent == patchId).Any())
            {
                throw new InvalidOperationException("Cannot open this patch as r/w because one or more patches depend on it");
            }
            return new FileProvider(BaseFile, canWrite, null, chain.Select(x => x.Path).ToArray());
        }

        /// <summary>
        /// Create a new patch and redirect subsequent write to it
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="path"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void CreatePatch(FileProvider provider, string path)
        {
            if (File.Exists(path)) { throw new InvalidOperationException("File already exists: " + path); }
            if (Path.GetFullPath(provider.BasePath) != Path.GetFullPath(BaseFile)
                || provider.Patches.Where(x => !Patches.ContainsKey(x.Guid)).Any())
            {
                throw new InvalidOperationException($"Provider does not belong to this {nameof(FileStore)}");
            }
            provider.ChangeCurrent(path);
            var patch = new Patch(path, false);
            Patches.Add(patch.Guid, patch);
            Reflect();
        }

        /// <summary>
        /// Create a child patch of specified parent
        /// </summary>
        /// <param name="parentId"></param>
        /// <param name="path"></param>
        public void CreatePatch(Guid parentId, string path)
        {
            if (File.Exists(path)) { throw new InvalidOperationException("File already exists: " + path); }
            if (!Patches.TryGetValue(parentId, out var parent)) { throw new KeyNotFoundException("Specified parent does not exist: " + parentId); }

            var patch = new Patch(path, true);
            patch.Attributes = parent.Attributes;
            patch.Parent = parentId;
            patch.Dispose();

            patch = new Patch(path, false);
            Patches.Add(patch.Guid, patch);
            Reflect();
        }

        /// <summary>
        /// Create new patch from base file
        /// </summary>
        /// <param name="path"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void CreatePatch(string path)
        {
            if (File.Exists(path)) { throw new InvalidOperationException("File already exists: " + path); }
            var patch = new Patch(path, true);
            patch.Parent = Guid.Empty;
            patch.Attributes = new FileInfo(BaseFile).Attributes;
            patch.Dispose();

            patch = new Patch(path, false);
            Patches.Add(patch.Guid, patch);
            Reflect();
        }
        public void RemovePatch(Guid id, bool deleteFile = false, bool deleteChidren = false)
        {
            var p = Patches[id];
            var chidren = Patches.Where(x => x.Value.Parent == id);
            if (chidren.Any())
            {
                if (!deleteChidren) { throw new InvalidOperationException("Cannot delete this patch because one or more patches depend on it."); }
                chidren.ForEach(x => RemovePatch(x.Key, deleteFile, true));
            }
            Console.WriteLine("Removing patch: " + p.Path);
            var path = p.Path;
            p.Dispose();
            if (deleteFile)
            {
                try
                {

                    File.Delete(path);
                }
                catch
                {
                    // Delete failed, reopen patch
                    p = Patches[id] = new Patch(path, false);
                    throw;
                }
            }
            Patches.Remove(id);
            Reflect();
        }
        void Reflect()
        {
            _info.Patches = Patches.Select(x => Path.GetRelativePath(Directory.GetCurrentDirectory(), x.Value.Path)).ToArray();
            _info.Save?.Invoke(_info);
        }

        public void Dispose()
        {
            Patches.ForEach(x => x.Value.Dispose());
            Patches.Clear();
        }
    }
    public class FileStoreInfo
    {
        public static FileStoreInfo FromJson(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("Specified store does not exist, generating template");
                File.WriteAllText(path, JsonConvert.SerializeObject(new FileStoreInfo(), Formatting.Indented));
            }
            var info = JsonConvert.DeserializeObject<FileStoreInfo>(File.ReadAllText(path));
            info.Save = (x) => File.WriteAllText(path, JsonConvert.SerializeObject(x, Formatting.Indented));
            return info;
        }
        public string BaseFile = @"base.vhdx";

        public string[] Patches = new string[] { "default.patch" };
        [JsonIgnore]
        public Action<FileStoreInfo> Save;
    }
}
