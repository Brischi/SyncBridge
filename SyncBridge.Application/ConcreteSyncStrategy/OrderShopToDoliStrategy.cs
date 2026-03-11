using SyncBridge.Core;
using SyncBridge.Core.Adapter;
using SyncBridge.Core.Dolibarr;
using SyncBridge.Core.Smartstore;
using System.Threading.Tasks;

namespace SyncBridge.ApplicationLayer.ConcreteSyncStrategy
{
    internal class OrderShopToDoliStrategy : ISyncStrategy
    {
        private readonly IDolibarrClient _dolibarr;
        private readonly ISmartstoreClient _smartstore;
        private readonly ILogger _logger;
        private readonly OrderMapper _mapper = new();

        public OrderShopToDoliStrategy(
            IDolibarrClient dolibarr,
            ISmartstoreClient smartstore,
            ILogger logger)
        {
            _dolibarr = dolibarr;
            _smartstore = smartstore;
            _logger = logger;
        }

        public async Task<SyncResult> SyncAsync()
        {
            var result = new SyncResult();

            var shopOrders = await _smartstore.GetOrdersAsync();

            foreach (var shopOrder in shopOrders)
            {
                // Idempotenz
                if (await _dolibarr.OrderExistsAsync(shopOrder.Id.ToString()))
                {
                    continue;
                }

                var doliOrder = _mapper.MapToDolibarr(shopOrder);

                await _dolibarr.CreateOrderAsync(doliOrder);

                result.Created++;
            }

            return result;
        }

        public Task CleanTargetAsync()
        {
            // bewusst leer – Orders löscht man nicht
            return Task.CompletedTask;
        }
    }
}
