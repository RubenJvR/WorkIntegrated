using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;

namespace ADIX.Pages
{
    public partial class Staff : Page
    {
        private const string ConnStr = "Data Source=ADIX.db";
        private ObservableCollection<StaffMember> StaffList = new ObservableCollection<StaffMember>();

        public Staff()
        {
            InitializeComponent();
            LoadStaffAsync();
        }

        public class StaffMember
        {
            public int StaffID { get; set; }
            public string Name { get; set; }
            public string Role { get; set; }
            public string Username { get; set; }
            public string PasswordHash { get; set; }
            public decimal Salary { get; set; }
        }

        private async void LoadStaffAsync()
        {
            try
            {
                using var conn = new SqliteConnection(ConnStr);
                await conn.OpenAsync();

                string query = @"
                    SELECT 
                        staffID,
                        name,
                        Role,
                        userName,
                        passwordHash,
                        salary
                    FROM STAFF
                    ORDER BY name;";

                using var cmd = new SqliteCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                StaffList.Clear();

                while (await reader.ReadAsync())
                {
                    var staff = new StaffMember
                    {
                        StaffID = Convert.ToInt32(reader["staffID"]),
                        Name = reader["name"]?.ToString() ?? "Unknown",
                        Role = reader["Role"]?.ToString() ?? "Staff",
                        Username = reader["userName"]?.ToString() ?? "",
                        PasswordHash = reader["passwordHash"]?.ToString() ?? "",
                        Salary = reader["salary"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["salary"])
                    };

                    StaffList.Add(staff);
                }

                StaffGrid.ItemsSource = StaffList;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading staff: {ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void StaffSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = StaffSearchTextBox.Text.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                StaffGrid.ItemsSource = StaffList;
                return;
            }

            var filteredList = StaffList.Where(staff =>
                staff.Name.ToLower().Contains(searchText) ||
                staff.Role.ToLower().Contains(searchText) ||
                staff.Username.ToLower().Contains(searchText)
            ).ToList();

            StaffGrid.ItemsSource = filteredList;
        }

        private void StaffGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                // Schedule the update to run AFTER the cell edit is committed
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var editedStaff = e.Row.DataContext as StaffMember;
                    if (editedStaff != null)
                    {
                        UpdateStaffInDatabase(editedStaff);
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void UpdateStaffInDatabase(StaffMember staff)
        {
            try
            {
                using var conn = new SqliteConnection(ConnStr);
                conn.Open();

                string updateSql = @"
            UPDATE STAFF 
            SET name = @name,
                Role = @role,
                userName = @username,
                passwordHash = @passwordHash,
                salary = @salary,
                lastModified = CURRENT_TIMESTAMP
            WHERE staffID = @staffID";

                using var cmd = new SqliteCommand(updateSql, conn);
                cmd.Parameters.AddWithValue("@name", staff.Name);
                cmd.Parameters.AddWithValue("@role", staff.Role ?? "Staff");
                cmd.Parameters.AddWithValue("@username", staff.Username ?? "");
                cmd.Parameters.AddWithValue("@passwordHash", staff.PasswordHash ?? "");

                // Ensure salary is converted to double for SQLite REAL type
                cmd.Parameters.AddWithValue("@salary", Convert.ToDouble(staff.Salary));

                cmd.Parameters.AddWithValue("@staffID", staff.StaffID);

                int rowsAffected = cmd.ExecuteNonQuery();
              
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating staff: {ex.Message}", "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void AddNewStaff_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Generate a new staff ID
                int newStaffID = GenerateNewStaffID();

                var newStaff = new StaffMember
                {
                    StaffID = newStaffID,
                    Name = "New Staff Member",
                    Role = "Staff",
                    Username = "newuser",
                    PasswordHash = "defaultpassword",
                    Salary = 0
                };

                // Add to database
                InsertStaffIntoDatabase(newStaff);

                // Refresh the list
                LoadStaffAsync();

                MessageBox.Show("New staff member added. Please edit the details.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding new staff: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int GenerateNewStaffID()
        {
            using var conn = new SqliteConnection(ConnStr);
            conn.Open();

            string query = "SELECT COALESCE(MAX(staffID), 0) + 1 FROM STAFF";
            using var cmd = new SqliteCommand(query, conn);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private void InsertStaffIntoDatabase(StaffMember staff)
        {
            using var conn = new SqliteConnection(ConnStr);
            conn.Open();

            string insertSql = @"
                INSERT INTO STAFF 
                (staffID, name, Role, userName, passwordHash, salary, lastModified)
                VALUES 
                (@staffID, @name, @role, @username, @passwordHash, @salary, CURRENT_TIMESTAMP)";

            using var cmd = new SqliteCommand(insertSql, conn);
            cmd.Parameters.AddWithValue("@staffID", staff.StaffID);
            cmd.Parameters.AddWithValue("@name", staff.Name);
            cmd.Parameters.AddWithValue("@role", staff.Role);
            cmd.Parameters.AddWithValue("@username", staff.Username);
            cmd.Parameters.AddWithValue("@passwordHash", staff.PasswordHash);
            cmd.Parameters.AddWithValue("@salary", staff.Salary);

            cmd.ExecuteNonQuery();

            // Mark sync as required
            Database.MarkSyncRequired();
        }

        

        

        private async void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            // Force save any pending edits
            StaffGrid.CommitEdit(DataGridEditingUnit.Row, true);
            await Database.CheckAndSyncAsync();
            MessageBox.Show("All changes have been saved.", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
       
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadStaffAsync();
            StaffSearchTextBox.Clear();
        }
    }
}