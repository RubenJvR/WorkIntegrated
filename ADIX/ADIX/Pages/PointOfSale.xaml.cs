using ADIX.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace ADIX
{
    public partial class PointOfSale : Page
    {
        public PointOfSale()
        {
            InitializeComponent();
            DataContext = new PointOfSaleViewModel();
        }

        private void Quote_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.MainFrame.Navigate(new Qoute());
        }

        private void Invoice_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.MainFrame.Navigate(new Invoice());
        }



    }
}