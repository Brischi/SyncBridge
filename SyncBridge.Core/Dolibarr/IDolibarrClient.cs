using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncBridge.Core.Dolibarr
{
    public interface IDolibarrClient
    {
        Task<int> GetProductStockAsync(string productId);
        Task<IReadOnlyList<DolibarrOrder>> GetOrdersAsync();
        // später mehr, z.B. Produkte, Lager etc.
        Task<IReadOnlyList<DolibarrProduct>> GetAllProductsAsync();

            Task<string?> CreateOrUpdateProductAsync(DolibarrProduct product); 
        Task DeleteProductAsync(string id);
        Task UpdateProductStockAsync(string productId, int quantity);
        Task DeleteAllProductsAsync();

        Task<IReadOnlyList<DolibarrCustomer>> GetAllCustomersAsync();         
        Task CreateOrUpdateCustomerAsync(DolibarrCustomer customer);          

        Task<bool> OrderExistsAsync(string externalId);
        Task CreateOrderAsync(DolibarrOrder order);



    }
}
