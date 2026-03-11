using SyncBridge.Core;
using SyncBridge.Core.Adapter;
using SyncBridge.Core.Dolibarr;
using SyncBridge.Core.Smartstore;
using System.Linq;
using System.Threading.Tasks;

namespace SyncBridge.ApplicationLayer.ConcreteSyncStrategy
{
    public class ProductShopToDoliStrategy : ISyncStrategy
    {
        private readonly IDolibarrClient _dolibarrClient;
        private readonly ISmartstoreClient _shopClient;
        private readonly ILogger _logger;

        public ProductShopToDoliStrategy(
            IDolibarrClient dolibarrClient,
            ISmartstoreClient shopClient,
            ILogger logger)
        {
            _dolibarrClient = dolibarrClient;
            _shopClient = shopClient;
            _logger = logger;
        }

        public async Task<SyncResult> SyncAsync()
        {
            _logger.Info("SYNC START (SHOP -> DOLIBARR PRODUCTS)");

            // 1) Dolibarr-Produkte laden
            var doliProducts = await _dolibarrClient.GetAllProductsAsync();
            _logger.Info($"DOLI: {doliProducts.Count} Produkte geladen");

            var doliRefToProduct = doliProducts
                .Where(p => !string.IsNullOrWhiteSpace(p.Ref))
                .GroupBy(p => IdNormalizer.Normalize(p.Ref))
                .ToDictionary(g => g.Key, g => g.First());

            var doliRefs = doliRefToProduct.Keys.ToHashSet();

            // 2) Shop-Produkte laden
            var shopProducts = await _shopClient.GetAllProductsAsync();
            _logger.Info($"SHOP: {shopProducts.Count} Produkte geladen");

            var shopSkuToProduct = shopProducts
                .Where(p => !string.IsNullOrWhiteSpace(p.Sku))
                .GroupBy(p => IdNormalizer.Normalize(p.Sku))
                .ToDictionary(g => g.Key, g => g.FirstOrDefault(p => p.Published) ?? g.First());

            var shopSkus = shopSkuToProduct.Keys.ToHashSet();
            _logger.Info($"SHOP: {shopSkus.Count} eindeutige SKUs nach Gruppierung");

            var deactivated = 0;
            var created = 0;
            var updated = 0;

            // 3) Deactivate in Dolibarr, wenn nicht mehr im Shop vorhanden
            foreach (var normRef in doliRefs)
            {
                if (shopSkus.Contains(normRef))
                    continue;

                if (!doliRefToProduct.TryGetValue(normRef, out var doliProduct) || doliProduct == null)
                    continue;

                if (doliProduct.Status == "0")
                    continue;

                doliProduct.Status = "0";
                await _dolibarrClient.CreateOrUpdateProductAsync(doliProduct);

                _logger.Info($"DEACTIVATED: Ref={doliProduct.Ref} (ID={doliProduct.Id})");
                deactivated++;
            }

            // 4) Create/Update Shop -> Dolibarr (Delta)
            foreach (var kvp in shopSkuToProduct)
            {
                var normSku = kvp.Key;
                var shopProduct = kvp.Value;
                if (shopProduct == null)
                    continue;

                var mapped = ProductMapper.MapToDolibarr(shopProduct);

                if (string.IsNullOrWhiteSpace(mapped.Ref))
                {
                    _logger.Warn($"SHOP: Produkt ohne Ref/SKU (Sku='{shopProduct.Sku ?? ""}') – übersprungen");
                    continue;
                }
                if (doliRefToProduct.TryGetValue(normSku, out var existing) && existing != null)
                {
                    mapped.Id = existing.Id;
                    bool hasStockMoved = false;
                    var needsUpdate = HasChanges(existing, mapped);

                    int existingStock = 0;
                    if (!string.IsNullOrWhiteSpace(mapped.Id))
                    {
                        existingStock = await _dolibarrClient.GetProductStockAsync(mapped.Id);
                    }

                    var delta = shopProduct.StockQuantity - existingStock;

                    if (!string.IsNullOrWhiteSpace(mapped.Id) && delta != 0)
                    {
                        await _dolibarrClient.UpdateProductStockAsync(mapped.Id, delta);
                        _logger.Info($"STOCK MOVE: Ref={mapped.Ref} Id={mapped.Id} Delta={delta} (Shop={shopProduct.StockQuantity}, Doli={existingStock})");
                        hasStockMoved = true;
                    }
                    else
                    {
                        _logger.Info($"STOCK: Ref={mapped.Ref} Id={mapped.Id} keine Änderung (Shop={shopProduct.StockQuantity}, Doli={existingStock})");
                    }

                    if (needsUpdate)
                    {
                        var idAfterUpdate = await _dolibarrClient.CreateOrUpdateProductAsync(mapped);
                        if (!string.IsNullOrWhiteSpace(idAfterUpdate))
                            mapped.Id = idAfterUpdate;

                        _logger.Info($"UPDATE: Ref={mapped.Ref} Id={mapped.Id}");
             
                    }
                    if (hasStockMoved || needsUpdate)
                    {
                        updated++;
                    }
                }


                else
                {
                    // Create-Fall
                    mapped.Id = string.Empty;

                    var newId = await _dolibarrClient.CreateOrUpdateProductAsync(mapped);
                    if (!string.IsNullOrWhiteSpace(newId))
                    {
                        mapped.Id = newId;
                        created++;
                        _logger.Info($"CREATE: Ref={mapped.Ref} Id={mapped.Id}");

                        if (shopProduct.StockQuantity != 0)
                        {
                            await _dolibarrClient.UpdateProductStockAsync(mapped.Id, shopProduct.StockQuantity);
                            _logger.Info($"STOCK_AFTER_CREATE: Ref={mapped.Ref} Id={mapped.Id} Qty={shopProduct.StockQuantity}");
                        }

                    }
                    else
                    {
                        created++;
                        _logger.Warn($"CREATE: Ref={mapped.Ref} - neue Dolibarr-ID konnte nicht ermittelt werden.");
                    }
                }
            }

            _logger.Info($"Sync: Deactivated={deactivated}, Created={created}, Updated={updated} (Total={deactivated + created + updated})");

            var result = new SyncResult
            {
                Deactivated = deactivated,
                Created = created,
                Updated = updated
            };

            if (result.IsUpToDate)
                _logger.Info("Alles up to date - nichts zu syncen!");

            _logger.Info("SYNC END (SHOP -> DOLIBARR PRODUCTS)");
            return result;
        }

        private static bool HasChanges(DolibarrProduct existing, DolibarrProduct incoming)
        {
            if (existing == null || incoming == null)
                return true;

            string Norm(string? s) => (s ?? "").Trim();

            decimal? ToDecimal(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return null;

                var t = s.Trim().Replace(",", ".");
                return decimal.TryParse(
                    t,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var d)
                    ? d
                    : null;
            }

            var exRef = Norm(existing.Ref);
            var inRef = Norm(incoming.Ref);

            var exLabel = Norm(existing.Label);
            var inLabel = Norm(incoming.Label);

            var exStatus = Norm(existing.Status);
            var inStatus = Norm(incoming.Status);

            var exPrice = ToDecimal(existing.Price);
            var inPrice = ToDecimal(incoming.Price);

            return exRef != inRef
                || exLabel != inLabel
                || exStatus != inStatus
                || exPrice != inPrice;
        }

        public async Task CleanTargetAsync()
        {
            _logger.Warn("Alle Produkte in Dolibarr werden gelöscht");
            await _dolibarrClient.DeleteAllProductsAsync();
        }
    }
}
