using ADIX.ViewModels;

namespace ADIX
{
    public static class ViewModelManager
    {
        private static PointOfSaleViewModel _pointOfSaleViewModel;
        private static readonly object _lock = new object();

        public static PointOfSaleViewModel PointOfSaleViewModel
        {
            get
            {
                lock (_lock)
                {
                    if (_pointOfSaleViewModel == null)
                    {
                        _pointOfSaleViewModel = new PointOfSaleViewModel();
                    }
                    return _pointOfSaleViewModel;
                }
            }
        }

        public static void ResetPointOfSale()
        {
            lock (_lock)
            {
                _pointOfSaleViewModel = new PointOfSaleViewModel();
            }
        }

        public static void ClearPointOfSale()
        {
            lock (_lock)
            {
                _pointOfSaleViewModel = null;
            }
        }
    }
}