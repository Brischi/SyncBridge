using SyncBridge.Core;
using SyncBridge.Core.Adapter;
using SyncBridge.Core.Dolibarr;
using SyncBridge.Core.Smartstore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncBridge.ApplicationLayer.ConcreteSyncStrategy
{
    public class CustomerDoliToShopStrategy : ISyncStrategy
    {
        private readonly IDolibarrClient _doliClient;
        private readonly ISmartstoreClient _shopClient;
        private readonly ILogger _logger;

        public CustomerDoliToShopStrategy(IDolibarrClient doli, ISmartstoreClient shop, ILogger logger)
        {
            _doliClient = doli;
            _shopClient = shop;
            _logger = logger;
        }

        public async Task<SyncResult> SyncAsync()
        {
            _logger.Info("SYNC START (CUSTOMERS DOLI->SHOP)");

            // 1) SHOP-Kunden laden
            var shopCustomers = await _shopClient.GetAllCustomersAsync();
            _logger.Info($"SHOP: {shopCustomers.Count} Kunden aus Smartstore geladen");

            // Key = Normalized Customer Key (primär Email, sonst CustomerNumber, sonst Id)
            var shopKeyToCustomer = shopCustomers
                .Select(c => new { Customer = c, Key = NormalizeCustomerKey(c) })
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key)
                .ToDictionary(
                    g => g.Key,
                    // Bei Duplikaten: bevorzugt Active==true, sonst ersten nehmen
                    g => g.FirstOrDefault(x => x.Customer.Active == true)?.Customer ?? g.First().Customer
                );

            var shopKeys = shopKeyToCustomer.Keys.ToHashSet();

            // primitives Dictionary: Key -> ShopId (string)
            var shopKeyToId = shopKeyToCustomer.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Id?.ToString()
            );

            _logger.Info($"SHOP: {shopKeys.Count} eindeutige Kunden-Keys nach Gruppierung (Duplikate entfernt)");

            // 2) DOLIBARR-Kunden laden
            var doliCustomers = await _doliClient.GetAllCustomersAsync();
            _logger.Info($"DOLI: {doliCustomers.Count} Kunden aus Dolibarr geladen");

            var doliKeySet = doliCustomers
                .Select(c => NormalizeCustomerKey(c))
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .ToHashSet();

            _logger.Info($"DOLI: {doliKeySet.Count} Kunden mit gültigem Key");

            // 3) DEACTIVATE: Shop-Kunden, die in Dolibarr nicht mehr existieren -> Active = false
            var deactivated = 0;
            var created = 0;
            var updated = 0;

            foreach (var key in shopKeys)
            {
                if (!doliKeySet.Contains(key))
                {
                    if (!shopKeyToCustomer.TryGetValue(key, out var existing) || existing == null)
                    {
                        _logger.Warn($"KEY {key}: ShopCustomer nicht gefunden – Deaktivierung übersprungen.");
                        continue;
                    }

                    shopKeyToId.TryGetValue(key, out var shopIdStr);

                    // Nur deaktivieren, wenn aktuell aktiv
                    if (existing.Active == true)
                    {
                        if (string.IsNullOrWhiteSpace(shopIdStr))
                        {
                            _logger.Warn($"KEY {key}: in Dolibarr nicht vorhanden, aber keine Shop-ID vorhanden – Deaktivierung übersprungen.");
                            continue;
                        }

                        existing.Active = false;
                        await _shopClient.UpdateCustomerAsync(shopIdStr, existing);
                        _logger.Info($"CUSTOMER DEACTIVATED: Key={key} Id={shopIdStr}");
                        deactivated++;
                    }
                    else
                    {
                        _logger.Info($"CUSTOMER ALREADY INACTIVE: Key={key} Id={shopIdStr ?? "?"}");
                    }
                }
            }

            // 4) CREATE/UPDATE
            foreach (var doliCustomer in doliCustomers)
            {
                var key = NormalizeCustomerKey(doliCustomer);
                if (string.IsNullOrWhiteSpace(key))
                {
                    _logger.Warn("DOLI CUSTOMER ohne gültigen Key (Email/CustomerNumber/Id) – übersprungen.");
                    continue;
                }

                // Mapper liefert jetzt ein Smartstore-konformes ShopCustomer-API-Modell
                var incoming = CustomerMapper.MapToSmartstore(doliCustomer);

                if (shopKeyToId.TryGetValue(key, out var shopIdStr) && !string.IsNullOrWhiteSpace(shopIdStr))
                {
                    if (!shopKeyToCustomer.TryGetValue(key, out var existing) || existing == null)
                    {
                        _logger.Warn($"KEY {key}: ShopCustomer nicht gefunden – Update übersprungen.");
                        continue;
                    }

                    if (HasChanges(existing, incoming))
                    {
                        // Nur sync-relevante Felder übernehmen. ID bleibt.
                        existing.Email = incoming.Email;
                        existing.Username = incoming.Username;

                        existing.FirstName = incoming.FirstName;
                        existing.LastName = incoming.LastName;
                        existing.FullName = incoming.FullName;
                        existing.Company = incoming.Company;

                        existing.CustomerNumber = incoming.CustomerNumber;

                        existing.Active = incoming.Active;

                        // Optional: Adressen nur übernehmen, wenn du sie im Mapper befüllst
                        existing.BillingAddress = incoming.BillingAddress;
                        existing.ShippingAddress = incoming.ShippingAddress;

                        await _shopClient.UpdateCustomerAsync(shopIdStr, existing);
                        _logger.Info($"CUSTOMER UPDATED: Key={key} Id={shopIdStr}");
                        updated++;
                    }
                    else
                    {
                        _logger.Info($"CUSTOMER SKIP: Key={key} (keine Änderungen)");
                    }
                }
                else
                {
                    await _shopClient.CreateCustomerAsync(incoming);
                    _logger.Info($"CUSTOMER CREATED: Key={key}");
                    created++;
                }
            }

            _logger.Info($"Sync Customers: Deactivated={deactivated}, Created={created}, Updated={updated} = {deactivated + created + updated} total");

            if (deactivated == 0 && created == 0 && updated == 0)
                _logger.Info("Alles up to date - nichts zu syncen!");

            return new SyncResult
            {
                Deactivated = deactivated,
                Created = created,
                Updated = updated
            };
        }

        // Vergleich: Welche Felder sollen ein Update triggern?
        private static bool HasChanges(ShopCustomer existing, ShopCustomer incoming)
        {
            string Norm(string? s) => (s ?? "").Trim();
            bool NormB(bool? b) => b ?? false;

            return Norm(existing.Email) != Norm(incoming.Email)
                || Norm(existing.Username) != Norm(incoming.Username)

                || Norm(existing.FirstName) != Norm(incoming.FirstName)
                || Norm(existing.LastName) != Norm(incoming.LastName)
                || Norm(existing.FullName) != Norm(incoming.FullName)
                || Norm(existing.Company) != Norm(incoming.Company)

                || Norm(existing.CustomerNumber) != Norm(incoming.CustomerNumber)

                || NormB(existing.Active) != NormB(incoming.Active)

                || AddressChanged(existing.BillingAddress, incoming.BillingAddress)
                || AddressChanged(existing.ShippingAddress, incoming.ShippingAddress);
        }

        private static bool AddressChanged(ShopAddress? a, ShopAddress? b)
        {
            string Norm(string? s) => (s ?? "").Trim();
            int NormI(int? i) => i ?? 0;

            if (a == null && b == null) return false;
            if (a == null || b == null) return true;

            return Norm(a.FirstName) != Norm(b.FirstName)
                || Norm(a.LastName) != Norm(b.LastName)
                || Norm(a.Email) != Norm(b.Email)
                || Norm(a.Company) != Norm(b.Company)
                || NormI(a.CountryId) != NormI(b.CountryId)
                || NormI(a.StateProvinceId) != NormI(b.StateProvinceId)
                || Norm(a.City) != Norm(b.City)
                || Norm(a.Address1) != Norm(b.Address1)
                || Norm(a.Address2) != Norm(b.Address2)
                || Norm(a.ZipPostalCode) != Norm(b.ZipPostalCode)
                || Norm(a.PhoneNumber) != Norm(b.PhoneNumber)
                || Norm(a.FaxNumber) != Norm(b.FaxNumber);
        }

        // Key-Strategie (stabil):
        // 1) Email (lower/trim)
        // 2) CustomerNumber (trim)
        // 3) Id
        private static string NormalizeCustomerKey(ShopCustomer c)
        {
            if (!string.IsNullOrWhiteSpace(c.Email))
                return c.Email.Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(c.CustomerNumber))
                return c.CustomerNumber.Trim();

            if (c.Id.HasValue)
                return c.Id.Value.ToString();

            return "";
        }

        private static string NormalizeCustomerKey(DolibarrCustomer c)
        {
            if (!string.IsNullOrWhiteSpace(c.Email))
                return c.Email.Trim().ToLowerInvariant();

            // Dolibarr: CodeClient/Ref als “Kundennummer”-Ersatz, falls keine Email vorhanden ist
            if (!string.IsNullOrWhiteSpace(c.CodeClient))
                return c.CodeClient.Trim();

            if (!string.IsNullOrWhiteSpace(c.Ref))
                return c.Ref.Trim();

            if (!string.IsNullOrWhiteSpace(c.Id))
                return IdNormalizer.Normalize(c.Id);

            return "";
        }

        // Optional wie beim Product-Strategy: nur für Dev/Test
        public async Task CleanTargetAsync()
        {
            //await _shopClient.DeleteAllCustomersAsync();
            //_logger.Info("Shop-Kunden komplett geleert!");
        }
    }
}
