using System.Windows;
using System.Windows.Controls;

namespace ADIX
{
    public partial class Sidebar : UserControl
    {
        public delegate void NavigationRequestedHandler(object sender, string pageName);
        public event NavigationRequestedHandler NavigationRequested;

        public delegate void CollapseToggledHandler(object sender, bool isCollapsed);
        public event CollapseToggledHandler CollapseToggled;

        private bool _isCollapsed = false;
        private string _currentActiveButton = "Dashboard";

        public Sidebar()
        {
            InitializeComponent();
            SetActiveButton("Dashboard");
        }

        private void SetActiveButton(string buttonName)
        {
            // Reset all buttons
            DashboardButton.Tag = null;
            POSButton.Tag = null;
            InventoryButton.Tag = null;
            ProductsButton.Tag = null;
            ItemGroupButton.Tag = null;
            SupplierButton.Tag = null;
            FinanceButton.Tag = null;
            MonthlyReportButton.Tag = null;
            SalesButton.Tag = null;
           

            // Set active button
            switch (buttonName)
            {
                case "Dashboard":
                    DashboardButton.Tag = "Active";
                    break;
                case "POS":
                    POSButton.Tag = "Active";
                    break;
                case "Inventory":
                    InventoryButton.Tag = "Active";
                    break;
                case "Products":
                    ProductsButton.Tag = "Active";
                    break;
                case "ItemGroup":
                    ItemGroupButton.Tag = "Active";
                    break;
                case "Suppliers":
                    SupplierButton.Tag = "Active";
                    break;
                case "Finance":
                    FinanceButton.Tag = "Active";
                    break;
                case "MonthlyReport":
                    MonthlyReportButton.Tag = "Active";
                    break;
                case "Sales":
                    SalesButton.Tag = "Active";
                    break;
              
            }

            _currentActiveButton = buttonName;
        }

        private void ToggleCollapse()
        {
            _isCollapsed = !_isCollapsed;

            if (_isCollapsed)
            {
                // Collapse mode - show only icons
                CollapseText.Text = "→"; // Right arrow to indicate expand
                DashboardText.Visibility = Visibility.Collapsed;
                POSText.Visibility = Visibility.Collapsed;
                InventoryText.Visibility = Visibility.Collapsed;
                ProductsText.Visibility = Visibility.Collapsed;
                ItemGroupText.Visibility = Visibility.Collapsed;
                SupplierText.Visibility = Visibility.Collapsed;
                FinanceText.Visibility = Visibility.Collapsed;
                MonthlyReportText.Visibility = Visibility.Collapsed;
                SalesText.Visibility = Visibility.Collapsed;
               
                LogoBorder.Visibility = Visibility.Collapsed;

                // Center align content when collapsed
                DashboardButton.Padding = new Thickness(0);
                POSButton.Padding = new Thickness(0);
                InventoryButton.Padding = new Thickness(0);
                ProductsButton.Padding = new Thickness(0);
                ItemGroupButton.Padding = new Thickness(0);
                SupplierButton.Padding = new Thickness(0);
                FinanceButton.Padding = new Thickness(0);
                MonthlyReportButton.Padding = new Thickness(0);
                SalesButton.Padding = new Thickness(0);
            

                DashboardButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                POSButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                InventoryButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                ProductsButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                ItemGroupButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                SupplierButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                FinanceButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                MonthlyReportButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                SalesButton.HorizontalContentAlignment = HorizontalAlignment.Center;
             

                // Set tooltips for collapsed mode
                ToolTipService.SetToolTip(DashboardButton, "Dashboard");
                ToolTipService.SetToolTip(POSButton, "POS");
                ToolTipService.SetToolTip(InventoryButton, "Inventory");
                ToolTipService.SetToolTip(ProductsButton, "Products");
                ToolTipService.SetToolTip(ItemGroupButton, "ItemGroup");
                ToolTipService.SetToolTip(SupplierButton, "Supplier");
                ToolTipService.SetToolTip(FinanceButton, "Finance");
                ToolTipService.SetToolTip(MonthlyReportButton, "MonthlyReport");
                ToolTipService.SetToolTip(SalesButton, "Sales");
               
            }
            else
            {
                // Expand mode - show text and icons
                CollapseText.Text = "←"; // Left arrow to indicate collapse
                DashboardText.Visibility = Visibility.Visible;
                POSText.Visibility = Visibility.Visible;
                InventoryText.Visibility = Visibility.Visible;
                ProductsText.Visibility = Visibility.Visible;
                ItemGroupText.Visibility = Visibility.Visible;
                SupplierText.Visibility = Visibility.Visible;
                FinanceText.Visibility = Visibility.Visible;
                MonthlyReportText.Visibility = Visibility.Visible;
                SalesText.Visibility = Visibility.Visible;
            
                LogoBorder.Visibility = Visibility.Visible;

                // Reset to left alignment
                DashboardButton.Padding = new Thickness(15, 0, 0, 0);
                POSButton.Padding = new Thickness(15, 0, 0, 0);
                InventoryButton.Padding = new Thickness(15, 0, 0, 0);
                ProductsButton.Padding = new Thickness(15, 0, 0, 0);
                ItemGroupButton.Padding = new Thickness(15, 0, 0, 0);
                SupplierButton.Padding = new Thickness(15, 0, 0, 0);
                FinanceButton.Padding = new Thickness(15, 0, 0, 0);
                MonthlyReportButton.Padding = new Thickness(15, 0, 0, 0);
                SalesButton.Padding = new Thickness(15, 0, 0, 0);
             

                DashboardButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                POSButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                InventoryButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                ProductsButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                ItemGroupButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                SupplierButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                FinanceButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                MonthlyReportButton.HorizontalContentAlignment = HorizontalAlignment.Left;
                SalesButton.HorizontalContentAlignment = HorizontalAlignment.Left;
              

                // Clear tooltips
                ToolTipService.SetToolTip(DashboardButton, null);
                ToolTipService.SetToolTip(POSButton, null);
                ToolTipService.SetToolTip(InventoryButton, null);
                ToolTipService.SetToolTip(ProductsButton, null);
                ToolTipService.SetToolTip(ItemGroupButton, null);
                ToolTipService.SetToolTip(SupplierButton, null);
                ToolTipService.SetToolTip(FinanceButton, null);
                ToolTipService.SetToolTip(MonthlyReportButton, null);
                ToolTipService.SetToolTip(SalesButton, null);
             
            }

            CollapseToggled?.Invoke(this, _isCollapsed);
        }

