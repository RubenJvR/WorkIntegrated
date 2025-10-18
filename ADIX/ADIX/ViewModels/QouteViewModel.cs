using System.ComponentModel;

namespace ADIX.ViewModels
{
    public class QouteViewModel : INotifyPropertyChanged
    {
        private string _billTo;
        private string _staffID;
        private string _vatNumber;
        private string _payment;
        private string _customerAddress;
        private string _invoiceDate;
        private string _invoiceNumber;
        private decimal _subTotal;
        private decimal _totalDiscount;
        private decimal _grandTotal;

        public string BillTo
        {
            get => _billTo;
            set { _billTo = value; OnPropertyChanged(nameof(BillTo)); }
        }

        public string StaffID
        {
            get => _staffID;
            set { _staffID = value; OnPropertyChanged(nameof(StaffID)); }
        }

        public string VATNumber
        {
            get => _vatNumber;
            set { _vatNumber = value; OnPropertyChanged(nameof(VATNumber)); }
        }

        public string Payment
        {
            get => _payment;
            set { _payment = value; OnPropertyChanged(nameof(Payment)); }
        }

        public string CustomerAddress
        {
            get => _customerAddress;
            set { _customerAddress = value; OnPropertyChanged(nameof(CustomerAddress)); }
        }

        public string InvoiceDate
        {
            get => _invoiceDate;
            set { _invoiceDate = value; OnPropertyChanged(nameof(InvoiceDate)); }
        }

        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set { _invoiceNumber = value; OnPropertyChanged(nameof(InvoiceNumber)); }
        }

        public decimal SubTotal
        {
            get => _subTotal;
            set { _subTotal = value; OnPropertyChanged(nameof(SubTotal)); }
        }

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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}