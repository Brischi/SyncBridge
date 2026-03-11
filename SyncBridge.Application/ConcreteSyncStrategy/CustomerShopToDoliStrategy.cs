using SyncBridge.Core;
using SyncBridge.Core.Adapter;
using SyncBridge.Core.Dolibarr;
using SyncBridge.Core.Smartstore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncBridge.ApplicationLayer.ConcreteSyncStrategy
{
    internal class CustomerShopToDoliStrategy : ISyncStrategy
    {
        private readonly IDolibarrClient _dolibarr;
        private readonly ISmartstoreClient _smartstore;
        private readonly ILogger _logger;

        public CustomerShopToDoliStrategy(
            IDolibarrClient dolibarr,
            ISmartstoreClient smartstore,
            ILogger logger)
        {
            _dolibarr = dolibarr;
            _smartstore = smartstore;
            _logger = logger;
        }

        public async Task<SyncResult> SyncAsync()
        {
            _logger.Info("CUSTOMER SYNC START (Shop → Dolibarr)");

            ////////////////////////////////
            // 1) SHOP-Kunden laden
            ////////////////////////////////

            var allShopCustomers = await _smartstore.GetAllCustomersAsync();
            _logger.Info($"SHOP: {allShopCustomers.Count} Kunden insgesamt geladen");

            // Filter: Id + Email (Id ist int? im Smartstore-Modell, Email string)
            var shopCustomers = allShopCustomers
                .Where(c => c != null)
                .Where(c => c.Id.HasValue)
                .Where(c => !string.IsNullOrWhiteSpace(c.Email))
                .ToList();

            _logger.Info($"SHOP: {shopCustomers.Count} Kunden nach Filter (Id + Email)");

            ////////////////////////////////
            // 2) DOLIBARR-Kunden laden
            ////////////////////////////////

            var doliCustomers = await _dolibarr.GetAllCustomersAsync();
            _logger.Info($"DOLI: {doliCustomers.Count} Kunden aus Dolibarr geladen");

            // Key-Strategie:
            // primär Email (lower/trim), sonst CodeClient, sonst Ref, sonst Id-normalized
            var doliKeyToCustomer = doliCustomers
                .Select(c => new { Customer = c, Key = NormalizeCustomerKey(c) })
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key)
                .ToDictionary(g => g.Key, g => g.First().Customer);

            ////////////////////////////////
            // 3) CREATE / UPDATE (Delta)
            ////////////////////////////////

            var created = 0;
            var updated = 0;
            var skipped = 0;

            foreach (var shop in shopCustomers)
            {
                var key = NormalizeCustomerKey(shop);
                if (string.IsNullOrWhiteSpace(key))
                {
                    _logger.Warn("SHOP CUSTOMER ohne gültigen Key (Email/CustomerNumber/Id) – übersprungen.");
                    continue;
                }

                // Shop -> Dolibarr Mapping (Mapper übersetzt Smartstore-Namen -> Dolibarr-Namen)
                var mapped = CustomerMapper.MapToDolibarr(shop);

                if (doliKeyToCustomer.TryGetValue(key, out var existing) && existing != null)
                {
                    // UPDATE: Dolibarr braucht Id, damit PUT/UPDATE läuft
                    mapped.Id = existing.Id;

                    if (!HasCustomerChanges(existing, mapped))
                    {
                        skipped++;
                        _logger.Info($"SKIP CUSTOMER: Key={key} ({mapped.Name}/{mapped.Email}) (keine Änderungen)");
                        continue;
                    }

                    await _dolibarr.CreateOrUpdateCustomerAsync(mapped);
                    updated++;
                    _logger.Info($"UPDATED CUSTOMER: Key={key} ({mapped.Name}/{mapped.Email})");
                }
                else
                {
                    // CREATE: Id leer lassen (null ist sauberer als "")
                    mapped.Id = null;

                    await _dolibarr.CreateOrUpdateCustomerAsync(mapped);
                    created++;
                    _logger.Info($"CREATED CUSTOMER: Key={key} ({mapped.Name}/{mapped.Email})");
                }
            }

            ////////////////////////////////
            // 4) Zusammenfassung
            ////////////////////////////////

            _logger.Info($"Customer Sync: Created={created}, Updated={updated}, Skipped={skipped} = {created + updated + skipped} total");
            if (created == 0 && updated == 0)
                _logger.Info("Alles up to date - nichts zu syncen!");

            return new SyncResult
            {
                Created = created,
                Updated = updated
            };
        }

        // Vergleich (nur relevante Felder)
        private static bool HasCustomerChanges(DolibarrCustomer existing, DolibarrCustomer incoming)
        {
            static string Norm(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return "";
                var t = s.Trim();
                t = System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ");
                return t;
            }

            static string NormEmail(string? s) => Norm(s).ToLowerInvariant();

            return Norm(existing.Name) != Norm(incoming.Name)
                || Norm(existing.NameAlias) != Norm(incoming.NameAlias)
                || Norm(existing.Firstname) != Norm(incoming.Firstname)
                || Norm(existing.Lastname) != Norm(incoming.Lastname)
                || Norm(existing.TypentCode) != Norm(incoming.TypentCode)

                || NormEmail(existing.Email) != NormEmail(incoming.Email)
                || Norm(existing.Phone) != Norm(incoming.Phone)
                || Norm(existing.PhoneMobile) != Norm(incoming.PhoneMobile)
                || Norm(existing.Fax) != Norm(incoming.Fax)

                || Norm(existing.Address) != Norm(incoming.Address)
                || Norm(existing.Zip) != Norm(incoming.Zip)
                || Norm(existing.Town) != Norm(incoming.Town)
                || Norm(existing.CountryId) != Norm(incoming.CountryId)
                || Norm(existing.CountryCode) != Norm(incoming.CountryCode)

                // Nur vergleichen, wenn du das wirklich synchron halten willst:
                || Norm(existing.Status) != Norm(incoming.Status)
                || Norm(existing.Client) != Norm(incoming.Client)

                || Norm(existing.CodeClient) != Norm(incoming.CodeClient)
                || Norm(existing.Ref) != Norm(incoming.Ref);
        }

        // Key-Strategie:
        // 1) Email (lower/trim)
        // 2) CustomerNumber
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

            if (!string.IsNullOrWhiteSpace(c.CodeClient))
                return c.CodeClient.Trim();

            if (!string.IsNullOrWhiteSpace(c.Ref))
                return c.Ref.Trim();

            if (!string.IsNullOrWhiteSpace(c.Id))
                return IdNormalizer.Normalize(c.Id);

            return "";
        }

        public Task CleanTargetAsync()
        {
            // bewusst kein Löschen von Kunden
            return Task.CompletedTask;
        }
    }
}
