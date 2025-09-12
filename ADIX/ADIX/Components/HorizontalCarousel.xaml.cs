using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    /// <summary>
    /// Interaction logic for HorizontalCarousel.xaml
    /// </summary>
    public partial class HorizontalCarousel : UserControl
    {
        public ObservableCollection<string> Items { get; }
        public HorizontalCarousel()
        {
            InitializeComponent();
            Items = new ObservableCollection<string>();
            CarouselList.ItemsSource = Items;
        }

        public void AddItem(string item)
        {
            Items.Add(item);
        }

        public void CycleNext()
        {
            if(Items.Count > 0)
            {
                var first = Items[0];
                Items.RemoveAt(0);
                Items.Add(first);
            }
        }
    }
}
