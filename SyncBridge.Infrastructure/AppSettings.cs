using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncBridge.Infrastructure
{
    public class AppSettings
    {
        public DolibarrSettings Dolibarr { get; set; } = new DolibarrSettings();
        public SmartstoreSettings Smartstore { get; set; } = new SmartstoreSettings();
    }

    public class DolibarrSettings
    {
        public string BaseUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
    }

    public class SmartstoreSettings
    {
        public string BaseUrl { get; set; } = "";
        public string PublicKey { get; set; } = "";
        public string SecretKey { get; set; } = "";
    }
}
