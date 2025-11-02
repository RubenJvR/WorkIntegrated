using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using ADIX.Models;

namespace ADIX.ViewModels
{
    public class InvoiceViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<InvoiceItem>? _invoiceItems;
        private string? _billTo;
        private string? _customerAddress;
        private string? _invoiceDate;
        private string? _invoiceNumber;
        private string? _staffID;
        private string? _vatNumber;
        private string? _payment;
        private string? _otherComments;
        private decimal _subTotal;
        private decimal _totalItemDiscounts;
        private decimal _overallDiscountAmount;
        private decimal _totalDiscount;
        private decimal _grandTotal;
        private decimal _overallDiscountPercent;

        public InvoiceViewModel()
        {
            InvoiceItems = new ObservableCollection<InvoiceItem>();
            InvoiceDate = DateTime.Now.ToString("yyyy-MM-dd");
            InvoiceNumber = "INV-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            InvoiceItems.CollectionChanged += (s, e) => CalculateTotals();
            CalculateTotals();
        }

  
        public InvoiceViewModel(System.Collections.Generic.List<POSItem> cartItems, string customerName, string selectedStaff, string vatAmount, string paymentMethod, string customerAddress, decimal overallDiscountPercent)
        {
            InvoiceItems = new ObservableCollection<InvoiceItem>();

            // Convert POS items to Invoice items
            foreach (var cartItem in cartItems.Where(item => item.Quantity > 0))
            {
                InvoiceItems.Add(new InvoiceItem
                {
                    SKU = cartItem.ItemID.ToString(),
                    Description = cartItem.ItemName,
                    Quantity = cartItem.Quantity,
                    UnitPrice = cartItem.Price,
                    Discount = cartItem.ItemDiscount / 100m // Convert percentage to decimal
                });
            }

            BillTo = customerName;
            StaffID = selectedStaff;
            VATNumber = vatAmount;
            Payment = paymentMethod;
            CustomerAddress = customerAddress;
            OverallDiscountPercent = overallDiscountPercent;
            InvoiceDate = DateTime.Now.ToString("yyyy-MM-dd");
            InvoiceNumber = "INV-" + DateTime.Now.ToString("yyyyMMddHHmmss");

            InvoiceItems.CollectionChanged += (s, e) => CalculateTotals();
            CalculateTotals();
        }

        public ObservableCollection<InvoiceItem>? InvoiceItems
        {
            get => _invoiceItems;
            set
            {
                _invoiceItems = value;
                OnPropertyChanged(nameof(InvoiceItems));
                CalculateTotals();
            }
        }

        public string? BillTo
        {
            get => _billTo;
            set { _billTo = value; OnPropertyChanged(nameof(BillTo)); }
        }

        public string? CustomerAddress
        {
            get => _customerAddress;
            set { _customerAddress = value; OnPropertyChanged(nameof(CustomerAddress)); }
        }

        public string? InvoiceDate
        {
            get => _invoiceDate;
            set { _invoiceDate = value; OnPropertyChanged(nameof(InvoiceDate)); }
        }

        public string? InvoiceNumber
        {
            get => _invoiceNumber;
            set { _invoiceNumber = value; OnPropertyChanged(nameof(InvoiceNumber)); }
        }

        public string? StaffID
        {
            get => _staffID;
            set { _staffID = value; OnPropertyChanged(nameof(StaffID)); }
        }

        public string? VATNumber
        {
            get => _vatNumber;
            set { _vatNumber = value; OnPropertyChanged(nameof(VATNumber)); }
        }

        public string? Payment
        {
            get => _payment;
            set { _payment = value; OnPropertyChanged(nameof(Payment)); }
        }

        public string? OtherComments
        {
            get => _otherComments;
            set { _otherComments = value; OnPropertyChanged(nameof(OtherComments)); }
        }

        public decimal SubTotal
        {
            get => _subTotal;
            set { _subTotal = value; OnPropertyChanged(nameof(SubTotal)); }
        }

        // New property for item-level discounts only
        public decimal TotalItemDiscounts
        {
            get => _totalItemDiscounts;
            set { _totalItemDiscounts = value; OnPropertyChanged(nameof(TotalItemDiscounts)); }
        }

        // New property for overall discount amount
        public decimal OverallDiscountAmount
        {
            get => _overallDiscountAmount;
            set { _overallDiscountAmount = value; OnPropertyChanged(nameof(OverallDiscountAmount)); }
        }

        // Overall discount percentage
        public decimal OverallDiscountPercent
        {
            get => _overallDiscountPercent;
            set
            {
                _overallDiscountPercent = value;
                OnPropertyChanged(nameof(OverallDiscountPercent));
                CalculateTotals();
            }
        }

        // Total discount (item discounts + overall discount)
        public decimal TotalDiscount
        {
            get => _totalDiscount;
            set { _totalDiscount = value; OnPropertyChanged(nameof(TotalDiscount)); }
        }

        public decimal GrandTotal
        {
            get => _grandTotal;
            set { _grandTotal = value; OnPropertyChanged(nameof(GrandTotal)); }
        }

        private void CalculateTotals()
        {
            if (InvoiceItems == null || !InvoiceItems.Any())
            {
                SubTotal = 0;
                TotalItemDiscounts = 0;
                OverallDiscountAmount = 0;
                TotalDiscount = 0;
                GrandTotal = 0;
                return;
            }

            // Calculate subtotal (before any discounts)
            SubTotal = InvoiceItems.Sum(item => item.UnitPrice * item.Quantity);

            // Calculate total item-level discount amount
            TotalItemDiscounts = InvoiceItems.Sum(item => (item.UnitPrice * item.Quantity) * item.Discount);

            // Calculate overall discount amount
            OverallDiscountAmount = SubTotal * (OverallDiscountPercent / 100m);

            // Calculate total discount (item discounts + overall discount)
            TotalDiscount = TotalItemDiscounts + OverallDiscountAmount;

            // Calculate grand total after ALL discounts
            GrandTotal = SubTotal - TotalDiscount;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class InvoiceItem : INotifyPropertyChanged
    {
        private string? _sku;
        private string? _description;
        private int _quantity;
        private decimal _unitPrice;
        private decimal _discount;

        public string? SKU
        {
            get => _sku;
            set
            {
                _sku = value;
                OnPropertyChanged(nameof(SKU));
                OnPropertyChanged(nameof(Total));
            }
        }

        public string? Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged(nameof(Quantity));
                OnPropertyChanged(nameof(Total));
            }
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                _unitPrice = value;
                OnPropertyChanged(nameof(UnitPrice));
                OnPropertyChanged(nameof(Total));
            }
        }

        public decimal Discount
        {
            get => _discount;
            set
            {
                _discount = value;
                OnPropertyChanged(nameof(Discount));
                OnPropertyChanged(nameof(Total));
            }
        }

        public decimal Total => (UnitPrice * Quantity) * (1 - Discount);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}