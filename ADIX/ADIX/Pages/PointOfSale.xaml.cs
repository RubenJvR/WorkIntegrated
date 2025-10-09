using System.Windows.Controls;
using ADIX.ViewModels;

namespace ADIX
{
    public partial class PointOfSale : Page
    {
        public PointOfSale()
        {
            InitializeComponent();
            DataContext = new PointOfSaleViewModel();
        }
    }
}