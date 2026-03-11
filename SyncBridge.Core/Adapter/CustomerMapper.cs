using SyncBridge.Core.Dolibarr;
using SyncBridge.Core.Smartstore;
using System;

namespace SyncBridge.Core.Adapter
{
    public static class CustomerMapper
    {
        // Dolibarr (Thirdparty) -> Smartstore
        public static ShopCustomer MapToSmartstore(DolibarrCustomer source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return new ShopCustomer
            {
                // Smartstore Id ist int? (nicht string)
                Id = TryParseInt(source.Id),

                // Smartstore CustomerNumber (nicht Ref/CodeClient)
                CustomerNumber = PrepareOptionalField(source.CodeClient) ?? PrepareOptionalField(source.Ref),

                // Smartstore Root-Namefelder
                FirstName = PrepareOptionalField(source.Firstname),
                LastName = PrepareOptionalField(source.Lastname),

                // FullName ist optional, aber praktisch
                FullName = BuildFullName(source.Firstname, source.Lastname),

                // Company kommt aus Dolibarr "Name" (Thirdparty Name)
                Company = PrepareOptionalField(source.Name),

                // Username: in Smartstore oft Pflicht/üblich, stabil = Email
                Email = PrepareOptionalField(source.Email),
                Username = PrepareOptionalField(source.Email),

                // Active ist bool? (nicht "0"/"1")
                Active = ToBoolFlag(source.Status),

                // Adresse/Kontakt liegen bei Smartstore unter BillingAddress
                BillingAddress = BuildBillingAddress(source),

                // Optional: Id-Referenzen (meist vom System vergeben)
                BillingAddressId = null,
                ShippingAddressId = null
            };
        }

        // Smartstore -> Dolibarr (Thirdparty)
        public static DolibarrCustomer MapToDolibarr(ShopCustomer source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return new DolibarrCustomer
            {
                Id = source.Id?.ToString(),

                // Dolibarr: du hattest Ref und CodeClient getrennt
                Ref = PrepareOptionalField(source.CustomerNumber),
                CodeClient = PrepareOptionalField(source.CustomerNumber),

                // Dolibarr Name / Alias
                Name = PrepareOptionalField(source.Company) ?? PrepareOptionalField(source.FullName),
                NameAlias = null,

                Firstname = PrepareOptionalField(source.FirstName),
                Lastname = PrepareOptionalField(source.LastName),
                TypentCode = null,

                Email = PrepareOptionalField(source.Email),

                // Telefon/Fax/Adresse aus BillingAddress
                Phone = PrepareOptionalField(source.BillingAddress?.PhoneNumber),
                Fax = PrepareOptionalField(source.BillingAddress?.FaxNumber),

                Address = PrepareOptionalField(source.BillingAddress?.Address1),
                Zip = PrepareOptionalField(source.BillingAddress?.ZipPostalCode),
                Town = PrepareOptionalField(source.BillingAddress?.City),

                // Smartstore CountryId ist int? → Dolibarr war string
                CountryId = source.BillingAddress?.CountryId?.ToString(),
                CountryCode = null, // TwoLetterIsoCode hast du in deinem ShopAddress-Modell nicht drin

                Status = ToDolibarrFlag(source.Active),
                Client = "1"
            };
        }

        private static ShopAddress? BuildBillingAddress(DolibarrCustomer c)
        {
            // Wenn du KEINE Adressdaten hast, gib null zurück (dann sendest du kein BillingAddress-Objekt)
            if (IsAllNullOrWhitespace(c.Address, c.Zip, c.Town, c.Phone, c.Fax, c.Email, c.Firstname, c.Lastname, c.Name, c.CountryId))
                return null;

            return new ShopAddress
            {
                FirstName = PrepareOptionalField(c.Firstname),
                LastName = PrepareOptionalField(c.Lastname),

                Email = PrepareOptionalField(c.Email),
                Company = PrepareOptionalField(c.Name),

                Address1 = PrepareOptionalField(c.Address),
                ZipPostalCode = PrepareOptionalField(c.Zip),
                City = PrepareOptionalField(c.Town),

                PhoneNumber = PrepareOptionalField(c.Phone),
                FaxNumber = PrepareOptionalField(c.Fax),

                CountryId = TryParseInt(c.CountryId)
            };
        }

        private static bool? ToBoolFlag(string? dolibarrFlag)
        {
            if (string.IsNullOrWhiteSpace(dolibarrFlag)) return null;
            var v = dolibarrFlag.Trim().ToLowerInvariant();
            return (v == "1" || v == "true" || v == "yes");
        }

        private static string ToDolibarrFlag(bool? value)
        {
            if (!value.HasValue) return "0";
            return value.Value ? "1" : "0";
        }

        private static string? PrepareOptionalField(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim();
        }

        private static int? TryParseInt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (int.TryParse(value.Trim(), out var i)) return i;
            return null;
        }

        private static string? BuildFullName(string? firstName, string? lastName)
        {
            var fn = PrepareOptionalField(firstName);
            var ln = PrepareOptionalField(lastName);

            if (fn == null && ln == null) return null;
            return $"{fn} {ln}".Trim();
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
    }
}
