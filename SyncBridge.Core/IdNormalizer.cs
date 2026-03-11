using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncBridge.Core
{
    public static class IdNormalizer // Dolibarr ersetzt jedes Leerzeichen beim neu erstellen einer ID durch einen Unterstrich. Wir normalisieren hier vor dem Vergleich entsprechend
    {
        public static string Normalize(string rawId)
        {
            if (string.IsNullOrWhiteSpace(rawId))
                return "";  // Leerer String statt null/Leerzeichen

            return rawId.Replace(' ', '_').Trim();
        }
    }
}
