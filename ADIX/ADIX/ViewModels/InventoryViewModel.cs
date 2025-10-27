using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ADIX.Models;
using ADIX.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace ADIX.ViewModels
{
    public class InventoryViewModel : INotifyPropertyChanged
    {
        private const string ConnStr = "Data Source=ADIX.db";
        public event PropertyChangedEventHandler PropertyChanged;

        private string _productSearchText;
        public string ProductSearchText
        {
            get => _productSearchText;
            set
            {
                _productSearchText = value;
                OnPropertyChanged(nameof(ProductSearchText));
                PerformLiveSearch();
            }
        }

        public ObservableCollection<Item> FilteredProducts { get; set; }
            = new ObservableCollection<Item>();

        private bool _isAutoCompleteOpen;
        public bool IsAutoCompleteOpen
        {
            get => _isAutoCompleteOpen;
            set
            {
                _isAutoCompleteOpen = value;
                OnPropertyChanged(nameof(IsAutoCompleteOpen));
            }
        }

        private Item _selectedProduct;
        public Item SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                _selectedProduct = value;
                OnPropertyChanged(nameof(SelectedProduct));
            }
        }

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


    private List<Item> SearchProducts(string search)
        {
            double ParseDouble(object o) =>
                        o == DBNull.Value ? 0 : Convert.ToDouble(o);

            List<Item> results = new List<Item>();

            using (var conn = new SqliteConnection(ConnStr))
            {
                conn.Open();
                string query = @"SELECT description, retailPrice, stockQuantity 
                         FROM Item 
                         WHERE description LIKE @search LIMIT 10";

                using (var cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@search", "%" + search + "%");
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new Item
                            {
                                ItemName = reader["description"]?.ToString(),
                                ItemPrice = ParseDouble(reader["retailPrice"]),
                                QuantityInStock = Convert.ToInt32(reader["stockQuantity"])
                            });
                        }
                    }
                }
            }
            return results;
        }

        private async void PerformLiveSearch()
        {
            if (string.IsNullOrWhiteSpace(ProductSearchText))
            {
                FilteredProducts.Clear();
                IsAutoCompleteOpen = false;
                return;
            }

            var matches = await Task.Run(() => SearchProducts(ProductSearchText));

            FilteredProducts.Clear();
            foreach (var m in matches)
                FilteredProducts.Add(m);

            IsAutoCompleteOpen = FilteredProducts.Count > 0;
        }

        private void ProductSearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            //if (e.Key == Key.Down && AutoCompletePopup.IsOpen)
            //{
                //AutoCompleteListBox.Focus();
                //AutoCompleteListBox.SelectedIndex = 0;
            //}
        }


    }


}