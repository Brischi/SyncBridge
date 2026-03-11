using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncBridge.ApplicationLayer
{
    public interface ISyncStrategy
    {
        Task<SyncResult> SyncAsync();
        Task CleanTargetAsync();
    }
}
