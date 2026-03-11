using SyncBridge.Core;
using SyncBridge.Core.Dolibarr;
using SyncBridge.Core.Smartstore;
using System.Threading.Tasks;

namespace SyncBridge.ApplicationLayer.ConcreteSyncStrategy
{
    internal class OrderDoliToShopStrategy : ISyncStrategy
    {
        private readonly IDolibarrClient _dolibarr;
        private readonly ISmartstoreClient _smartstore;
        private readonly ILogger _logger;
        public OrderDoliToShopStrategy(IDolibarrClient dolibarr, ISmartstoreClient smartstore, ILogger logger)
        {
            _dolibarr = dolibarr;
            _smartstore = smartstore;
            _logger = logger;
        }

        public Task<SyncResult> SyncAsync()
        {
            // TODO: echte Logik implementieren
            return Task.FromResult(new SyncResult());
        }

        public Task CleanTargetAsync()
        {
            // TODO: echte Logik implementieren
            return Task.CompletedTask;
        }
    }
}
