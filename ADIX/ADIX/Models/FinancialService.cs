using ADIX.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ADIX.Models
{
    public static class FinancialService
    {
        private const string ConnectionString = "Data Source=ADIX.db";

        public static FinancialReport GenerateFinancialReport(DateTime startDate, DateTime endDate)
        {
            var report = new FinancialReport
            {
                StartDate = startDate,
                EndDate = endDate,
                ReportDate = DateTime.Now
            };

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            CalculateRevenue(report, connection, startDate, endDate);
            CalculateCOGS(report, connection, startDate, endDate);
            CalculateExpenses(report, connection, startDate, endDate);
            CalculateAccounts(report, connection, startDate, endDate);

            return report;
        }

        private static void CalculateRevenue(FinancialReport report, SqliteConnection connection, DateTime startDate, DateTime endDate)
        {
            string revenueSql = @"
                SELECT 
                    COALESCE(SUM(CASE WHEN iq.type = 1 THEN iq.totalAmount ELSE 0 END), 0) as GrossRevenue,
                    COALESCE(SUM(CASE WHEN iq.type = 1 AND ii.quantity < 0 THEN ABS(ii.quantity * ii.priceAtSale) ELSE 0 END), 0) as Returns,
                    COUNT(DISTINCT iq.invoiceQuoteID) as TransactionCount
                FROM INVOICEQUOTE iq
                LEFT JOIN INVOICEITEM ii ON iq.invoiceQuoteID = ii.invoiceQuoteID
                WHERE iq.date BETWEEN @startDate AND @endDate";

            using var cmd = new SqliteCommand(revenueSql, connection);
            cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                report.GrossRevenue = Convert.ToDecimal(reader["GrossRevenue"]);
                report.Returns = Convert.ToDecimal(reader["Returns"]);
                report.TotalTransactions = Convert.ToInt32(reader["TransactionCount"]);
            }
        }

        private static void CalculateCOGS(FinancialReport report, SqliteConnection connection, DateTime startDate, DateTime endDate)
        {
            string cogsSql = @"
                SELECT COALESCE(SUM(ii.quantity * i.costPrice), 0) as COGS
                FROM INVOICEITEM ii
                INNER JOIN INVOICEQUOTE iq ON ii.invoiceQuoteID = iq.invoiceQuoteID
                INNER JOIN ITEM i ON ii.itemID = i.itemID
                WHERE iq.type = 1 
                AND iq.date BETWEEN @startDate AND @endDate
                AND ii.quantity > 0";

            using var cmd = new SqliteCommand(cogsSql, connection);
            cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

            var result = cmd.ExecuteScalar();
            report.COGS = result != DBNull.Value ? Convert.ToDecimal(result) : 0;
        }

        private static void CalculateExpenses(FinancialReport report, SqliteConnection connection, DateTime startDate, DateTime endDate)
        {
            string expensesSql = @"
                SELECT 
                    expenseType,
                    SUM(amount) as TotalAmount
                FROM EXPENSES
                WHERE date BETWEEN @startDate AND @endDate
                AND expenseType != 'Salary Payment'
                GROUP BY expenseType";

            using var cmd = new SqliteCommand(expensesSql, connection);
            cmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string expenseType = reader["expenseType"].ToString() ?? "Unknown";
                decimal amount = Convert.ToDecimal(reader["TotalAmount"]);
                report.OperatingExpenses[expenseType] = amount;
                report.TotalOperatingExpenses += amount;
            }

            string salarySql = @"
                SELECT COALESCE(SUM(amount), 0) as SalaryExpense
                FROM EXPENSES
                WHERE expenseType = 'Salary Payment'
                AND date BETWEEN @startDate AND @endDate";

            using var salaryCmd = new SqliteCommand(salarySql, connection);
            salaryCmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
            salaryCmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

            var salaryResult = salaryCmd.ExecuteScalar();
            report.SalaryExpenses = salaryResult != DBNull.Value ? Convert.ToDecimal(salaryResult) : 0;
        }

        private static void CalculateAccounts(FinancialReport report, SqliteConnection connection, DateTime startDate, DateTime endDate)
        {
            string arSql = @"
                SELECT COALESCE(SUM(totalAmount), 0) as AccountsReceivable
                FROM INVOICEQUOTE 
                WHERE type = 1 
                AND paymentStatus != 'Paid'
                AND date BETWEEN @startDate AND @endDate";

            using var arCmd = new SqliteCommand(arSql, connection);
            arCmd.Parameters.AddWithValue("@startDate", startDate.ToString("yyyy-MM-dd"));
            arCmd.Parameters.AddWithValue("@endDate", endDate.ToString("yyyy-MM-dd"));

            var arResult = arCmd.ExecuteScalar();
            report.AccountsReceivable = arResult != DBNull.Value ? Convert.ToDecimal(arResult) : 0;

            string apSql = @"
                SELECT 
                    s.name as SupplierName,
                    SUM(i.costPrice * i.stockQuantity) as OutstandingAmount
                FROM SUPPLIER s
                INNER JOIN ITEM i ON s.supplierID = i.supplierID
                WHERE i.stockQuantity > 0
                GROUP BY s.supplierID, s.name
                HAVING OutstandingAmount > 0";

            using var apCmd = new SqliteCommand(apSql, connection);
            using var apReader = apCmd.ExecuteReader();

            while (apReader.Read())
            {
                string supplier = apReader["SupplierName"].ToString() ?? "Unknown";
                decimal amount = Convert.ToDecimal(apReader["OutstandingAmount"]);
                report.AccountsPayable[supplier] = amount;
                report.TotalAccountsPayable += amount;
            }
        }

        public static List<Expense> GetExpenses(DateTime? startDate = null, DateTime? endDate = null)
        {
            var expenses = new List<Expense>();

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string sql = @"
                SELECT 
                    expenseID, expenseType, amount, date, description, paymentMethod, lastModified
                FROM EXPENSES
                WHERE 1=1";

            if (startDate.HasValue)
                sql += " AND date >= @startDate";
            if (endDate.HasValue)
                sql += " AND date <= @endDate";

            sql += " ORDER BY date DESC, expenseID DESC";

            using var cmd = new SqliteCommand(sql, connection);

            if (startDate.HasValue)
                cmd.Parameters.AddWithValue("@startDate", startDate.Value.ToString("yyyy-MM-dd"));
            if (endDate.HasValue)
                cmd.Parameters.AddWithValue("@endDate", endDate.Value.ToString("yyyy-MM-dd"));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var expense = new Expense
                {
                    ExpenseID = Convert.ToInt32(reader["expenseID"]),
                    ExpenseType = reader["expenseType"].ToString() ?? "Unknown",
                    Amount = Convert.ToDecimal(reader["amount"]),
                    Date = DateTime.Parse(reader["date"].ToString() ?? DateTime.Today.ToString()),
                    Description = reader["description"].ToString() ?? "",
                    PaymentMethod = reader["paymentMethod"].ToString() ?? "Cash",
                    LastModified = DateTime.Parse(reader["lastModified"].ToString() ?? DateTime.Now.ToString())
                };

                expense.Status = CalculateExpenseStatus(expense.Date);
                expenses.Add(expense);
            }

            return expenses;
        }

        public static List<StaffSalary> GetStaffSalaries()
        {
            var staffSalaries = new List<StaffSalary>();

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string sql = @"
                SELECT 
                    staffID, name, Role, userName, salary, lastModified
                FROM STAFF 
                ORDER BY name";

            using var cmd = new SqliteCommand(sql, connection);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var salary = new StaffSalary
                {
                    StaffID = Convert.ToInt32(reader["staffID"]),
                    Name = reader["name"].ToString() ?? "Unknown",
                    Role = reader["Role"].ToString() ?? "Unknown",
                    Username = reader["userName"].ToString() ?? "",
                    Salary = Convert.ToDecimal(reader["salary"]),
                    LastModified = DateTime.Parse(reader["lastModified"].ToString() ?? DateTime.Now.ToString())
                };

                staffSalaries.Add(salary);
            }

            return staffSalaries;
        }

        public static List<SupplierPaymentStatus> GetSupplierPaymentStatus()
        {
            var supplierStatus = new List<SupplierPaymentStatus>();

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string sql = @"
                SELECT 
                    s.name as SupplierName,
                    SUM(i.costPrice * i.stockRecieved) as TotalStockValue,
                    COALESCE(SUM(sp.amount), 0) as TotalPaid,
                    SUM(i.costPrice * i.stockRecieved) - COALESCE(SUM(sp.amount), 0) as OutstandingAmount
                FROM SUPPLIER s
                INNER JOIN ITEM i ON s.supplierID = i.supplierID
                LEFT JOIN SUPPLIER_PAYMENT sp ON s.supplierID = sp.supplierID
                WHERE i.stockRecieved > 0
                GROUP BY s.supplierID, s.name
                HAVING OutstandingAmount > 0";

            using var cmd = new SqliteCommand(sql, connection);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var status = new SupplierPaymentStatus
                {
                    SupplierName = reader["SupplierName"].ToString() ?? "Unknown",
                    TotalStockValue = Convert.ToDecimal(reader["TotalStockValue"]),
                    TotalPaid = Convert.ToDecimal(reader["TotalPaid"]),
                    OutstandingAmount = Convert.ToDecimal(reader["OutstandingAmount"]),
                    PaymentStatus = Convert.ToDecimal(reader["OutstandingAmount"]) == 0 ? "Paid" : "Pending"
                };

                supplierStatus.Add(status);
            }

            return supplierStatus;
        }

        public static (bool success, string message) AddExpense(Expense expense)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string insertSql = @"
                INSERT INTO EXPENSES (expenseType, amount, date, description, paymentMethod, lastModified)
                VALUES (@type, @amount, @date, @desc, @paymentMethod, CURRENT_TIMESTAMP)";

            using var cmd = new SqliteCommand(insertSql, connection);
            cmd.Parameters.AddWithValue("@type", expense.ExpenseType);
            cmd.Parameters.AddWithValue("@amount", expense.Amount);
            cmd.Parameters.AddWithValue("@date", expense.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@desc", expense.Description ?? "");
            cmd.Parameters.AddWithValue("@paymentMethod", expense.PaymentMethod);

            try
            {
                cmd.ExecuteNonQuery();
                Database.MarkSyncRequired();
                return (true, "Expense added successfully");
            }
            catch (Exception ex)
            {
                return (false, $"Error adding expense: {ex.Message}");
            }
        }

        public static (bool success, string message) ProcessSalaryPayment(SalaryPayment payment, decimal currentSalary)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string description = $"Salary payment for {payment.StaffName} (Staff ID: {payment.StaffID}) - {payment.PaymentDate:MMMM yyyy}";

            string insertSql = @"
                INSERT INTO EXPENSES (expenseType, amount, date, description, paymentMethod, lastModified)
                VALUES ('Salary Payment', @amount, @date, @desc, @paymentMethod, CURRENT_TIMESTAMP)";

            using var cmd = new SqliteCommand(insertSql, connection);
            cmd.Parameters.AddWithValue("@amount", payment.Amount);
            cmd.Parameters.AddWithValue("@date", payment.PaymentDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@desc", description);
            cmd.Parameters.AddWithValue("@paymentMethod", payment.PaymentMethod);

            try
            {
                cmd.ExecuteNonQuery();

                if (Math.Abs(payment.Amount - currentSalary) > 0.01m)
                {
                    Database.UpdateStaffSalary(payment.StaffID, (double)payment.Amount);
                }

                Database.MarkSyncRequired();
                return (true, $"Salary payment processed: {payment.StaffName} - R {payment.Amount:N2}");
            }
            catch (Exception ex)
            {
                return (false, $"Error processing salary payment: {ex.Message}");
            }
        }

        private static string CalculateExpenseStatus(DateTime expenseDate)
        {
            if (expenseDate > DateTime.Today)
                return "Scheduled";
            else if (expenseDate == DateTime.Today)
                return "Due Today";
            else if (expenseDate >= DateTime.Today.AddDays(-7))
                return "Paid";
            else
                return "Processed";
        }
    }
}