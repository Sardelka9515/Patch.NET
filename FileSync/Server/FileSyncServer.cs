using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SausageIPC;
using PatchDotNet;
namespace FileSync.Server
{
    internal class FileSyncServer
    {
        readonly IpcServer<Client> server;
        readonly ServerSettings _settings;
        readonly FileStore _store;
        DateTime _lastLockRequested;
        public Client LockHolder { get; private set; }
        public FileSyncServer(ServerSettings settings)
        {
            _settings = settings;
            _store = new(FileStoreInfo.FromJson(Path.Combine(settings.BaseDirectory, "FileStore.pfs"));
            server = new(settings.Address);
            server.OnHandshake += Handshake;
            server.OnMessageReceived += Received;
            server.OnClientConnected += Connected;
            server.OnQuerying += Querying;
        }
        bool Lock(Client c)
        {
            if (LockHolder != null && c != LockHolder && DateTime.UtcNow - _lastLockRequested < _settings.LockTimeOut)
            {
                return false;
            }
            LockHolder = c;
            _lastLockRequested = DateTime.UtcNow;
            return true;
        }
        bool Unlock(Client c)
        {
            if (c == LockHolder || LockHolder == null) 
            {
                _lastLockRequested = DateTime.MinValue;
                LockHolder = null;
                return true; 
            }
            return false;
        }
        private void Querying(object sender, QueryEventArgs<Client> e)
        {
            var msg = e.Query;
            var client = e.Sender;
            try
            {

                switch ((MessageType)int.Parse(msg.MetaData["Type"]))
                {
                    case MessageType.RequestPatchTransfer:
                        var fromVersion = Guid.Parse(msg.MetaData["FromVersion"]);
                        _store.Patches[fromVersion]
                        break;
                    default: server.Disconnect(client, "shut up"); break;
                }
            }
            catch (Exception ex)
            {
                server.Disconnect(client, ex.Message);
            }
        }

        private void Connected(object sender, Client e)
        {
            e.Id = Guid.Parse(e.Alias);
            if (!_settings.Clients.ContainsKey(e.Id))
            {
                SaveClients();
            }
        }

        private void Received(object sender, MessageReceivedEventArgs<Client> e)
        {
            var msg = e.Message;
            var client = e.Sender;
            try
            {

                switch ((MessageType)int.Parse(msg.MetaData["Type"]))
                {
                    case MessageType.BeginPatchTransfer:
                        if (msg.Data.Length != Patch.HeaderSize)
                        {
                            throw new DataMisalignedException("Incorrect patch header size");
                        }
                        var patch = new Patch(new MemoryStream(msg.Data));
                        if (client.PatchStream != null) { throw new Exception("Already transferring"); }
                        client.PatchStream = File.Create(Path.Combine(_settings.BaseDirectory, patch.Guid.ToString()));
                        patch.Dispose();
                        break;
                    case MessageType.TransferPatchData:
                        if (client.PatchStream == null)
                        {
                            throw new Exception("Not transferring");
                        }
                        client.PatchStream.Write(msg.Data);
                        break;
                    case MessageType.EndPatchTransfer:
                        if (client.PatchStream == null)
                        {
                            throw new Exception("Not transferring");
                        }
                        client.PatchStream.Close();
                        break;
                    default: server.Disconnect(client, "shut up"); break;
                }
            }
            catch (Exception ex)
            {
                server.Disconnect(client, ex.Message);
            }
        }

        private void Handshake(object sender, HandshakeEventArgs e)
        {
            try
            {
                var id = Guid.Parse(e.Alias);

                e.Aprrove();
                if (server.Clients.Values.Any(x => x.Id == id))
                {
                    throw new Exception("Another client with the same id already connected");
                }
            }
            catch (Exception ex)
            {
                e.Deny(ex.Message);
            }
        }
        void SaveClients()
        {
            _settings.Clients.Clear();
            foreach (var c in server.Clients.Values)
            {
                _settings.Clients.Add(c.Id, c.Version);
            }
            _settings.Save();
        }
    }
}
