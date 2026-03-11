using SyncBridge.Core;
using SyncBridge.Core.Smartstore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SyncBridge.Infrastructure
{
    public class SmartstoreHttpClient : ISmartstoreClient
    {
        private readonly HttpClient _http;

        public SmartstoreHttpClient(string baseUrl, string publicKey, string secretKey)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    if (message.RequestUri.Host == "localhost")
                        return true;
                    return errors == System.Net.Security.SslPolicyErrors.None;
                }
            };

            _http = new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl)
            };

            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{publicKey}:{secretKey}"));

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);

            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // ----------------------------
        // PRODUCTS 
        // ----------------------------

        public async Task<IReadOnlyList<ShopProduct>> GetAllProductsAsync()
        {
            var response = await _http.GetAsync("odata/v1/Products");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var products = new List<ShopProduct>();

            if (root.TryGetProperty("value", out var valueElement) &&
                valueElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in valueElement.EnumerateArray())
                {
                    int? id = null;
                    if (item.TryGetProperty("Id", out var idProp) &&
                        idProp.ValueKind == JsonValueKind.Number)
                        id = idProp.GetInt32();

                    var sku = item.TryGetProperty("Sku", out var skuProp) ? skuProp.GetString() ?? "" : "";
                    var name = item.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() ?? "" : "";

                    decimal price = 0;
                    if (item.TryGetProperty("Price", out var priceProp) &&
                        priceProp.ValueKind == JsonValueKind.Number &&
                        priceProp.TryGetDecimal(out var priceVal))
                        price = priceVal;

                    bool published = true;
                    if (item.TryGetProperty("Published", out var publishedProp))
                        published = publishedProp.GetBoolean();

                    int stockQuantity = 0;
                    if (item.TryGetProperty("StockQuantity", out var stockProp) &&
                        stockProp.ValueKind == JsonValueKind.Number)
                        stockQuantity = stockProp.GetInt32();

                    products.Add(new ShopProduct
                    {
                        Id = id,
                        Sku = sku,
                        Name = name,
                        Price = price,
                        Published = published,
                        StockQuantity = stockQuantity
                    });
                }
            }

            return products;
        }

        public async Task CreateProductAsync(ShopProduct product)
        {
            var payload = new
            {
                Sku = product.Sku,
                Name = product.Name,
                Price = product.Price,
                Published = product.Published,
                StockQuantity = product.StockQuantity,
                TaxCategoryId = product.TaxCategoryId,
                IsTaxExempt = product.IsTaxExempt
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await _http.PostAsync("odata/v1/Products", content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Shop error {(int)response.StatusCode}: {body}");
        }

        public async Task UpdateProductAsync(string id, ShopProduct product)
        {
            var payload = new
            {
                Sku = product.Sku,
                Name = product.Name,
                Price = product.Price,
                Published = product.Published,
                StockQuantity = product.StockQuantity
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Patch, $"odata/v1/Products({id})")
            {
                Content = content
            };

            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Shop error {(int)response.StatusCode}: {body}");
        }

        public async Task DeleteProductAsync(string sku)
        {
            var id = await FindProductIdBySkuAsync(sku);
            if (id != null)
            {
                var response = await _http.DeleteAsync($"odata/v1/Products({id})");
                response.EnsureSuccessStatusCode();
            }
        }

        public async Task DeleteAllProductsAsync()
        {
            var products = await GetAllProductsAsync();
            foreach (var product in products.Where(p => p.Id.HasValue))
            {
                await _http.DeleteAsync($"odata/v1/Products({product.Id.Value})");
            }
        }

        public async Task CreateOrUpdateProductAsync(ShopProduct product)
        {
            var id = await FindProductIdBySkuAsync(product.Sku);

            if (id != null)
                await UpdateProductAsync(id, product);
            else
                await CreateProductAsync(product);
        }

        public async Task<string> FindProductIdBySkuAsync(string sku)
        {
            var products = await GetAllProductsAsync();
            var found = products.FirstOrDefault(p => p.Sku == sku);
            return found?.Id?.ToString();
        }

        // ----------------------------
        // ORDERS
        // ----------------------------

        public async Task<IReadOnlyList<ShopOrder>> GetOrdersAsync(DateTime? since = null)
        {
            var url = "/odata/v1/orders?$top=100&$orderby=CreatedOnUtc desc";


            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Smartstore GetOrders failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}");
            }

            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("value", out var value))
                return new List<ShopOrder>();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var orders = JsonSerializer.Deserialize<List<ShopOrder>>(
                value.GetRawText(), options) ?? new List<ShopOrder>();

            if (since.HasValue)
            {
                var sinceUtc = since.Value.ToUniversalTime();
                orders = orders.Where(o => o.CreatedOnUtc >= sinceUtc).ToList();
            }

            return orders;
        }

        public async Task<IReadOnlyList<ShopCustomer>> GetAllCustomersAsync()
        {
            // Hinweis: Ohne $expand bekommst du BillingAddress/ShippingAddress oft nicht mit.
            // Du kannst später z.B. $expand=BillingAddress($expand=Country,StateProvince) ergänzen.
            var response = await _http.GetAsync("odata/v1/Customers");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("value", out var value) ||
                value.ValueKind != JsonValueKind.Array)
            {
                return new List<ShopCustomer>();
            }

            // Direkt in dein ShopCustomer-API-Modell deserialisieren
            var customers = JsonSerializer.Deserialize<List<ShopCustomer>>(
                value.GetRawText(), JsonOptions) ?? new List<ShopCustomer>();

            return customers;
        }

        public async Task CreateCustomerAsync(ShopCustomer customer)
        {
            const string url = "odata/v1/Customers";

            if (customer == null) throw new ArgumentNullException(nameof(customer));

            // Minimales, sauberes API-Objekt bauen (aber weiterhin: "mit Objekt arbeiten")
            // -> nur Felder, die du wirklich setzen willst/sollst.
            var payload = BuildCustomerUpsertPayload(customer);

            // Smartstore braucht i.d.R. Email + Username
            if (string.IsNullOrWhiteSpace(payload.Email))
                throw new ArgumentException("Customer Email must not be empty.", nameof(customer));
            if (string.IsNullOrWhiteSpace(payload.Username))
                payload.Username = payload.Email;

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync(url, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var err = ExtractODataError(body);
                throw new HttpRequestException(
                    $"CreateCustomer failed {(int)response.StatusCode}: {err}\nPayload={json}\nResponse={SafeSnippet(body)}");
            }
        }

        public async Task UpdateCustomerAsync(string id, ShopCustomer customer)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Customer id must not be empty.", nameof(id));
            if (customer == null)
                throw new ArgumentNullException(nameof(customer));

            var url = $"odata/v1/Customers({id})";

            var payload = BuildCustomerUpsertPayload(customer);

            if (string.IsNullOrWhiteSpace(payload.Email))
                throw new ArgumentException("Customer Email must not be empty.", nameof(customer));
            if (string.IsNullOrWhiteSpace(payload.Username))
                payload.Username = payload.Email;

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var err = ExtractODataError(body);
                throw new HttpRequestException(
                    $"UpdateCustomer failed {(int)response.StatusCode}: {err}\nPayload={json}\nResponse={SafeSnippet(body)}");
            }
        }

        /// <summary>
        /// Baut ein "Upsert"-Objekt auf Basis deines ShopCustomer (API-Modell),
        /// ohne unnötige/read-only Felder zu schicken.
        /// </summary>
        private static ShopCustomer BuildCustomerUpsertPayload(ShopCustomer source)
        {

            return new ShopCustomer
            {
                Email = TrimOrNull(source.Email),
                Username = TrimOrNull(source.Username) ?? TrimOrNull(source.Email),

                FirstName = TrimOrNull(source.FirstName),
                LastName = TrimOrNull(source.LastName),
                FullName = null,
                Company = TrimOrNull(source.Company),

                // CustomerNumber ist oft systemseitig, nur setzen wenn du sicher bist, dass es schreibbar ist.
                CustomerNumber = null,

                Active = source.Active,

                BillingAddress = BuildAddressPayload(source.BillingAddress),
                ShippingAddress = BuildAddressPayload(source.ShippingAddress)
            };
        }

        private static ShopAddress? BuildAddressPayload(ShopAddress? a)
        {
            if (a == null) return null;

            // Wenn wirklich alles leer ist, senden wir keine Address
            if (IsAllNullOrWhitespace(
                    a.Salutation, a.Title, a.FirstName, a.LastName, a.Email, a.Company,
                    a.City, a.Address1, a.Address2, a.ZipPostalCode, a.PhoneNumber, a.FaxNumber))
            {
                return null;
            }

            return new ShopAddress
            {
                Salutation = TrimOrNull(a.Salutation),
                Title = TrimOrNull(a.Title),
                FirstName = TrimOrNull(a.FirstName),
                LastName = TrimOrNull(a.LastName),
                Email = TrimOrNull(a.Email),
                Company = TrimOrNull(a.Company),

                CountryId = a.CountryId,        // int?
                StateProvinceId = a.StateProvinceId,  // int?

                City = TrimOrNull(a.City),
                Address1 = TrimOrNull(a.Address1),
                Address2 = TrimOrNull(a.Address2),
                ZipPostalCode = TrimOrNull(a.ZipPostalCode),
                PhoneNumber = TrimOrNull(a.PhoneNumber),
                FaxNumber = TrimOrNull(a.FaxNumber)
            };
        }

        private static string? TrimOrNull(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return s.Trim();
        }

        private static bool IsAllNullOrWhitespace(params string?[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v))
                    return false;
            }
            return true;
        }

        // --- deine bestehenden Helper bleiben, nur ggf. JsonOptions oben nutzen ---
        private static string SafeSnippet(string s, int max = 2000)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length <= max ? s : s.Substring(0, max) + " ... (truncated)";
        }

        private static string ExtractODataError(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var err))
                {
                    var msg = err.TryGetProperty("message", out var m) ? (m.GetString() ?? "") : "";

                    if (string.IsNullOrWhiteSpace(msg) &&
                        err.TryGetProperty("innererror", out var inner) &&
                        inner.TryGetProperty("message", out var im))
                    {
                        msg = im.GetString() ?? "";
                    }

                    if (!string.IsNullOrWhiteSpace(msg))
                        return msg.Trim();
                }
            }
            catch { }

            return "Unknown OData error (see Response snippet).";
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null, // WICHTIG: PascalCase bleibt PascalCase
            PropertyNameCaseInsensitive = true, // fürs Deserialisieren hilfreich
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

}