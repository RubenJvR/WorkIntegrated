using System;
using System.Windows;


namespace ADIX
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Subscribe to navigation events
            SidebarControl.NavigationRequested += Sidebar_NavigationRequested;
            SidebarControl.CollapseToggled += Sidebar_CollapseToggled;

            // Attach the Loaded event handler for async initialization
            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize database
                await Database.InitializeAsync();

                // Perform comprehensive sync to get all missing data
                if (Database.IsInternetAvailable())
                {
                    try
                    {
                        await Database.SyncAllMissingDataAsync();
                        var lastSync = Database.GetLastSyncTime();

                        MessageBox.Show($"Database initialized and fully synced with Azure SQL.\nLast sync: {lastSync:yyyy-MM-dd HH:mm:ss}",
                                      "Success",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Information);
                    }
                    catch (Exception syncEx)
                    {
                        Console.WriteLine($"Warning: Comprehensive sync failed: {syncEx.Message}");
                        MessageBox.Show("Database initialized but sync incomplete. Some data may be missing.",
                                      "Partial Sync",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("Database initialized in offline mode. Data will sync when internet is available.",
                                  "Offline Mode",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                }

                // Navigate to main page
                MainFrame.Navigate(new Dashboard());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database initialization failed: {ex.Message}",
                              "Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
                MainFrame.Navigate(new Dashboard());
            }
        }

        private void Sidebar_NavigationRequested(object sender, string pageName)
        {
            NavigateToPage(pageName);
        }

        private void Sidebar_CollapseToggled(object sender, bool isCollapsed)
        {
            // Adjust sidebar width when collapsed/expanded
            if (isCollapsed)
            {
                SidebarColumn.Width = new GridLength(80); // Slightly wider for icons
            }
            else
            {
                SidebarColumn.Width = new GridLength(220); // Expanded width
            }
        }

        private async void NavigateToPage(string pageName)
        {
            SidebarControl.SetActivePage(pageName);

            // Use comprehensive sync before navigation if needed
            if (Database.IsInternetAvailable() && Database.IsSyncRequired())
            {
                try
                {
                    await Database.SyncAllMissingDataAsync();
                    Console.WriteLine("Comprehensive sync completed before navigation");
                }
                catch (Exception syncEx)
                {
                    Console.WriteLine($"Warning: Comprehensive sync failed during navigation: {syncEx.Message}");
                    // Fall back to basic sync
                    try
                    {
                        await Database.CheckAndSyncAsync();
                    }
                    catch { /* Ignore fallback failure */ }
                }
            }

            // Rest of navigation code remains the same...
            switch (pageName)
            {
                case "Dashboard":
                    MainFrame.Navigate(new Dashboard());
                    break;
                case "POS":
                    var posPage = new PointOfSale();
                    MainFrame.Navigate(posPage);
                    break;
                case "Inventory":
                    var inventoryPage = new Inventory();
                    MainFrame.Navigate(inventoryPage);
                    break;
                case "Products":
                    MainFrame.Navigate(new Products());
                    break;
                case "Suppliers":
                    MainFrame.Navigate(new SupplierPage());
                    break;
                case "Finance":
                    MainFrame.Navigate(new Finance());
                    break;
                case "MonthlyReport":
                    MainFrame.Navigate(new MonthlyReport());
                    break;
                case "Sales":
                    MainFrame.Navigate(new Sales());
                    break;
            }
        }

        private void SidebarControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Optional: Any sidebar initialization code
        }
    }
}