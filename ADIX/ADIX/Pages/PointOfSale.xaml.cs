using System;
using System.Windows.Controls;

namespace ADIX
{
    public partial class PointOfSale : Page
    {
        public PointOfSale()
        {
            InitializeComponent();

            // Auto-fill Date
            DateText.Text = DateTime.Now.ToString("dd/MM/yyyy");

            // Auto-generate Invoice Number (example)
            InvoiceText.Text = "INV-" + DateTime.Now.Ticks.ToString().Substring(10);
        }
    }
}
