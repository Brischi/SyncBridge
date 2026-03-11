using SyncBridge.ApplicationLayer;
using SyncBridge.Core;
using SyncBridge.Core.Dolibarr;
using SyncBridge.Core.Smartstore;
using SyncBridge.ApplicationLayer.ConcreteSyncStrategy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncBridge.ApplicationLayer
{

    public class SyncStrategyFactory : ISyncStrategyFactory
    {
        private readonly IDolibarrClient _dolibarr;
        private readonly ISmartstoreClient _smartstore;
        private readonly ILogger _logger;

        public SyncStrategyFactory(IDolibarrClient dolibarr, ISmartstoreClient smartstore, ILogger logger)
        {
            _dolibarr = dolibarr;
            _smartstore = smartstore;
            _logger = logger;
        }

        public ISyncStrategy Create(SyncCategory category, SyncDirection direction)
        {
            return (category, direction) switch
            {
                // PRODUCTS
                (SyncCategory.Products, SyncDirection.ShopToDolibarr) =>
                    new ProductShopToDoliStrategy(_dolibarr, _smartstore, _logger),
                (SyncCategory.Products, SyncDirection.DolibarrToShop) =>
                    new ProductDoliToShopStrategy(_dolibarr, _smartstore, _logger),

                // CUSTOMER
                (SyncCategory.Customers, SyncDirection.ShopToDolibarr) =>
                    new CustomerShopToDoliStrategy(_dolibarr, _smartstore, _logger),
                (SyncCategory.Customers, SyncDirection.DolibarrToShop) =>
                    new CustomerDoliToShopStrategy(_dolibarr, _smartstore, _logger),

                // ORDERS
                (SyncCategory.Orders, SyncDirection.ShopToDolibarr) =>
                    new OrderShopToDoliStrategy(_dolibarr, _smartstore, _logger),
                (SyncCategory.Orders, SyncDirection.DolibarrToShop) =>
                    new OrderDoliToShopStrategy(_dolibarr, _smartstore, _logger),

                _ => throw new NotSupportedException(
                    $"Keine Strategy für {category} mit Richtung {direction} registriert.")
            };
        }
    }

}






