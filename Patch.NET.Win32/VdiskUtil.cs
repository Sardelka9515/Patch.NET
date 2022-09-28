using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vanara.IO;
using FLAGS=Vanara.PInvoke.VirtDisk.ATTACH_VIRTUAL_DISK_FLAG;

namespace PatchDotNet.Win32
{
    public static class VdiskUtil
    {
        public static Dictionary<string, VirtualDisk> Mounted=new();
        public static void AttachVhd(string path, bool readOnly)
        {
            if (VirtualDisk.IsAttached(path)||Mounted.ContainsKey(path)) { return; }
            var disk = VirtualDisk.Open(path, readOnly);
            disk.Attach(FLAGS.ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME|(readOnly ? FLAGS.ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY : FLAGS.ATTACH_VIRTUAL_DISK_FLAG_NONE));
            Mounted.Add(path, disk);
        }
        public static bool DetachVhd(string path)
        {
            if(Mounted.TryGetValue(path, out VirtualDisk disk))
            {
                disk.Detach();
                Mounted.Remove(path);
            }
            else
            {
                VirtualDisk.Detach(path);
            }
            return true;
        }
        public static bool IsAttched(string path)
        {
            return VirtualDisk.IsAttached(path);
        }
    }
}
