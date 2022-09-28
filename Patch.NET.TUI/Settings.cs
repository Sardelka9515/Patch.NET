using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PatchDotNet.TUI
{
    public class Settings
    {
        public string LastSelected = Guid.Empty.ToString();
        public string MountPoint = "mount";
        public string Filename = "File.vhdx";
        public bool MountReadonly = false;
    }
}
