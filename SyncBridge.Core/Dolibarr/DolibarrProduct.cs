using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncBridge.Core.Dolibarr
{
    public class DolibarrProduct
    {
        public string? Id { get; set; }
        public string Ref { get; set; } = "";
        public string Label { get; set; } = "";
        public string Price { get; set; } = "";
        public string TvaTx { get; set; } = "";
        public string Status { get; set; } = "";
        public string Stock { get; set; } = "";

        public string WarehouseId { get; set; } = "1"; // Standard-Lager

    }
}
