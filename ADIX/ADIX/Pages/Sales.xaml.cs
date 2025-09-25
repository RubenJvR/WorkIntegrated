using System;
using System.Collections.Generic;
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
    /// Interaction logic for Sales.xaml
    /// </summary>
    public partial class Sales : Page
    {
        public Sales()
        {
            InitializeComponent();
        }

        private void SalesDate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if (SalesDate.SelectedItem is ComboBoxItem selected)
            {
                string choice = selected.Content.ToString();
                if (choice == "Custom")
                {
                    CustomDatePanel.Visibility = Visibility.Visible;
                }
                else
                {
                    CustomDatePanel.Visibility = Visibility.Collapsed;
                }
            }
        }
    }

    
    }
