using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ADIX.Models;
using ADIX.Repositories;

namespace ADIX.ViewModels
{
    public class PointOfSaleViewModel : INotifyPropertyChanged
    {
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
        private int _invoiceNumber;

        public ObservableCollection<POSItem> CartItems { get; set; }
        public ObservableCollection<StaffMember> StaffMembers { get; set; }
        public ObservableCollection<string> PaymentMethods { get; set; }

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

            // Initialize commands
            CheckoutCommand = new RelayCommand(Checkout, CanCheckout);
            CancelTransactionCommand = new RelayCommand(CancelTransaction);
            CreateQuoteCommand = new RelayCommand(CreateQuote, CanCheckout);

            // Load payment methods
            PaymentMethods.Add("Cash");
            PaymentMethods.Add("EFT");
            PaymentMethods.Add("Credit");
            PaymentMethods.Add("Return");

            // Set default values
            _discountPercent = 0; // Explicitly set to 0
            _vatAmount = 15; // Set a default VAT percentage (e.g., 15%)

            // Load data
            try
            {
                LoadStaff();
                InitializeInvoice();
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

        private void LoadAvailableItems()
        {
            try
            {
                var items = _repository.GetAllItems();

                // Add available items to cart with quantity 0
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
            set
            {
                // Ensure value is valid
                decimal validValue = value;
                if (validValue < 0) validValue = 0;
                if (validValue > 30) validValue = 30; // Max 30% VAT

                if (_vatAmount != validValue)
                {
                    _vatAmount = validValue;
                    OnPropertyChanged(nameof(VATAmount));
                    ValidateAndCalculateTotals();
                }
            }
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
                // Ensure value is valid
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

        public int InvoiceNumber
        {
            get => _invoiceNumber;
            set { _invoiceNumber = value; OnPropertyChanged(nameof(InvoiceNumber)); }
        }

        private void CalculateTotals()
        {
            if (CartItems == null) return;

            decimal subtotal = 0;
            decimal totalItemDiscounts = 0;

            // Calculate subtotal and individual item discounts
            foreach (var item in CartItems.Where(i => i.Quantity > 0))
            {
                decimal itemTotal = item.Quantity * item.Price;
                subtotal += itemTotal;
                totalItemDiscounts += itemTotal * (item.ItemDiscount / 100m);
            }

            // Update TotalExcludingDiscount (this is the subtotal before any discounts)
            TotalExcludingDiscount = subtotal;

            // Calculate overall discount amount
            decimal overallDiscountAmount = subtotal * (DiscountPercent / 100m);

            // Calculate total after ALL discounts (both item-level and overall)
            decimal totalAfterAllDiscounts = subtotal - totalItemDiscounts - overallDiscountAmount;

            // Apply VAT
            decimal vatAmount = totalAfterAllDiscounts * (VATAmount / 100m);

            // Final total
            TotalBill = totalAfterAllDiscounts + vatAmount;

            // Debug output (remove in production)
            System.Diagnostics.Debug.WriteLine($"Subtotal: {subtotal}, Item Discounts: {totalItemDiscounts}, Overall Discount: {overallDiscountAmount}, VAT: {vatAmount}, Total: {TotalBill}");
        }

        private void ValidateNumericInputs()
        {
            // Ensure VAT is reasonable
            if (VATAmount < 0) VATAmount = 0;
            if (VATAmount > 30) VATAmount = 30; // Assuming max 30% VAT

            // Ensure discount is reasonable
            if (DiscountPercent < 0) DiscountPercent = 0;
            if (DiscountPercent > 100) DiscountPercent = 100; // Max 100% discount
        }

        private bool CanCheckout(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(CustomerName) &&
                   SelectedStaff != null &&
                   !string.IsNullOrWhiteSpace(SelectedPaymentMethod) &&
                   CartItems.Any(i => i.Quantity > 0);
        }

        private void Checkout(object? parameter)
        {
            try
            {
                // Validate stock
                foreach (var item in CartItems.Where(i => i.Quantity > 0))
                {
                    if (item.Quantity > item.InStock)
                    {
                        MessageBox.Show($"Insufficient stock for {item.ItemName}. Available: {item.InStock}",
                            "Stock Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Create invoice (type = 1 for sale)
                int invoiceId = _repository.CreateInvoice(
                    CustomerName ?? "",
                    SelectedStaff?.StaffID ?? 0,
                    SelectedPaymentMethod ?? "",
                    PaymentReceived,
                    VATAmount,
                    Address ?? "",
                    1, // Type 1 = Sale
                    TotalBill
                );

                // Add items to invoice
                var itemsToAdd = CartItems.Where(i => i.Quantity > 0).ToList();
                _repository.AddInvoiceItems(invoiceId, itemsToAdd);

                MessageBox.Show($"Sale completed successfully!\nInvoice #: {invoiceId}\nTotal: R {TotalBill:F2}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Reset form
                CancelTransaction(null);
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
                // Create quote (type = 2 for quote)
                int quoteId = _repository.CreateInvoice(
                    CustomerName ?? "",
                    SelectedStaff?.StaffID ?? 0,
                    SelectedPaymentMethod ?? "",
                    false,
                    VATAmount,
                    Address ?? "",
                    2, // Type 2 = Quote
                    TotalBill
                );

                var itemsToAdd = CartItems.Where(i => i.Quantity > 0).ToList();
                _repository.AddInvoiceItems(quoteId, itemsToAdd);

                MessageBox.Show($"Quote created successfully!\nQuote #: {quoteId}\nTotal: R {TotalBill:F2}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                CancelTransaction(null);
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
                CustomerName = string.Empty;
                SelectedStaff = null;
                SelectedPaymentMethod = null;
                PaymentReceived = false;
                VATAmount = 0;
                Address = string.Empty;
                DiscountPercent = 0;

                // Reset cart quantities
                foreach (var item in CartItems)
                {
                    item.Quantity = 0;
                    item.ItemDiscount = 0;
                }

                InitializeInvoice();
            }
        }

        private void ValidateAndCalculateTotals()
        {
            // Validate DiscountPercent
            if (_discountPercent < 0) _discountPercent = 0;
            if (_discountPercent > 100) _discountPercent = 100;

            // Validate VATAmount
            if (_vatAmount < 0) _vatAmount = 0;
            if (_vatAmount > 30) _vatAmount = 30; // Assuming max 30% VAT

            CalculateTotals();
        }

        // Method to get cart items for Quote/Invoice
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

    // Simple RelayCommand implementation with nullable parameters
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