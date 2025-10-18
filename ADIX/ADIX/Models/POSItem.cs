using System.ComponentModel;

namespace ADIX.Models
{
    public class POSItem : INotifyPropertyChanged
    {
        private int _itemID;
        private string? _itemName;
        private int _quantity;
        private int _stockControl;
        private decimal _price;
        private decimal _totalAmount;
        private int _inStock;
        private decimal _itemDiscount;
        private decimal _discountedItemAmount;
        private decimal _totalDiscounted;

        public int ItemID
        {
            get => _itemID;
            set { _itemID = value; OnPropertyChanged(nameof(ItemID)); }
        }

        public string? ItemName
        {
            get => _itemName;
            set { _itemName = value; OnPropertyChanged(nameof(ItemName)); }
        }

        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged(nameof(Quantity));
                CalculateTotals();
            }
        }

        public int StockControl
        {
            get => _stockControl;
            set { _stockControl = value; OnPropertyChanged(nameof(StockControl)); }
        }

        public decimal Price
        {
            get => _price;
            set
            {
                _price = value;
                OnPropertyChanged(nameof(Price));
                CalculateTotals();
            }
        }

        public decimal TotalAmount
        {
            get => _totalAmount;
            private set { _totalAmount = value; OnPropertyChanged(nameof(TotalAmount)); }
        }

        public int InStock
        {
            get => _inStock;
            set { _inStock = value; OnPropertyChanged(nameof(InStock)); }
        }

        public decimal ItemDiscount
        {
            get => _itemDiscount;
            set
            {
                _itemDiscount = value;
                OnPropertyChanged(nameof(ItemDiscount));
                CalculateTotals();
            }
        }

        public decimal DiscountedItemAmount
        {
            get => _discountedItemAmount;
            private set { _discountedItemAmount = value; OnPropertyChanged(nameof(DiscountedItemAmount)); }
        }

        public decimal TotalDiscounted
        {
            get => _totalDiscounted;
            private set { _totalDiscounted = value; OnPropertyChanged(nameof(TotalDiscounted)); }
        }

        private void CalculateTotals()
        {
            TotalAmount = Quantity * Price;
            decimal discountAmount = TotalAmount * (ItemDiscount / 100);
            DiscountedItemAmount = TotalAmount - discountAmount;
            TotalDiscounted = discountAmount;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}