namespace SyncBridge.Core.Smartstore;

public interface ISmartstoreClient
{
    // Produkte
    Task<IReadOnlyList<ShopProduct>> GetAllProductsAsync();
    Task CreateOrUpdateProductAsync(ShopProduct product);
    Task DeleteAllProductsAsync();
    Task DeleteProductAsync(string sku);
    Task CreateProductAsync(ShopProduct product);
    Task UpdateProductAsync(string id, ShopProduct product);

    // Bestellungen
    Task<IReadOnlyList<ShopOrder>> GetOrdersAsync(DateTime? since = null);

    Task<string> FindProductIdBySkuAsync(string sku);
    // Kunden
    Task<IReadOnlyList<ShopCustomer>> GetAllCustomersAsync();
    Task CreateCustomerAsync(ShopCustomer customer);
    Task UpdateCustomerAsync(string id, ShopCustomer customer);
}
