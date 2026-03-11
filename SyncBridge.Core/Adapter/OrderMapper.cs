using SyncBridge.Core.Dolibarr;
using SyncBridge.Core.Smartstore;
using System.Linq;

namespace SyncBridge.Core.Adapter
{
    public class OrderMapper
    {
        public DolibarrOrder MapToDolibarr(ShopOrder shopOrder)
        {
            return new DolibarrOrder
            {
                ExternalId = shopOrder.Id.ToString(),
                OrderDate = shopOrder.CreatedOnUtc,
                Total = shopOrder.OrderTotal,

                // Items existieren noch → bleiben einfach so
                Items = shopOrder.Items
            };
        }
    }
}
