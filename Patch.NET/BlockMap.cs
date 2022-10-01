/*
 * 
      Schema:
        {patchCount}{patch1.Guid}{patch1.LastWrite}...
        {fragCount}{frag1.StartPosition}{frag1.EndPosition}{frag1.ReadPosition}{frag1.PatchIndex(-1 for null)}...
        {BlockMap.EOF}

 * */
using PatchDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PatchDotNet
{
    /// <summary>
    /// A class to export/import file fragments mapping in memory
    /// </summary>
    public class BlockMap : IDisposable
    {
        public Patch[] Patches;
        public FileFragement[] Fragments;
        public Stream BaseStream;
        public BlockMap(Stream mapStream, Stream baseStream, Patch[] patches, bool closeStream = true)
        {
            BaseStream = baseStream;
            BinaryReader reader = new(mapStream);
            Patches = patches;
            Patch parent = null;
            if (patches.Length != reader.ReadInt32())
            {
                throw new BrokenPatchChainException("Spicified patch length does not match the mapping");
            }
            for (var i = 0; i < Patches.Length; i++)
            {
                var patch = Patches[i];
                var id = new Guid(reader.ReadBytes(16));
                if (patch.Guid != id)
                {
                    throw new BrokenPatchChainException($"The patch chain is broken at index {i}. Stated guid: {id}, actual: {patch.Guid}");
                }
                if (!patch.IsChildOf(parent, BaseStream.Length))
                {
                    throw new BrokenPatchChainException($"The patch chain is broken at index {i}. Actual parent is {(Guid)parent}=>{parent?.Length ?? BaseStream.Length}, expecting {patch.Parent}=>{patch.ParentLength}");
                }
                if (patch.LastWriteTimeUtc != DateTime.FromBinary(reader.ReadInt64()))
                {
                    throw new OutdatedMappingException($"Outdated BlockMap for patch: " + patch.Path);
                }
                parent = patch;
            }
            Fragments = new FileFragement[reader.ReadInt32()];
            for (int i = 0; i < Fragments.Length; i++)
            {
                Fragments[i] = new FileFragement()
                {
                    StartPosition = reader.ReadInt64(),
                    EndPosition = reader.ReadInt64(),
                    ReadPosition = reader.ReadInt64(),
                    Stream = GetStream(reader.ReadInt32())
                };

            }
            Stream GetStream(int index)
            {
                if (index == -1) { return null; }
                else if (index == -2) { return BaseStream; }
                return Patches[index].Reader.BaseStream;
            }
            if (reader.ReadString() != "BlockMap.EOF")
            {
                throw new DataMisalignedException("Unexpected BlockMap EOF");
            }
            if (closeStream)
            {
                reader.Close();
                reader.Dispose();
            }
            Console.WriteLine("Successfully loaded blockmap");
        }
        public BlockMap(FileProvider p)
        {
            Patches = p.Patches.ToArray();
            Fragments = p.Fragments.ToArray();
            BaseStream = p.BaseStream;
        }
        public void Save(string path)
        {
            var s = File.Create(path);
            Save(s);
            s.Close();
            s.Dispose();
        }
        public void Save(Stream output)
        {
            var writer = new BinaryWriter(new BufferedStream(output));
            writer.Write(Patches.Length);
            foreach (var p in Patches)
            {
                writer.Write(p.Guid.ToByteArray());
                writer.Write(p.LastWriteTimeUtc.ToBinary());
            }
            writer.Write(Fragments.Length);
            foreach (var f in Fragments)
            {
                writer.Write(f.StartPosition);
                writer.Write(f.EndPosition);
                writer.Write(f.ReadPosition);
                writer.Write(GetIndex(f.Stream));
            }
            writer.Write("BlockMap.EOF");
            int GetIndex(Stream s)
            {
                if (s == null) { return -1; }
                else if (s == BaseStream) { return -2; }
                for (int i = 0; i < Patches.Length; i++)
                {
                    if (Patches[i].Reader.BaseStream == s)
                    {
                        return i;
                    }
                }
                throw new Exception("Given fragment's stream not found in patches: " + (s as FileStream)?.Length);
            }
            writer.Flush();
        }
        /// <summary>
        /// Free all streams and patches in this mapping, should not be called normally unless a exception were to be encountered
        /// </summary>
        /// <remarks>Calling this will cause the <see cref="FileProvider"/> that initialized with this instance to malfunction</remarks>
        /// <exception cref="NotImplementedException"></exception>
        public void Dispose()
        {
            BaseStream?.Dispose();
            Patches.ForEach(x => x?.Dispose());
        }
    }
}
