using System.Windows;
using SyncBridge.ApplicationLayer;
using SyncBridge.Core;
using SyncBridge.Core.Dolibarr;
using SyncBridge.Core.Smartstore;
using SyncBridge.Infrastructure;

namespace SyncBridge
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Konfiguration laden
            var settings = AppSettingsLoader.Load();

            // 2. Infrastruktur auf Basis der Konfig erstellen
            IDolibarrClient dolibarr = new DolibarrHttpClient(settings.Dolibarr);

            ISmartstoreClient smart = new SmartstoreHttpClient(
                settings.Smartstore.BaseUrl,
                settings.Smartstore.PublicKey,
                settings.Smartstore.SecretKey);

            var uiLogger = new UiLogger();

            // 3. Application Layer
            ISyncStrategyFactory factory = new SyncStrategyFactory(dolibarr, smart, uiLogger);


            // 4. UI bekommt nur noch den fertigen Service
            var mainWindow = new MainWindow(factory, uiLogger);
            mainWindow.Show();
        }
    }
}
