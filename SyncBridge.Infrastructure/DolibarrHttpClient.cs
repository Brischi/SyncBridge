using SyncBridge.Core.Dolibarr;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Globalization;

namespace SyncBridge.Infrastructure;

public class DolibarrHttpClient : IDolibarrClient
{
    private readonly HttpClient _httpClient;

    public DolibarrHttpClient(DolibarrSettings settings)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(settings.BaseUrl)   // z.B. "http://localhost:8080"
        };

        _httpClient.DefaultRequestHeaders.Add("DOLAPIKEY", settings.ApiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }
   
    private async Task<string> GetProductsRawAsync()
    {
    var response = await _httpClient.GetAsync("/api/index.php/products");
    response.EnsureSuccessStatusCode();
    var json = await response.Content.ReadAsStringAsync();

    return json;
    }

    public async Task<IReadOnlyList<DolibarrProduct>> GetAllProductsAsync()
    {
        var json = await GetProductsRawAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var products = JsonSerializer.Deserialize<List<DolibarrProduct>>(json, options) ?? new List<DolibarrProduct>();

        return products;
    }

    public async Task<string?> CreateOrUpdateProductAsync(DolibarrProduct product)
    {
        if (string.IsNullOrWhiteSpace(product.Ref))
            throw new ArgumentException("Dolibarr product Ref must not be empty.", nameof(product));
        if (string.IsNullOrWhiteSpace(product.Label))
            throw new ArgumentException("Dolibarr product Label must not be empty.", nameof(product));

        var priceString = string.IsNullOrWhiteSpace(product.Price) ? "0" : product.Price.Trim().Replace(",", ".");
        var price = decimal.TryParse(priceString, NumberStyles.Any, CultureInfo.InvariantCulture, out var p)
            ? p
            : 0m;

        var payload = new
        {
            @ref = product.Ref,
            label = product.Label,
            price = price,
            status = product.Status
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        var isCreate = string.IsNullOrEmpty(product.Id);
        if (isCreate)
        {
            // CREATE (POST)
            response = await _httpClient.PostAsync("api/index.php/products", content);
        }
        else
        {
            // UPDATE (PUT)
            var url = $"api/index.php/products/{product.Id}";
            response = await _httpClient.PutAsync(url, content);
        }
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Fehler bei Produkt Ref='{product.Ref}', Label='{product.Label}': " +
                $"Dolibarr product sync failed ({(int)response.StatusCode} {response.ReasonPhrase}): {errorBody}");
        }
        // Bei UPDATE einfach die vorhandene Id zurückgeben
        if (!isCreate)
            return product.Id;
        // Bei CREATE: Body lesen und Id herausziehen
        var responseBody = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;
        // Dolibarr gibt in vielen Versionen direkt die numerische ID oder das Objekt zurück
        // darum erst reines int versicjem
        if (int.TryParse(responseBody, out var numericId))
            return numericId.ToString();
        // Alternativ: JSON-Objekt mit "id"
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("id", out var idProp))
            {
                var idString = idProp.GetRawText().Trim('"');
                return idString;
            }
        }
        catch
        {
            // Ignorieren, wenn kein gültiges JSON
        }

        return null;
    }
    

    public async Task<IReadOnlyList<DolibarrOrder>> GetOrdersAsync()
    {
        // limit hochziehen, damit du überhaupt was siehst
        var response = await _httpClient.GetAsync("/api/index.php/orders?limit=100");

        if ((int)response.StatusCode == 404)
            return new List<DolibarrOrder>();

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Achtung: Dolibarr-Order JSON-Felder heißen oft anders.
        // Damit es trotzdem "durchbaut", deserialisieren wir erst einmal flexibel.
        // Wenn deine DolibarrOrder Properties nicht matchen, bleiben sie leer -> dann unten im Create/Exists sauber machen.
        var list = JsonSerializer.Deserialize<List<DolibarrOrder>>(json, options) ?? new List<DolibarrOrder>();
        return list;
    }
    public async Task CreateOrderAsync(DolibarrOrder order)
    {
        if (string.IsNullOrWhiteSpace(order.ExternalId))
            throw new ArgumentException("ExternalId must not be empty.", nameof(order));

        // Minimal-Payload, damit du erstmal anlegen kannst.
        // WICHTIG: socid (ThirdParty) ist je nach Dolibarr zwingend.
        // Fürs Durchbauen erstmal fix auf 1 (oder nimm eine Konstante aus Settings).
        var payload = new Dictionary<string, object?>
        {
            // Kunde/ThirdParty (Pflicht in vielen Setups)
            ["socid"] = 1,

            // Externes Matching (analog zu deinem Customer ref_ext)
            ["ref_ext"] = order.ExternalId,

            // Datum: Dolibarr akzeptiert oft Unix-Timestamp oder YYYY-MM-DD.
            // Wir nehmen YYYY-MM-DD als string.
            ["date"] = order.OrderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),

            // Optional: öffentliche Notiz
            ["note_public"] = $"Imported from Shop (ExternalId={order.ExternalId})"
        };

        var json = JsonSerializer.Serialize(payload);


        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/api/index.php/orders", content);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Create order failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}");
        }
    }
    public async Task<bool> OrderExistsAsync(string externalId)
    {
        // Versuch 1: Suche über ref_ext (wenn Dolibarr-API Filter unterstützt)
        // Manche Dolibarr-Setups unterstützen sqlfilters oder sortfield etc.
        // Zum "durchbauen" machen wir es robust: Orders holen und lokal prüfen.

        var orders = await GetOrdersAsync();
        return orders.Any(o => o.ExternalId == externalId);
    }
    public async Task DeleteProductAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/index.php/products/{id}");

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Delete product failed (Id={id}): {(int)response.StatusCode} {errorBody}");
        }
    }

    public async Task UpdateProductStockAsync(string productId, int quantity)
    {
        var payload = new Dictionary<string, object?>
        {
            ["product_id"] = int.Parse(productId),   // ← WICHTIG: product_id statt fk_product
            ["warehouse_id"] = 1,                   // oder die richtige Lager-ID aus Dolibarr
            ["qty"] = quantity,                     // Menge, die bewegt werden soll
            ["movementlabel"] = "SyncBridge stock sync"
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("api/index.php/stockmovements", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Stock update failed (Id={productId}, Qty={quantity}): {errorBody}");
        }
    }


    public async Task DeleteAllProductsAsync()
    {
        var products = await GetAllProductsAsync();
        var productsWithId = products.Where(p => !string.IsNullOrWhiteSpace(p.Id)).ToList();


        foreach (var product in productsWithId)
        {
            try
            {
                await DeleteProductAsync(product.Id);

            }
            catch (Exception ex)
            {
 
            }
        }
    }
    public async Task<int> GetProductStockAsync(string productId)
    {
        // Einzelnes Produkt-Stock-Endpoint aufrufen
        var response = await _httpClient.GetAsync($"api/index.php/products/{productId}/stock");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();


        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Feldnamen im Explorer bei GET /products/{id}/stock prüfen.
        // Häufig ist es "stock_reel" oder ähnlich.[web:58][web:72]


        if (root.ValueKind == JsonValueKind.Object &&
      root.TryGetProperty("stock_reel", out var stockProp) &&
      stockProp.TryGetInt32(out var current))
        {
            return current;
        }

        // Fallback für andere Dolibarr-Versionen
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("stock", out var stockProp2) &&
            stockProp2.TryGetInt32(out var current2))
        {
            return current2;
        }

        return 0;
    }
    public async Task<IReadOnlyList<DolibarrCustomer>> GetAllCustomersAsync()
    {
        // mode=1 => nur customers
        var response = await _httpClient.GetAsync("/api/index.php/thirdparties?mode=1&limit=100");

        if ((int)response.StatusCode == 404)
        {
            // Dolibarr liefert 404 statt [] wenn keine vorhanden
            return new List<DolibarrCustomer>();
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Achtung: Dolibarr liefert Thirdparty-Objekte, Property-Namen können abweichen.
        // Wenn Deserialisieren scheitert: dann müssen wir DolibarrCustomer an die echten JSON-Felder anpassen.
        var list = JsonSerializer.Deserialize<List<DolibarrCustomer>>(json, options) ?? new List<DolibarrCustomer>();
        return list;
    }
    public async Task CreateOrUpdateCustomerAsync(DolibarrCustomer customer)
    {
        if (string.IsNullOrWhiteSpace(customer.Name))
            throw new ArgumentException("Dolibarr customer Name must not be empty.", nameof(customer));

        // Payload: Dolibarr Thirdparty-Felder
        var payload = new Dictionary<string, object?>
        {
            ["name"] = customer.Name,
            ["email"] = customer.Email,
            ["phone"] = customer.Phone,
            ["address"] = customer.Address,
            ["zip"] = customer.Zip,
            ["town"] = customer.Town,

            // Kunde markieren (Thirdparty is customer)
            ["client"] = 1,


        };

        // Land ist je nach Dolibarr-Version eher "country_code" oder "country_id".
        // Wenn es bei dir nicht geht, erstmal weglassen.
        if (!string.IsNullOrWhiteSpace(customer.CountryCode))
            payload["country_code"] = customer.CountryCode;

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;

        if (string.IsNullOrWhiteSpace(customer.Id))
        {
            // CREATE
            response = await _httpClient.PostAsync("/api/index.php/thirdparties", content);
        }
        else
        {
            // UPDATE
            response = await _httpClient.PutAsync($"/api/index.php/thirdparties/{customer.Id}", content);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Customer sync failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}");
        }
    }

}
