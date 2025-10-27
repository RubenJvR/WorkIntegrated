using ADIX.Models;
using ADIX.Repositories;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ADIX.ViewModels
{
    public class PointOfSaleViewModel : INotifyPropertyChanged
    {
        // Refund command
        public ICommand RefundCommand { get; }

        private readonly POSRepository _repository;
        private string? _customerName;
        private StaffMember? _selectedStaff;
        private string? _selectedPaymentMethod;
        private bool _paymentReceived;
        private decimal _vatAmount;
        private string? _address;
        private decimal _discountPercent;
        private decimal _totalBill;
        private decimal _totalExcludingDiscount;
        private string? _currentDate;
        private long _invoiceNumber;

        // ========== AUTOCOMPLETE PROPERTIES ==========
        private string? _productSearchText;
        private bool _isAutoCompleteOpen;
        private ObservableCollection<POSItem> _filteredProducts;
        private System.Collections.Generic.List<POSItem> _allProducts;

        public string? ProductSearchText
        {
            get => _productSearchText;
            set
            {
                _productSearchText = value;
                OnPropertyChanged(nameof(ProductSearchText));
                FilterProducts();
            }
        }

        public bool IsAutoCompleteOpen
        {
            get => _isAutoCompleteOpen;
            set
            {
                _isAutoCompleteOpen = value;
                OnPropertyChanged(nameof(IsAutoCompleteOpen));
            }
        }

        public ObservableCollection<POSItem> FilteredProducts
        {
            get => _filteredProducts;
            set
            {
                _filteredProducts = value;
                OnPropertyChanged(nameof(FilteredProducts));
            }
        }
        // ========== END AUTOCOMPLETE PROPERTIES ==========

        public ObservableCollection<POSItem> CartItems { get; set; }
        public ObservableCollection<StaffMember> StaffMembers { get; set; }
        public ObservableCollection<string> PaymentMethods { get; set; }

        // Property to show only cart items with quantity > 0
        public ObservableCollection<POSItem> CartItemsWithQuantity
        {
            get
            {
                var itemsWithQuantity = new ObservableCollection<POSItem>(
                    CartItems.Where(i => i.Quantity > 0).ToList()
                );
                return itemsWithQuantity;
            }
        }

        // Property to check if cart has items
        public bool HasItemsInCart
        {
            get => CartItems.Any(i => i.Quantity > 0);
        }

        public ICommand CheckoutCommand { get; }
        public ICommand CancelTransactionCommand { get; }
        public ICommand CreateQuoteCommand { get; }

        public PointOfSaleViewModel()
        {
            _repository = new POSRepository();

            // Initialize collections first
            CartItems = new ObservableCollection<POSItem>();
            StaffMembers = new ObservableCollection<StaffMember>();
            PaymentMethods = new ObservableCollection<string>();
            FilteredProducts = new ObservableCollection<POSItem>();
            _allProducts = new System.Collections.Generic.List<POSItem>();

            // Initialize commands
            CheckoutCommand = new RelayCommand(Checkout, CanCheckout);
            CancelTransactionCommand = new RelayCommand(CancelTransaction);
            CreateQuoteCommand = new RelayCommand(CreateQuote, CanCheckout);
            RefundCommand = new RelayCommand(ProcessRefund, CanProcessRefund);

            // Load payment methods
            PaymentMethods.Add("Cash");
            PaymentMethods.Add("EFT");
            PaymentMethods.Add("Credit Card");
            PaymentMethods.Add("Debit Card");
            PaymentMethods.Add("Return");

            // Set default values
            _discountPercent = 0;
            _vatAmount = 15m;

            // Load data
            try
            {
                LoadStaff();
                InitializeInvoice();
                _repository.RefreshItemsFromSync();
                LoadAvailableItems();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization error: {ex.Message}\n\nMake sure Database.Initialize() is called before creating this view.",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Subscribe to cart item changes
            CartItems.CollectionChanged += (s, e) => CalculateTotals();
        }

        // ========== AUTOCOMPLETE METHODS ==========
        private void FilterProducts()
        {
            if (string.IsNullOrWhiteSpace(ProductSearchText))
            {
                IsAutoCompleteOpen = false;
                FilteredProducts.Clear();
                return;
            }

            string searchText = ProductSearchText;

            var filtered = _allProducts
                .Where(p =>
                {
                    if (p.ItemName != null)
                    {
                        string itemName = p.ItemName;
                        if (itemName.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }

                    // StockControl is an int, so convert to string for searching
                    string stockControlStr = p.StockControl.ToString();
                    if (stockControlStr.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;

                    return false;
                })
                .OrderBy(p =>
                {
                    if (p.ItemName == null) return int.MaxValue;
                    string itemName = p.ItemName;
                    int index = itemName.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase);
                    return index >= 0 ? index : int.MaxValue;
                })
                .ThenBy(p => p.ItemName)
                .Take(20)
                .ToList();

            FilteredProducts.Clear();
            foreach (var item in filtered)
            {
                FilteredProducts.Add(item);
            }

            IsAutoCompleteOpen = filtered.Count > 0;
        }

        public void AddProductToCart(POSItem product)
        {
            if (product == null) return;

            // Check if item already exists in cart (matching by ItemID for accuracy)
            var existingItem = CartItems.FirstOrDefault(item => item.ItemID == product.ItemID);

            if (existingItem != null)
            {
                // Increment quantity if item exists
                existingItem.Quantity += 1;
            }
            else
            {
                // This shouldn't happen since we load all items, but handle it anyway
                // Create a new cart item
                var newCartItem = new POSItem
                {
                    ItemID = product.ItemID,
                    ItemName = product.ItemName,
                    Quantity = 1,
                    StockControl = product.StockControl,
                    Price = product.Price,
                    InStock = product.InStock,
                    ItemDiscount = 0
                };

                newCartItem.PropertyChanged += CartItem_PropertyChanged;
                CartItems.Add(newCartItem);
            }

            // Recalculate totals
            CalculateTotals();
        }
        // ========== END AUTOCOMPLETE METHODS ==========

        // Method to check if refund can be processed
        private bool CanProcessRefund(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(CustomerName) &&
                   SelectedStaff != null &&
                   SelectedPaymentMethod == "Return" &&
                   CartItems.Any(i => i.Quantity > 0);
        }

        private void ProcessRefund(object? parameter)
        {
            try
            {
                if (SelectedPaymentMethod != "Return")
                {
                    MessageBox.Show("Please set Payment Method to 'Return' for refund processing.",
                        "Refund Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                foreach (var item in CartItems.Where(i => i.Quantity > 0))
                {
                    if (item.Quantity <= 0)
                    {
                        MessageBox.Show($"Invalid quantity for {item.ItemName}. Refund quantity must be positive.",
                            "Refund Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                long refundId = _repository.CreateRefund(
                    CustomerName ?? "",
                    SelectedStaff?.StaffID ?? 0,
                    VATAmount,
                    Address ?? "",
                    TotalBill
                );

                var itemsToRefund = CartItems.Where(i => i.Quantity > 0).ToList();
                _repository.AddRefundItems(refundId, itemsToRefund);

                MessageBox.Show($"Refund processed successfully!\nRefund #: {refundId}\nRefund Amount: R {Math.Abs(TotalBill):F2}",
                    "Refund Success", MessageBoxButton.OK, MessageBoxImage.Information);

                ResetTransaction();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing refund: {ex.Message}", "Refund Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadStaff()
        {
            try
            {
                var staff = _repository.GetAllStaff();
                StaffMembers.Clear();
                foreach (var member in staff)
                {
                    StaffMembers.Add(member);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading staff: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void RefreshItems()
        {
            try
            {
                // Clear existing items
                CartItems.Clear();

                // Reload all items from database (including newly synced ones)
                LoadAvailableItems();

                // Recalculate totals
                CalculateTotals();

                OnPropertyChanged(nameof(CartItems));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing items: {ex.Message}", "Refresh Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadAvailableItems()
        {
            try
            {
                var items = _repository.GetAllItems();

                // Load all products for autocomplete search
                _allProducts.Clear();
                foreach (var item in items)
                {
                    _allProducts.Add(item);
                }

                // Add ALL items to master list with quantity 0 (only items with Qty > 0 will display)
                foreach (var item in items)
                {
                    item.PropertyChanged += CartItem_PropertyChanged;
                    CartItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading items: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CartItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(POSItem.Quantity) ||
                e.PropertyName == nameof(POSItem.ItemDiscount))
            {
                CalculateTotals();
            }
        }

        private void InitializeInvoice()
        {
            CurrentDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            InvoiceNumber = _repository.GetNextInvoiceNumber();
        }

        private void ResetTransaction()
        {
            CustomerName = string.Empty;
            SelectedStaff = null;
            SelectedPaymentMethod = null;
            PaymentReceived = false;
            Address = string.Empty;
            DiscountPercent = 0;
            ProductSearchText = string.Empty; // Clear search box

            foreach (var item in CartItems)
            {
                item.Quantity = 0;
                item.ItemDiscount = 0;
            }

            // Refresh stock quantities to reflect changes
            RefreshStockQuantities();
            InitializeInvoice();
        }

        // Method to refresh stock quantities without resetting the cart
        private void RefreshStockQuantities()
        {
            try
            {
                foreach (var cartItem in CartItems)
                {
                    var currentItem = _repository.GetItemById(cartItem.ItemID);
                    if (currentItem != null)
                    {
                        cartItem.InStock = currentItem.InStock;
                        cartItem.StockControl = currentItem.StockControl;
                    }
                }

                // Also update _allProducts for search
                for (int i = 0; i < _allProducts.Count; i++)
                {
                    var currentItem = _repository.GetItemById(_allProducts[i].ItemID);
                    if (currentItem != null)
                    {
                        _allProducts[i].InStock = currentItem.InStock;
                        _allProducts[i].StockControl = currentItem.StockControl;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing stock: {ex.Message}");
            }
        }

        public string? CustomerName
        {
            get => _customerName;
            set { _customerName = value; OnPropertyChanged(nameof(CustomerName)); }
        }

        public StaffMember? SelectedStaff
        {
            get => _selectedStaff;
            set { _selectedStaff = value; OnPropertyChanged(nameof(SelectedStaff)); }
        }

        public string? SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set { _selectedPaymentMethod = value; OnPropertyChanged(nameof(SelectedPaymentMethod)); }
        }

        public bool PaymentReceived
        {
            get => _paymentReceived;
            set { _paymentReceived = value; OnPropertyChanged(nameof(PaymentReceived)); }
        }

        public decimal VATAmount
        {
            get => _vatAmount;
            private set
            {
                _vatAmount = 15m;
                OnPropertyChanged(nameof(VATAmount));
                CalculateTotals();
            }
        }

        private const decimal MAX_TOTAL_DISCOUNT_PERCENT = 50m;

        private bool ValidateDiscounts()
        {
            decimal totalEffectiveDiscount = 0;

            foreach (var item in CartItems.Where(i => i.Quantity > 0))
            {
                // Calculate effective discount for this item
                decimal itemDiscountPercent = item.ItemDiscount + (DiscountPercent * (1 - item.ItemDiscount / 100m));
                totalEffectiveDiscount = Math.Max(totalEffectiveDiscount, itemDiscountPercent);

                // Check if any single item has excessive discount
                if (itemDiscountPercent > MAX_TOTAL_DISCOUNT_PERCENT)
                {
                    MessageBox.Show($"Discount for {item.ItemName} exceeds maximum allowed ({MAX_TOTAL_DISCOUNT_PERCENT}%).\n" +
                                  $"Item Discount: {item.ItemDiscount}% + Overall Discount: {DiscountPercent}% = {itemDiscountPercent:F1}% total",
                                  "Excessive Discount", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        public string? Address
        {
            get => _address;
            set { _address = value; OnPropertyChanged(nameof(Address)); }
        }

        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                decimal validValue = value;
                if (validValue < 0) validValue = 0;
                if (validValue > 100) validValue = 100;

                if (_discountPercent != validValue)
                {
                    _discountPercent = validValue;
                    OnPropertyChanged(nameof(DiscountPercent));
                    ValidateAndCalculateTotals();
                }
            }
        }

        public decimal TotalBill
        {
            get => _totalBill;
            set { _totalBill = value; OnPropertyChanged(nameof(TotalBill)); }
        }

        public decimal TotalExcludingDiscount
        {
            get => _totalExcludingDiscount;
            set { _totalExcludingDiscount = value; OnPropertyChanged(nameof(TotalExcludingDiscount)); }
        }

        public string? CurrentDate
        {
            get => _currentDate;
            set { _currentDate = value; OnPropertyChanged(nameof(CurrentDate)); }
        }

        public long InvoiceNumber
        {
            get => _invoiceNumber;
            set { _invoiceNumber = value; OnPropertyChanged(nameof(InvoiceNumber)); }
        }

        private void CalculateTotals()
        {
            if (CartItems == null) return;

            decimal subtotal = 0;
            decimal totalItemDiscounts = 0;

            foreach (var item in CartItems.Where(i => i.Quantity > 0))
            {
                decimal itemTotal = item.Quantity * item.Price;
                subtotal += itemTotal;

                decimal itemDiscountAmount = itemTotal * (item.ItemDiscount / 100m);
                totalItemDiscounts += itemDiscountAmount;
            }

            TotalExcludingDiscount = subtotal;

            decimal amountAfterItemDiscounts = subtotal - totalItemDiscounts;
            decimal overallDiscountAmount = amountAfterItemDiscounts * (DiscountPercent / 100m);

            decimal totalAfterAllDiscounts = amountAfterItemDiscounts - overallDiscountAmount;

            decimal vatAmount = totalAfterAllDiscounts * (VATAmount / 100m);

            TotalBill = totalAfterAllDiscounts + vatAmount;

            System.Diagnostics.Debug.WriteLine($"Subtotal: {subtotal}, Item Discounts: {totalItemDiscounts}, " +
                                             $"Overall Discount: {overallDiscountAmount}, VAT: {vatAmount}, Total: {TotalBill}");
        }

        private void ValidateNumericInputs()
        {
            if (VATAmount < 0) VATAmount = 0;
            if (VATAmount > 30) VATAmount = 30;

            if (DiscountPercent < 0) DiscountPercent = 0;
            if (DiscountPercent > 100) DiscountPercent = 100;
        }

        private bool CanCheckout(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(CustomerName) &&
                   SelectedStaff != null &&
                   !string.IsNullOrWhiteSpace(SelectedPaymentMethod) &&
                   CartItems.Any(i => i.Quantity > 0);
        }

        public void ReloadItemsFromDatabase()
        {
            try
            {
                CartItems.Clear();
                _allProducts.Clear();

                var items = _repository.GetAllItems();

                foreach (var item in items)
                {
                    // Add to all products for search
                    _allProducts.Add(item);

                    // Add to cart items for display
                    item.PropertyChanged += CartItem_PropertyChanged;
                    CartItems.Add(item);
                }

                InitializeInvoice();
                CalculateTotals();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reloading POS items: {ex.Message}",
                    "Reload Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Checkout(object? parameter)
        {
            try
            {
                if (!ValidateDiscounts())
                    return;

                foreach (var item in CartItems.Where(i => i.Quantity > 0))
                {
                    if (item.Quantity > item.InStock)
                    {
                        MessageBox.Show($"Insufficient stock for {item.ItemName}. Available: {item.InStock}",
                            "Stock Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                long invoiceId = _repository.CreateInvoice(
                    CustomerName ?? "",
                    SelectedStaff?.StaffID ?? 0,
                    SelectedPaymentMethod ?? "",
                    PaymentReceived,
                    VATAmount,
                    Address ?? "",
                    1,
                    TotalBill
                );

                var itemsToAdd = CartItems.Where(i => i.Quantity > 0).ToList();
                _repository.AddInvoiceItems(invoiceId, itemsToAdd);

                MessageBox.Show($"Sale completed successfully!\nInvoice #: {invoiceId}\nTotal: R {TotalBill:F2}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                ResetTransaction();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing checkout: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateQuote(object? parameter)
        {
            try
            {
                long quoteId = _repository.CreateInvoice(
                    CustomerName ?? "",
                    SelectedStaff?.StaffID ?? 0,
                    SelectedPaymentMethod ?? "",
                    false,
                    VATAmount,
                    Address ?? "",
                    2,
                    TotalBill
                );

                var itemsToAdd = CartItems.Where(i => i.Quantity > 0).ToList();
                _repository.AddInvoiceItems(quoteId, itemsToAdd);

                MessageBox.Show($"Quote created successfully!\nQuote #: {quoteId}\nTotal: R {TotalBill:F2}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                ResetTransaction();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating quote: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelTransaction(object? parameter)
        {
            var result = MessageBox.Show("Are you sure you want to cancel this transaction?",
                "Confirm Cancel", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ResetTransaction();
            }
        }

        private void ValidateAndCalculateTotals()
        {
            if (_discountPercent < 0) _discountPercent = 0;
            if (_discountPercent > 100) _discountPercent = 100;

            if (_vatAmount < 0) _vatAmount = 0;
            if (_vatAmount > 30) _vatAmount = 30;

            CalculateTotals();
        }

        public System.Collections.Generic.List<POSItem> GetCartItemsForExport()
        {
            return CartItems.Where(item => item.Quantity > 0).ToList();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object? parameter) => _execute(parameter);
    }
}