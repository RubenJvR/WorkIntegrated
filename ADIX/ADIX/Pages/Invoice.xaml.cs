using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
    public partial class Invoice : Page
    {
        public Invoice()
        {
            InitializeComponent();
            DataContext = new InvoiceViewModel();
            Loaded += OnInvoiceLoaded;
        }

        private void OnInvoiceLoaded(object sender, RoutedEventArgs e)
        {
            // Hide sidebar when this page is loaded
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                var sidebar = mainWindow.FindName("Sidebar") as Sidebar;
                if (sidebar != null)
                {
                    sidebar.Visibility = Visibility.Collapsed;
                }
            }
        }

     
    }

    // ViewModel for invoice data
    public class InvoiceViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Add your properties here (CustomerName, Address, InvoiceItems, etc.)
        public ObservableCollection<InvoiceItem> InvoiceItems { get; } = new ObservableCollection<InvoiceItem>();

        // Add other properties as needed...
    }

    public class InvoiceItem
    {
        public string SKU { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal Total => (UnitPrice * Quantity) * (1 - Discount);
    }
}
