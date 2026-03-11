using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace SyncBridge.Core.Smartstore
{
    public class ShopCustomer
    {
        // --- Root-Felder aus Smartstore Customer JSON ---
        public int? Id { get; set; }

        public Guid? CustomerGuid { get; set; }

        public string? Username { get; set; }
        public string? Email { get; set; }

        public bool? Active { get; set; }

        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? FullName { get; set; }

        public string? Company { get; set; }
        public string? CustomerNumber { get; set; }

        public int? BillingAddressId { get; set; }
        public int? ShippingAddressId { get; set; }

        public ShopAddress? BillingAddress { get; set; }
        public ShopAddress? ShippingAddress { get; set; }

        public List<ShopAddress>? Addresses { get; set; }
    }

    public class ShopAddress
    {
        public int? Id { get; set; }

        public string? Salutation { get; set; }
        public string? Title { get; set; }

        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        public string? Email { get; set; }
        public string? Company { get; set; }

        public int? CountryId { get; set; }
        public int? StateProvinceId { get; set; }

        public string? City { get; set; }
        public string? Address1 { get; set; }
        public string? Address2 { get; set; }

        public string? ZipPostalCode { get; set; }

        public string? PhoneNumber { get; set; }
        public string? FaxNumber { get; set; }
    }
}





