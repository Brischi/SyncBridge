using SyncBridge.Core;
using SyncBridge.Core.Dolibarr;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncBridge.ApplicationLayer
{
    public interface ISyncStrategyFactory
    {
        ISyncStrategy Create(SyncCategory category, SyncDirection direction);

    }
}
