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
    public partial class Login : Window
    {
        public Login()
        {
            InitializeComponent();
        }

        private void Login_button(object sender, RoutedEventArgs e)
        {
            string username = UsernameText.Text;
            string password = passwordBox.Password;

            // Add your authentication logic here
            if (Database.ValidateUser(username, password))
            {
                // Login successful - open main application
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();

                this.Close(); // Close login window
            }
            else
            {
                MessageBox.Show("Invalid username or password.");
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
            }
        }


        private void Cancel_button(object sender, RoutedEventArgs e)
        {
            UsernameText.Text = string.Empty;
            passwordBox.Password = string.Empty;

        }

      
    }
}