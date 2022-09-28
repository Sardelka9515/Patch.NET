using System.Diagnostics;
using System.Threading;
using System.Collections.Concurrent;

namespace PatchDotNet.Win32
{
    public class VdiskInfo
    {
        public readonly string DeviceTypeID;
        public readonly string VendorID;
        public readonly string State;
        public readonly string VirtualSize;
        public readonly string PhysicalSize;
    }
    public static class DiskPart
    {
        static Process _process = new();
        public static event EventHandler<string> OutputReceived;
        static DiskPart()
        {
            _process.StartInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                FileName = "cmd.exe",
                Arguments = "/c diskpart",
                Verb = "runas",
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };
            _process.EnableRaisingEvents = true;
            _process.Exited += (s, e) => _process = null;
            _process.ErrorDataReceived += (s, e) => { throw new Exception("DISKPART error: " + e.Data); };
            _process.Start();
            _process.BeginErrorReadLine();
            WaitOutput();
        }
        public static string[] Execute(string command,string successFragment=null)
        {
            _process.StandardInput.WriteLine(command);
            var r = WaitOutput();
            if(successFragment!=null && !r.Where(x=>x.Contains(successFragment)).Any())
            {
                throw new Exception("DiskPart operation failed: " + r.JoinLines());
            }
            return r;
        }
        public static void Format(uint diskIndex, uint partIndex, string fs,bool quick)
        {
            SelectDisk(diskIndex);
            SelectPartition(partIndex);
            var command = "format ";
            if (quick) { command += "quick "; }
            command += "fs=" + fs;
            Execute(command, "DiskPart successfully");
        }
        public static void Remove(uint disk, uint part,string mountPoints="all")
        {
            SelectDisk(disk);
            SelectPartition(part);
            Execute("remove "+mountPoints, "DiskPart successfully");
        }
        public static void Clean(uint index,bool all=false)
        {
            SelectDisk(index);
            Execute("clean"+(all ? " all" : ""), "DiskPart succeeded in cleaning the disk.");
        }
        public static void SelectDisk(uint index)
        {
            Execute("sel dis " + index, "is now the selected disk");
        }
        public static void SelectPartition(uint index)
        {
            Execute("sel part " + index, "is now the selected partition");
        }
        public static void Attach(string vdiskfile)
        {
            SelectVDisk(vdiskfile);
            Execute("attach vdisk", "DiskPart successfully");
        }
        public static void SelectVDisk(string vdiskfile)
        {
            Execute($"sel vdisk file=\"{vdiskfile}\"", "DiskPart successfully");
        }
        public static bool IsVdiskAttached(string vdiskfile)
        {
            SelectVDisk(vdiskfile);
            var result = Execute("detail vdisk", "Device type ID").Where(x=>x.StartsWith("State")).FirstOrDefault();
            return result?.Split(": ")[1].Contains("Attached") ?? false;
        }
        public static void Detach(string vdiskfile)
        {
            SelectVDisk(vdiskfile);
            Execute("detach vdisk", "DiskPart successfully");
        }
        static string[] WaitOutput()
        {
            var s = "";
            while (!s.EndsWith("DISKPART>"))
            {
                s += (char)_process.StandardOutput.Read();
            }
            OutputReceived?.Invoke(null,s);
            var lines = s.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            return lines.Skip(1).Take(lines.Length - 2).ToArray();
        }
    }
}
