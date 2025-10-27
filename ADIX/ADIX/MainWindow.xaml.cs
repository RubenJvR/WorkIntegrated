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
                // Initialize database (this already syncs if internet is available)
                await Database.InitializeAsync();

                // Show appropriate message based on connection status
                if (Database.IsInternetAvailable())
                {
                    var lastSync = Database.GetLastSyncTime();
                    if (lastSync != DateTime.MinValue)
                    {
                        MessageBox.Show($"Database initialized and synced with Azure SQL.\nLast sync: {lastSync:yyyy-MM-dd HH:mm:ss}",
                                      "Success",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Database initialized successfully.",
                                      "Success",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Information);
                    }

                    // ✅ NEW: Sync all tables (except ITEM) after initialization
                    try
                    {
                        await Task.Run(() => Database.SyncAllTablesFromAzure());
                        Console.WriteLine("All tables synced successfully during startup");
                    }
                    catch (Exception syncEx)
                    {
                        Console.WriteLine($"Warning: Full table sync failed during startup: {syncEx.Message}");
                        // Don't show error message - continue with app startup
                    }
                }
                else
                {
                    MessageBox.Show("Database initialized in offline mode. Data will sync when internet is available.",
                                  "Offline Mode",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                }

                // Navigate to main page AFTER initialization
                MainFrame.Navigate(new Dashboard());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database initialization failed: {ex.Message}",
                              "Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);

                // Still navigate to dashboard even if initialization fails
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
            // Update active button in sidebar
            SidebarControl.SetActivePage(pageName);

            // Sync before navigating if needed
            if (Database.IsInternetAvailable() && Database.IsSyncRequired())
            {
                try
                {
                    Database.SyncAllTablesFromAzure(); 
                    Console.WriteLine("All tables synced successfully during startup");
                }
                catch (Exception syncEx)
                {
                    Console.WriteLine($"Warning: Full table sync failed during startup: {syncEx.Message}");
                    
                }
            }

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