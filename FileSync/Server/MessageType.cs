using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Server
{
    internal enum MessageType
    {
        BeginPatchTransfer,
        TransferPatchData,
        EndPatchTransfer,
        #region QUERY
        GetLastVersion,
        RequestPatchTransfer,
        Lock,
        Unlock,

        #endregion
    }
}
