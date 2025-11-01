using System;
using System.Windows;
using System.Windows.Controls;


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

                SidebarControl.ApplyRoleBasedRestrictions();

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
                SidebarColumn.Width = new GridLength(80); 
            }
            else
            {
                SidebarColumn.Width = new GridLength(220); 
            }
        }

        private async void NavigateToPage(string pageName)
        {

            if (!UserSession.IsAdmin && !IsAllowedPageForNonAdmin(pageName))
            {
                MessageBox.Show($"Access Denied: You don't have permission to access {pageName}.\n\nPlease contact an administrator.",
                               "Access Denied",
                               MessageBoxButton.OK,
                               MessageBoxImage.Warning);
                return;
            }

            SidebarControl.SetActivePage(pageName);

            // Perform lightweight data sync
            if (Database.IsInternetAvailable())
            {
                try
                {
                    await Database.SyncAllMissingDataAsync();
                    Console.WriteLine("Quick sync completed during page navigation");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Quick sync failed: {ex.Message}");
                }
            }

            // Navigate to selected page
            Page targetPage = pageName switch
            {
                "Dashboard" => new Dashboard(),
                "POS" => new PointOfSale(),
                "Inventory" => new Inventory(),
                "Suppliers" => new SupplierPage(),
                "Finance" => new Finance(),
                "MonthlyReport" => new MonthlyReport(),
                "Sales" => new Sales(),
                "Staff" => new ADIX.Pages.Staff(),
                _ => new Dashboard()
            };

            MainFrame.Navigate(targetPage);
        }
        private bool IsAllowedPageForNonAdmin(string pageName)
        {
            // Define which pages non-admin users can access
            var allowedPages = new[] { "Dashboard", "POS", "Inventory" };
            return allowedPages.Contains(pageName);
        }
        private void SidebarControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Optional: Any sidebar initialization code
        }
    }
}