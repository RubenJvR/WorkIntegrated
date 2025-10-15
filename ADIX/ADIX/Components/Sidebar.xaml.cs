
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
    /// <summary>
    /// Interaction logic for Sidebar.xaml
    /// </summary>
    public partial class Sidebar : UserControl
    {

        private bool isOpen = true;
        public Sidebar()
        {
            InitializeComponent();
        }

        private void ToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            SidebarColumn.Width = isOpen ? new GridLength(50) : new GridLength(200);
        }

        private void Dashboard_Click(object sender, RoutedEventArgs e)
        {
  
            var mainWindow =Window.GetWindow(this) as MainWindow;
            mainWindow?.MainFrame.Navigate(new Dashboard());
        }

        private void Pos_Click(object sender, RoutedEventArgs e)
        {

            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.MainFrame.Navigate(new PointOfSale());
        }
        private void Inventory_Click(object sender, RoutedEventArgs e)
        {

            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.MainFrame.Navigate(new Inventory());
        }
        private void Product_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.MainFrame.Navigate(new Products());
        }
        private void Supplier_Click(object sender, RoutedEventArgs e)
        {

            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.MainFrame.Navigate(new SupplierPage ());
        }
        private void Finance_Click(object sender, RoutedEventArgs e)
        {

            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.MainFrame.Navigate(new Finance());
        }
        private void Sales_Click(object sender, RoutedEventArgs e)
        {

            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.MainFrame.Navigate(new Sales());
        }
        private void Consignment_Click(object sender, RoutedEventArgs e)
        {

            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.MainFrame.Navigate(new Consignment());
        }



    }
}
