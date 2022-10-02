using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Server
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class Client : SausageIPC.ClientBase
    {
        [JsonProperty]
        public Guid Id;

        [JsonProperty]
        public Guid Version;
        
        public FileStream PatchStream;
    }
}
