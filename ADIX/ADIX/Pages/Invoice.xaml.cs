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
        private ObservableCollection<InvoiceItem> _invoiceItems;
        private string _billTo;
        private string _paymentInfo;
        private string _customerName;
        private string _invoiceDate;
        private string _invoiceNumber;
        private string _staffID;
        private string _vatNumber;
        private string _payment;
        private string _otherComments;
        private decimal _subTotal;
        private decimal _totalDiscount;
        private decimal _grandTotal;

        public InvoiceViewModel()
        {
            // Initialize with sample data so the table appears
            InvoiceItems = new ObservableCollection<InvoiceItem>
            {
                new InvoiceItem { SKU = "SKU001", Description = "Sample Item", Quantity = 1, UnitPrice = 100, Discount = 0.1m },
                new InvoiceItem { SKU = "SKU002", Description = "Another Item", Quantity = 2, UnitPrice = 50, Discount = 0m }
            };

            // Set default values
            InvoiceDate = DateTime.Now.ToString("yyyy/MM/dd");
            InvoiceNumber = "120719";

            // Subscribe to collection changes
            InvoiceItems.CollectionChanged += (s, e) => CalculateTotals();

            // Calculate initial totals
            CalculateTotals();
        }

        public ObservableCollection<InvoiceItem> InvoiceItems
        {
            get => _invoiceItems;
            set
            {
                _invoiceItems = value;
                OnPropertyChanged();
                CalculateTotals();
            }
        }

        public string BillTo
        {
            get => _billTo;
            set { _billTo = value; OnPropertyChanged(); }
        }

        public string PaymentInfo
        {
            get => _paymentInfo;
            set { _paymentInfo = value; OnPropertyChanged(); }
        }

        public string CustomerName
        {
            get => _customerName;
            set { _customerName = value; OnPropertyChanged(); }
        }

        public string InvoiceDate
        {
            get => _invoiceDate;
            set { _invoiceDate = value; OnPropertyChanged(); }
        }

        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set { _invoiceNumber = value; OnPropertyChanged(); }
        }

        public string StaffID
        {
            get => _staffID;
            set { _staffID = value; OnPropertyChanged(); }
        }

        public string VATNumber
        {
            get => _vatNumber;
            set { _vatNumber = value; OnPropertyChanged(); }
        }

        public string Payment
        {
            get => _payment;
            set { _payment = value; OnPropertyChanged(); }
        }

        public string OtherComments
        {
            get => _otherComments;
            set { _otherComments = value; OnPropertyChanged(); }
        }

        public decimal SubTotal
        {
            get => _subTotal;
            set { _subTotal = value; OnPropertyChanged(); }
        }

        public decimal TotalDiscount
        {
            get => _totalDiscount;
            set { _totalDiscount = value; OnPropertyChanged(); }
        }

        public decimal GrandTotal
        {
            get => _grandTotal;
            set { _grandTotal = value; OnPropertyChanged(); }
        }

        private void CalculateTotals()
        {
            if (InvoiceItems == null) return;

            SubTotal = InvoiceItems.Sum(item => item.UnitPrice * item.Quantity);
            TotalDiscount = InvoiceItems.Sum(item => (item.UnitPrice * item.Quantity) * item.Discount);
            GrandTotal = SubTotal - TotalDiscount;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class InvoiceItem : INotifyPropertyChanged
    {
        private string _sku;
        private string _description;
        private int _quantity;
        private decimal _unitPrice;
        private decimal _discount;

        public string SKU
        {
            get => _sku;
            set
            {
                _sku = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Total));
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Total));
            }
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                _unitPrice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Total));
            }
        }

        public decimal Discount
        {
            get => _discount;
            set
            {
                _discount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Total));
            }
        }

        public decimal Total => (UnitPrice * Quantity) * (1 - Discount);

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}