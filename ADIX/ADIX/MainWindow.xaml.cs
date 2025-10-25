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

                // REMOVED: Duplicate sync call
                // Database.InitializeAsync() already handles syncing

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

        private void NavigateToPage(string pageName)
        {
            // Update active button in sidebar
            SidebarControl.SetActivePage(pageName);

            switch (pageName)
            {
                case "Dashboard":
                    MainFrame.Navigate(new Dashboard());
                    break;
                case "POS":
                    // Create POS page and refresh items when loaded
                    var posPage = new PointOfSale();
                    posPage.Loaded += async (s, e) =>
                    {
                        // Trigger background sync check when POS loads
                        if (Database.IsInternetAvailable() && Database.IsSyncRequired())
                        {
                            try
                            {
                                await Database.CheckAndSyncAsync();

                                // Refresh POS items after sync
                                if (posPage.DataContext is ViewModels.PointOfSaleViewModel vm)
                                {
                                    vm.ReloadItemsFromDatabase();
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Background sync on POS load failed: {ex.Message}");
                            }
                        }
                    };
                    MainFrame.Navigate(posPage);
                    break;
                case "Inventory":
                    MainFrame.Navigate(new Inventory());
                    break;
                case "Products":
                    MainFrame.Navigate(new Products());
                    break;
                case "ItemGroup":
                    MainFrame.Navigate(new ItemGroup());
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