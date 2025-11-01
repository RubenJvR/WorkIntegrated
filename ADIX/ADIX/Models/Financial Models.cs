// FinancialModels.cs
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;

namespace ADIX.Models
{
    // Main financial report model
    public class FinancialReport
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime ReportDate { get; set; }

        // Revenue Section
        public decimal GrossRevenue { get; set; }
        public decimal Returns { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal QuoteAmount { get; set; }

        // Cost Section
        public decimal COGS { get; set; }
        public decimal GrossProfit { get; set; }
        public decimal GrossProfitMargin => NetRevenue > 0 ? (GrossProfit / NetRevenue) * 100 : 0;

        // Expense Section
        public Dictionary<string, decimal> OperatingExpenses { get; set; } = new Dictionary<string, decimal>();
        public decimal SalaryExpenses { get; set; }
        public decimal TotalOperatingExpenses { get; set; }
        public decimal TotalExpenses { get; set; }

        // Profit Section
        public decimal NetProfit { get; set; }
        public decimal ProfitMargin => NetRevenue > 0 ? (NetProfit / NetRevenue) * 100 : 0;

        // Accounts Section
        public decimal AccountsReceivable { get; set; }
        public Dictionary<string, decimal> AccountsPayable { get; set; } = new Dictionary<string, decimal>();
        public decimal TotalAccountsPayable { get; set; }

        // Performance Metrics
        public int TotalTransactions { get; set; }
        public decimal AverageTransactionValue => TotalTransactions > 0 ? NetRevenue / TotalTransactions : 0;

        // Chart Data
        public List<MonthlyTrend> RevenueTrend { get; set; } = new List<MonthlyTrend>();
        public List<MonthlyTrend> ProfitLossTrend { get; set; } = new List<MonthlyTrend>();
        public List<ExpenseCategory> ExpenseBreakdown { get; set; } = new List<ExpenseCategory>();
    }

    // Monthly trend data for charts
    public class MonthlyTrend
    {
        public string Period { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }

    // Expense category for breakdown
    public class ExpenseCategory
    {
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
    }

    // Expense model
    public class Expense
    {
        public int ExpenseID { get; set; }
        public string ExpenseType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = "Cash";
        public DateTime LastModified { get; set; }
        public string Status { get; set; } = "Pending";
        public string DueStatus { get; set; } = "Current";
    }

    // Staff salary model
    public class StaffSalary
    {
        public int StaffID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public decimal Salary { get; set; }
        public DateTime LastModified { get; set; }
    }

    // Salary payment model
    public class SalaryPayment
    {
        public int PaymentID { get; set; }
        public int StaffID { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = "EFT";
    }

    // Supplier payment model
    public class SupplierPaymentStatus
    {
        public string SupplierName { get; set; } = string.Empty;
        public decimal TotalStockValue { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal OutstandingAmount { get; set; }
        public string PaymentStatus { get; set; } = "Pending";
        public DateTime? LastPaymentDate { get; set; }
        public string AgingStatus { get; set; } = "Current";
    }

    // Validation result model
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
    }

    // Chart data models
    public class ChartData
    {
        public List<string> Labels { get; set; } = new List<string>();
        public List<decimal> Values { get; set; } = new List<decimal>();
        public List<string> Colors { get; set; } = new List<string>();
    }

    // Financial summary for dashboard
    public class FinancialSummary
    {
        public decimal MonthlyTurnover { get; set; }
        public decimal MonthlyExpenses { get; set; }
        public decimal NetProfit { get; set; }
        public decimal OutstandingPayments { get; set; }
        public decimal GrossProfitMargin { get; set; }
        public decimal AccountsReceivable { get; set; }
    }
}