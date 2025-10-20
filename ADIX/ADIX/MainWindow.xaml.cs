using Microsoft.Data.Sqlite;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WorkIntegrated;
using System.Configuration;
using ADIX.Pages;

namespace ADIX
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Database.Initialize();

            // Subscribe to navigation events
            SidebarControl.NavigationRequested += Sidebar_NavigationRequested;

            // Navigate to default page
            MainFrame.Navigate(new Dashboard());
        }

        private void Sidebar_NavigationRequested(object sender, string pageName)
        {
            NavigateToPage(pageName);
        }

        private void NavigateToPage(string pageName)
        {
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

        private void Sidebar_Loaded(object sender, RoutedEventArgs e)
        {
            // Optional: Any sidebar initialization code
        }

        private void SidebarControl_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}