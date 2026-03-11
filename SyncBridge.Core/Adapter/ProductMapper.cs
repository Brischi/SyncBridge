using SyncBridge.Core.Dolibarr;
using SyncBridge.Core.Smartstore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SyncBridge.Core.Adapter
{
    public static class ProductMapper
    {
        public static ShopProduct MapToSmartstore(DolibarrProduct source)
        {
            
            int stock = 0;
            int.TryParse(source.Stock, out stock); // Weil von Dolibar als String geliefert

            // Published aus Dolibarr-Status ableiten ("1" = aktiv)
            bool published = source.Status == "1";

            // Steuer: Wenn TvaTx == "0" → steuerbefreit
            bool isTaxExempt = source.TvaTx == "0";

            return new ShopProduct // Dolibarprodukt in Shopprodukt umwandeln
            {
                Sku = source.Ref ?? "",
                Name = source.Label ?? source.Ref ?? "",
                Price = ParseDecimal(source.Price) ?? 0,
                Published = published,
                StockQuantity = stock,
                IsTaxExempt = isTaxExempt,
            };
        }

        public static DolibarrProduct MapToDolibarr(ShopProduct source)
        {
            return new DolibarrProduct
            {
                Ref = source.Sku,
                Label = source.Name,
                Price = source.Price.ToString(CultureInfo.InvariantCulture),
                Status = source.Published ? "1" : "0",
                Stock = source.StockQuantity.ToString()
            };

        }

        // Versuch, einen Text in eine Dezimalzahl umzuwandeln. Wenn das klappt, Zahl zurückgeben. Wenn nicht, null zurück geben ohne ohne Fehler zu werfen
        private static decimal? ParseDecimal(string value)
        { //numberstyles.any erlaubt alle gängigen Zahlenformate, CultureInfo.InvariantCulture sorgt für Punkt als Dezimaltrennzeichen
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
                ? result
                : null;
        }
    }
}
