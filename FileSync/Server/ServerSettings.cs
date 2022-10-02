using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Server
{
    internal class ServerSettings : Settings
    {
        public ServerSettings(string path) : base(path) { }
        public string Address { get; set; } = "127.0.0.1:5728";
        public string BaseDirectory { get; set; } = "server";
        public Dictionary<Guid,Guid> Clients { get; set; }
        public Guid CurrentVersion { get;set; }
        public TimeSpan LockTimeOut { get; set; } = TimeSpan.FromMinutes(2);
    }
}
