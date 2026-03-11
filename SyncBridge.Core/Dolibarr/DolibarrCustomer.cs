using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncBridge.Core.Dolibarr
{
    // Dolibarr "Customer" = Thirdparty (Kunde/Firma)
    public class DolibarrCustomer
    {
        // Identifikation
        public string? Id { get; set; }
        public string? Ref { get; set; }

        // Name
        public string? Name { get; set; }            // "name"
        public string? NameAlias { get; set; }       // "name_alias"
        public string? Firstname { get; set; }       // "firstname"
        public string? Lastname { get; set; }        // "lastname"
        public string? TypentCode { get; set; }      // "typent_code" (z.B. TE_PRIVATE)

        // Kontakt
        public string? Email { get; set; }           // "email"
        public string? Phone { get; set; }           // "phone"
        public string? PhoneMobile { get; set; }     // "phone_mobile"
        public string? Fax { get; set; }             // "fax"

        // Adresse
        public string? Address { get; set; }         // "address"
        public string? Zip { get; set; }             // "zip"
        public string? Town { get; set; }            // "town"
        public string? CountryId { get; set; }       // "country_id" (String im JSON)
        public string? CountryCode { get; set; }     // "country_code" (z.B. DE)

        // Status
        public string? Status { get; set; }          // "status" (z.B. "1")
        public string? Client { get; set; }          // "client" ("1" = Kunde)

        // Kundennummer
        public string? CodeClient { get; set; }      // "code_client"
    }
}
