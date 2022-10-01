using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync
{
    internal class ClientSettings : Settings
    {
        public ClientSettings(string path) : base(path) { }
        public string Server { get; set; } = "127.0.0.1:5728";
        public string BaseDirectory { get; set; } = "client";
        public string ClientID { get; set; }
    }
}
