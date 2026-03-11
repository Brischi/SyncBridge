using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncBridge.Core.Smartstore
{
    public class ShopProduct
    {
        public int? Id { get; set; }  
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Price { get; set; } = 0;
        public bool Published { get; set; } = true;
        public int StockQuantity { get; set; } = 0;
        public int TaxCategoryId { get; set; } = 1;  // Standard-Steuer
        public bool IsTaxExempt { get; set; } = true;   
    }
}
