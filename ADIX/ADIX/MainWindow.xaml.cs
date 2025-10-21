using System;
using System.Windows;
using ADIX.Pages;

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
                // Initialize database and attempt sync
                await Database.InitializeAsync();

                // Navigate to main page
                MainFrame.Navigate(new Dashboard());

                // Show status message
                if (Database.IsInternetAvailable())
                {
                    MessageBox.Show("Database initialized and synced with Azure SQL.",
                                  "Success",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Database initialized in offline mode. Data will sync when internet is available.",
                                  "Offline Mode",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database initialization failed: {ex.Message}",
                              "Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
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
                    MainFrame.Navigate(new PointOfSale());
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
                case "Settings":
                    MainFrame.Navigate(new Setting());
                    break;
            }
        }

        private void SidebarControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Optional: Any sidebar initialization code
        }
    }
}