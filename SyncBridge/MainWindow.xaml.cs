using SyncBridge.ApplicationLayer;
using SyncBridge.Core;
using SyncBridge.Core.Dolibarr;
using System;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Threading;


namespace SyncBridge
{
    public partial class MainWindow : Window
    {
        private readonly ISyncStrategyFactory _factory;
        private readonly UiLogger _logger;
        private bool _uiReady = false;
        private readonly DispatcherTimer _autoSyncTimer = new DispatcherTimer();
        private bool _autoSyncRunning = false;

        public MainWindow(ISyncStrategyFactory factory, UiLogger logger)
        {
            InitializeComponent();
            _factory = factory;
            _logger = logger;
            _autoSyncTimer.Tick += AutoSyncTimer_Tick;

            // Logs aus UiLogger im UI anzeigen, z.B. als ItemsSource
            LogItems.ItemsSource = _logger.Entries;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _uiReady = true;
            ApplyAutoSyncUiState();
            UpdateAutoSyncTimer();
        }

        // =====================
        // LOG
        // =====================
        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            _logger.Entries.Clear();
            _logger.Info("Logs gelöscht");
        }

        private void AddLog(string text)
        {
            _logger.Info(text);
        }

        // =====================
        // SYNC BUTTONS
        // =====================
        private async void SyncAllButton_Click(object sender, RoutedEventArgs e)
        {
            AddLog("Sync All gestartet");
            await SyncProductsInternalAsync();
        }

        private async void SyncProducts_Click(object sender, RoutedEventArgs e)
        {
        await SyncProductsInternalAsync();
        }
        private async Task SyncProductsInternalAsync()
        {
            AddLog("Products Sync gestartet");

            var direction = ProductsSourceText.Text == "Smartstore"
                ? SyncDirection.ShopToDolibarr
                : SyncDirection.DolibarrToShop;

            var strategy = _factory.Create(SyncCategory.Products, direction);

            try
            {
                var result = await strategy.SyncAsync();
                AddLog($"Products Sync fertig: {result.Total} Einträge");
            }
            catch (Exception ex)
            {
                AddLog($"Products Sync Fehler: {ex.Message}");
            }
        }

        private async void SyncCustomer_Click(object sender, RoutedEventArgs e)
        {
            AddLog("Customer Sync gestartet");

            var direction = CustomerSourceText.Text == "Smartstore"
                ? SyncDirection.ShopToDolibarr
                : SyncDirection.DolibarrToShop;

            var strategy = _factory.Create(SyncCategory.Customers, direction);

            try
            {
                var result = await strategy.SyncAsync();
                AddLog($"Customer Sync fertig: {result.Total} Einträge");
            }
            catch (Exception ex)
            {
                AddLog($"Customer Sync Fehler: {ex.Message}");
                MessageBox.Show(ex.Message, "Sync-Fehler");
            }
        }

        private async void SyncOrders_Click(object sender, RoutedEventArgs e)
        {
            AddLog("Orders Sync gestartet");

            var direction = OrdersSourceText.Text == "Smartstore"
                ? SyncDirection.ShopToDolibarr
                : SyncDirection.DolibarrToShop;

            var strategy = _factory.Create(SyncCategory.Customers, direction);
            try
            {

                var result = await strategy.SyncAsync();
                AddLog($"Customer Sync fertig: {result.Total} Einträge");
            }
            catch (Exception ex)
            {
                AddLog($"Customer Sync Fehler: {ex.Message}");
                MessageBox.Show(ex.Message, "Sync-Fehler");
            }
        }

        // =====================
        // SETTINGS – PRODUCTS
        // =====================
        private void ProductsForward_Checked(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            ProductsSourceText.Text = "Dolibarr";
            ProductsTargetText.Text = "Smartstore";
            AddLog("Products Richtung: Dolibarr → Smartstore");
        }

        private void ProductsBackward_Checked(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            ProductsSourceText.Text = "Smartstore";
            ProductsTargetText.Text = "Dolibarr";
            AddLog("Products Richtung: Smartstore → Dolibarr");
        }

        // =====================
        // SETTINGS – CUSTOMER
        // =====================
        private void CustomerForward_Checked(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            CustomerSourceText.Text = "Dolibarr";
            CustomerTargetText.Text = "Smartstore";
            AddLog("Customer Richtung: Dolibarr → Smartstore");
        }

        private void CustomerBackward_Checked(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            CustomerSourceText.Text = "Smartstore";
            CustomerTargetText.Text = "Dolibarr";
            AddLog("Customer Richtung: Smartstore → Dolibarr");
        }

        // =====================
        // SETTINGS – ORDERS
        // =====================
        private void OrdersForward_Checked(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            OrdersSourceText.Text = "Dolibarr";
            OrdersTargetText.Text = "Smartstore";
            AddLog("Orders Richtung: Dolibarr → Smartstore");
        }

        private void OrdersBackward_Checked(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            OrdersSourceText.Text = "Smartstore";
            OrdersTargetText.Text = "Dolibarr";
            AddLog("Orders Richtung: Smartstore → Dolibarr");
        }

        // =====================
        // SETTINGS – AUTO SYNC
        // =====================
        private void ApplyAutoSyncUiState()
        {
            bool intervalOn = AutoSyncIntervalRadio.IsChecked == true;

            AutoSyncIntervalTextBox.IsEnabled = intervalOn;

            if (!intervalOn)
                return;

            if (!int.TryParse(AutoSyncIntervalTextBox.Text, out int minutes) || minutes < 1)
            {
                AutoSyncIntervalTextBox.Text = "15";
                minutes = 15;
            }
        }
        private void UpdateAutoSyncTimer()
        {
            if (AutoSyncIntervalRadio.IsChecked != true)
            {
                _autoSyncTimer.Stop();
                return;
            }

            if (!int.TryParse(AutoSyncIntervalTextBox.Text, out int minutes) || minutes < 1)
            {
                AutoSyncIntervalTextBox.Text = "15";
                minutes = 15;
            }

            _autoSyncTimer.Interval = TimeSpan.FromMinutes(minutes);
            _autoSyncTimer.Start();
        }

        private async void AutoSyncTimer_Tick(object? sender, EventArgs e)
        {
            if (_autoSyncRunning || AutoSyncIntervalRadio.IsChecked != true)
                return;

            _autoSyncRunning = true;
            try
            {
                AddLog("Auto-Sync: Sync All wird ausgelöst");
                await Task.Yield();
                SyncAllButton_Click(sender, new RoutedEventArgs());
            }
            finally
            {
                _autoSyncRunning = false;
            }
        }

        private void AutoSyncOff_Checked(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            ApplyAutoSyncUiState();
            AddLog("Auto-Sync: Aus");
            UpdateAutoSyncTimer();
        }

        private void AutoSyncInterval_Checked(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;

            ApplyAutoSyncUiState();
            UpdateAutoSyncTimer();
            AddLog($"Auto-Sync: Alle {AutoSyncIntervalTextBox.Text} Minuten");
        }
        private void AutoSyncIntervalTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!_uiReady) return;

            ApplyAutoSyncUiState();
            UpdateAutoSyncTimer();
        }
    }
}
