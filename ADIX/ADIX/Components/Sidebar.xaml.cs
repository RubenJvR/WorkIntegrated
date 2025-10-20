using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ADIX
{
    public partial class Sidebar : UserControl
    {
        public event EventHandler<string> NavigationRequested;

        public Sidebar()
        {
            InitializeComponent();
        }

        private void Dashboard_button_click(object sender, RoutedEventArgs e)
        {
            NavigationRequested?.Invoke(this, "Dashboard");
        }

        private void Pointofsale_button_click(object sender, RoutedEventArgs e)
        {
            NavigationRequested?.Invoke(this, "POS");
        }

        private void Inventory_button_click(object sender, RoutedEventArgs e)
        {
            NavigationRequested?.Invoke(this, "Inventory");
        }

        private void Products_button_click(object sender, RoutedEventArgs e)
        {
            NavigationRequested?.Invoke(this, "Products");
        }

        private void Supplier_button_click(object sender, RoutedEventArgs e)
        {
            NavigationRequested?.Invoke(this, "Suppliers");
        }

        private void Finance_button_click(object sender, RoutedEventArgs e)
        {
            NavigationRequested?.Invoke(this, "Finance");
        }

        private void Sales_button_click(object sender, RoutedEventArgs e)
        {
            NavigationRequested?.Invoke(this, "Sales");
        }

        private void Setting_button_click(object sender, RoutedEventArgs e)
        {
            NavigationRequested?.Invoke(this, "Settings");
        }

        private void ItemGroup_button_click(object sender, RoutedEventArgs e)
        {
            NavigationRequested?.Invoke(this, "ItemGroup");
        }

        private void MonthlyReport_button_click(object sender, RoutedEventArgs e)
        {
            NavigationRequested?.Invoke(this, "MonthlyReport");
        }
    }
}