using SyncBridge.ApplicationLayer;
using SyncBridge.Core;
using SyncBridge.Core.Adapter;
using SyncBridge.Core.Dolibarr;
using SyncBridge.Core.Smartstore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SyncBridge.ApplicationLayer.ConcreteSyncStrategy
{
    public class ProductDoliToShopStrategy : ISyncStrategy
    {
        // HTTP-Client für Dolibarr (Quelle)
        private readonly IDolibarrClient _doliClient;
        // HTTP-Client für den Shop (Ziel)
        private readonly ISmartstoreClient _shopClient;
        // Abstraktes Logging-Interface für UI/Datei/Console
        private readonly ILogger _logger;

        // Konstruktor: Abhängigkeiten werden über Dependency Injection hereingegeben
        public ProductDoliToShopStrategy(IDolibarrClient doli, ISmartstoreClient shop, ILogger logger)
        {
            _doliClient = doli;
            _shopClient = shop;
            _logger = logger;
        }

        // Kernmethode: Führt den vollständigen Delta-Sync Dolibarr -> Shop aus
        public async Task<SyncResult> SyncAsync()
        {
            _logger.Info("SYNC START");

            // 1. SHOP-Produkte laden
            var shopProducts = await _shopClient.GetAllProductsAsync();     // alle Produkte aus Shop laden, damit ich mit Doli-Produkten vergleichen kann
            _logger.Info($"SHOP: {shopProducts.Count} Produkte aus Smartstore geladen");

            // Vollständige Produktinfos pro SKU. Es wird ein Dictionary erstellt. Key = SKU, Value = ShopProduct
            var shopSkuToProduct = shopProducts
                .Where(p => !string.IsNullOrWhiteSpace(p.Sku))
                .GroupBy(p => IdNormalizer.Normalize(p.Sku))// Produkte nach SKU gruppieren, falls es mehrere Einträge mit gleicher SKU gubt (alter Versionen, ...) und normaliziert (SKU vereinheitlichen (Leerzeichen und Unterstriche entfernen. Da beim Anlegen eines Produkts vom Smartstore in Dolibarr Dolibarr ein Leerzeichen in der SKU automatisch durch einen Unterstich ersetzen würde)
                .ToDictionary(g => g.Key,  // geladene Produkte in ein Dictionary packen und sortieren nach SKU, falls es Dublikate gibt, wird das erste Produkt genommen
                  g => g.FirstOrDefault(p => p.Published) ?? g.First());// geladene Produkte in ein Dictionary packen und sortieren nach SKU, falls es Dublikate gibt, wird das erste veröffentlichte Produkt genommen, falls keins veröffentlicht ist, wird das erste Produkt. Dictionary kann also keine doppenten Keys haben

            // Reine Existenzprüfung der SKUs. SKUs werden in HashSet gepackt für schnelle Suche
            var shopSkus = shopSkuToProduct.Keys.ToHashSet();

            // primitives Dictionary mit SKU und ID
            var shopSkuToId = shopSkuToProduct
                .ToDictionary(p => p.Key, p => p.Value.Id?.ToString());

            _logger.Info($"SHOP: {shopSkus.Count} eindeutige SKUs nach Gruppierung (Dublikate entfernt)");
 
            // 2. DOLIBARR-Produkte laden
            var doliProducts = await _doliClient.GetAllProductsAsync(); // alls Produkte aus Dolibarr laden
            _logger.Info($"DOLI: {doliProducts.Count} Produkte aus Dolibarr geladen");

            // Reine Existenzprüfung der SKUs. SKUs werden in HashSet gepackt für schnelle Suche
            var doliSkus = doliProducts
                .Where(p => !string.IsNullOrWhiteSpace(p.Ref)) // Produkte ohne REF/SKU ignorieren
                .Select(p => IdNormalizer.Normalize(p.Ref)) // SKU vereinheitlichen (Leerzeichen und Unterstriche entfernen. Da beim Anlegen eines Produkts vom Smartstore in Dolibarr Dolibarr ein Leerzeichen in der SKU automatisch durch einen Unterstich ersetzen würde
                .ToHashSet();
            _logger.Info($"DOLI: {doliSkus.Count} mit gültiger SKU");

            // 3. DEACTIVATE: Shop-Produkte die in Dolibarr nicht mehr existieren auf nicht veröffentlicht setzen (Löschen dieser Produkte über Delete wäre zu radikal, da eventuell andere Abhängigkeiten existieren und das die referentielle Integrität verletzen könnte)
            var deactivated = 0;
            var created = 0;
            var updated = 0;
            foreach (var normSku in shopSkus)
            {
                if (!doliSkus.Contains(normSku))
                {
                    // stellt sicher, dass es zu der SKU ein gültiges Shop-Produkt gibt, und holt anschließend optional die Shop-ID, ohne bei fehlenden Einträgen einen Fehler zu verursachen.
                    if (!shopSkuToProduct.TryGetValue(normSku, out var shopProduct) || shopProduct == null)
                    {
                        _logger.Warn($"SKU {normSku}: ShopProduct nicht gefunden – Deaktivierung übersprungen.");
                        continue;
                    }
                    shopSkuToId.TryGetValue(normSku, out var shopIdStr); // shopIdStr kann null sein, wird danach geprüft
                    // Nur deaktivieren, wenn noch veröffentlicht
                    if (shopProduct.Published)
                    {
                        if (string.IsNullOrWhiteSpace(shopIdStr))
                        {
                            _logger.Warn($"{shopProduct.Sku} ist in Dolibarr nicht mehr vorhanden, SKU existiert noch in Smartstore, aber keine Shop-ID vorhanden");
                            continue;
                        }
                        shopProduct.Published = false;
                        await _shopClient.UpdateProductAsync(shopIdStr, shopProduct);
                        _logger.Info($"Produkt {shopProduct.Sku} (ID={shopIdStr}) existiert in Dolibarr nicht mehr und wurde deaktiviert (Published=false).");
                        deactivated++;
                    }
                    else
                    {
                        _logger.Info($"Produkt {shopProduct.Sku} (ID={shopIdStr ?? "?"}) existiert in Dolibarr nicht mehr und ist bereits deaktiviert (Published=false).");
                    }
                }
            }

            // 4. CREATE/UPDATE
            // Alle Dolibarr-Produkte durchlaufen, die eine gültige Referenz (SKU) haben
            foreach (var doliProduct in doliProducts.Where(p => !string.IsNullOrWhiteSpace(p.Ref)))
            {
                var normalizedSku = IdNormalizer.Normalize(doliProduct.Ref);           
                // Dolibarr-Produkt in Shop-Produkt umwandeln mit dem ProductMapper
                var shopProduct = ProductMapper.MapToSmartstore(doliProduct);
                shopProduct.Name = ExtractCleanName(doliProduct.Label);

                if (!string.IsNullOrWhiteSpace(doliProduct.Id))
                {
                    var doliStock = await _doliClient.GetProductStockAsync(doliProduct.Id);
                    shopProduct.StockQuantity = doliStock;   // überschreibt den evtl. leeren Mapper-Wert
                }

                if (shopSkuToId.TryGetValue(normalizedSku, out var shopIdStr)
                    && !string.IsNullOrWhiteSpace(shopIdStr))
                {
                    var existing = shopSkuToProduct[normalizedSku];

                    // 1) Feld-Änderungen wie bei Shop->Doli (HasChanges ohne Stock)
                    var needsFieldUpdate = HasChanges(existing, shopProduct);
                    var wasUpdated = false;

                    // 2) Stock separat: Delta Doli -> Shop
                    var existingStock = existing.StockQuantity;      
                    var targetStock = shopProduct.StockQuantity;   
                    var stockDelta = targetStock - existingStock;

                    // 3) Stock-Sync
                    if (stockDelta != 0)
                    {
                        existing.StockQuantity = targetStock;
                        await _shopClient.UpdateProductAsync(shopIdStr, existing);
                        _logger.Info(
                            $"STOCK MOVE (DOLI->SHOP): Sku={existing.Sku} Id={shopIdStr} " +
                            $"Delta={stockDelta} (Doli={targetStock}, ShopAlt={existingStock})");
                        wasUpdated = true;
                    }

                    // 4) Feld-Update
                    if (needsFieldUpdate)
                    {
                        existing.Name = shopProduct.Name;
                        existing.Price = shopProduct.Price;
                        existing.Published = shopProduct.Published;
                        existing.IsTaxExempt = shopProduct.IsTaxExempt;

                        await _shopClient.UpdateProductAsync(shopIdStr, existing);
                        _logger.Info($"UPDATE (FIELDS): {existing.Sku} (ID={shopIdStr})");
                        wasUpdated = true;
                    }

                    if (!needsFieldUpdate && stockDelta == 0)
                    {
                        _logger.Info($"SKIP: {existing.Sku} (keine Änderungen)");
                    }
                    if (wasUpdated)
                    {
                        updated++;
                    }
                }
                else
                {
                    // CREATE-Zweig: Produkt existiert im Shop noch nicht
                    await _shopClient.CreateProductAsync(shopProduct);
                    _logger.Info($"CREATED: {shopProduct.Sku}");
                    created++;
                }
            }

            _logger.Info($"Sync: Deactivated Products: {deactivated}, New Products: {created}, Updated Products: {updated} = {deactivated + created + updated} total");

            if (deactivated == 0 && created == 0 && updated == 0)
                _logger.Info("Alles up to date - nichts zu syncen!");

            return new SyncResult
            {
                Deactivated = deactivated,
                Created = created,
                Updated = updated
            };
        }

        // Vergleichsmethode für UPDATE: Prüft, ob sich relevante Felder zwischen zwei Shop-Produkten unterscheiden. Bereinigt einen Roh-Produktnamen aus Dolibarr für die Anzeige im Shop. Gibt bei leerem/ungültigem Namen einen leeren String zurück. Wenn eckige Klammern vorhanden sind, wird nur der Inhalt innerhalb der Klammern verwendet.(interne Dolibarr-Präfixe werden entfernt). Andernfalls wird der Name auf maximal 50 Zeichen gekürzt
            private static bool HasChanges(ShopProduct existing, ShopProduct incoming)
        {
            string Norm(string s) => (s ?? "").Trim();

            return Norm(existing.Name) != Norm(incoming.Name)
                || existing.Price != incoming.Price
                || existing.Published != incoming.Published;
                //|| existing.IsTaxExempt != incoming.IsTaxExempt;
        }
        private static string ExtractCleanName(string dirtyName)
        {
            if (string.IsNullOrWhiteSpace(dirtyName))
                return "";

            int open = dirtyName.IndexOf('[');
            int close = dirtyName.IndexOf(']', open + 1);

            // Wenn "[...]" vorhanden ist: nur Inhalt zwischen den Klammern nehmen
            if (open >= 0 && close > open)
                return dirtyName.Substring(open + 1, close - open - 1).Trim();

            return dirtyName.Length > 50 ? dirtyName.Substring(0, 50) : dirtyName;
        }

        // ACHTUNG: Diese Löschlogik wurde nur in der Entwicklungs-/Testphase verwendet, um Testdaten schnell zu bereinigen. Im Produktivbetrieb werden Daten deaktiviert nicht gelöscht, um referentielle Integrität zu gewährleisten
        public async Task CleanTargetAsync()
        {
            await _shopClient.DeleteAllProductsAsync();
            _logger.Info("Shop komplett geleert!");
        }
    }
}
