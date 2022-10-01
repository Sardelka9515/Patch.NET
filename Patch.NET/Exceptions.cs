using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PatchDotNet
{
    public class OutdatedMappingException:Exception
    {
        public OutdatedMappingException(string msg) : base(msg) { }
    }
    public class BrokenPatchChainException : Exception
    {
        public BrokenPatchChainException(string msg) : base(msg) { }
    }

    public class PatchCorrutpedException : Exception
    {
        public long LastIntactPosition;
        public PatchCorrutpedException(long lastIntact,string msg,Exception inner) : base(msg,inner) { LastIntactPosition = lastIntact; }
    }
}