        // Navigation methods
        private void Dashboard_button_click(object sender, RoutedEventArgs e)
        {
            SetActiveButton("Dashboard");
            NavigationRequested?.Invoke(this, "Dashboard");
        }

        private void Pointofsale_button_click(object sender, RoutedEventArgs e)
        {
            SetActiveButton("POS");
            NavigationRequested?.Invoke(this, "POS");
        }

        private void Inventory_button_click(object sender, RoutedEventArgs e)
        {
            SetActiveButton("Inventory");
            NavigationRequested?.Invoke(this, "Inventory");
        }

        private void Products_button_click(object sender, RoutedEventArgs e)
        {
            SetActiveButton("Products");
            NavigationRequested?.Invoke(this, "Products");
        }

        private void ItemGroup_button_click(object sender, RoutedEventArgs e)
        {
            SetActiveButton("ItemGroup");
            NavigationRequested?.Invoke(this, "ItemGroup");
        }

        private void Supplier_button_click(object sender, RoutedEventArgs e)
        {
            SetActiveButton("Suppliers");
            NavigationRequested?.Invoke(this, "Suppliers");
        }

        private void Finance_button_click(object sender, RoutedEventArgs e)
        {
            SetActiveButton("Finance");
            NavigationRequested?.Invoke(this, "Finance");
        }

        private void MonthlyReport_button_click(object sender, RoutedEventArgs e)
        {
            SetActiveButton("MonthlyReport");
            NavigationRequested?.Invoke(this, "MonthlyReport");
        }

        private void Sales_button_click(object sender, RoutedEventArgs e)
        {
            SetActiveButton("Sales");
            NavigationRequested?.Invoke(this, "Sales");
        }

    

        private void CollapseButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleCollapse();
        }

        // Public method to set active page from main window
        public void SetActivePage(string pageName)
        {
            SetActiveButton(pageName);
        }
    }
}