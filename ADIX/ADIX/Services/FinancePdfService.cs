using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace ADIX.Services
{
    public class FinancePdfData
    {
        public decimal MonthlyTurnover { get; set; }
        public decimal MonthlyExpenses { get; set; }
        public decimal ProfitLoss { get; set; }
        public decimal OutstandingPayments { get; set; }
        public DataTable SupplierPayments { get; set; }
        public DataTable ExpenseBreakdown { get; set; }
        public DataTable StaffSalaries { get; set; }
        public DataTable SalaryPaymentHistory { get; set; }
        public Dictionary<string, decimal> ExpenseDistribution { get; set; }
        public Dictionary<string, decimal> TurnoverTrend { get; set; }
        public Dictionary<string, decimal> ProfitLossTrend { get; set; }
        public string ReportDate { get; set; }
        public string AppliedFilters { get; set; }
        public DateTime CurrentDate { get; set; }
    }

    public static class FinancePdfService
    {
        public static void GeneratePdf(FinancePdfData data, string filePath)
        {
            try
            {
                QuestPDF.Settings.License = LicenseType.Community;

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header()
                            .AlignCenter()
                            .Text($"ADIX Finance Report - {DateTime.Now:yyyy-MM-dd}")
                            .SemiBold().FontSize(16).FontColor(Colors.Black);

                        page.Content()
                            .PaddingVertical(1, Unit.Centimetre)
                            .Column(column =>
                            {
                                column.Spacing(15);

                                AddFinancialSummary(column, data);

                                if (!string.IsNullOrEmpty(data.AppliedFilters) && data.AppliedFilters != "Supplier: All Suppliers, Status: All Status, Date: All Dates")
                                {
                                    AddFiltersSection(column, data);
                                }

                                if (data.ExpenseDistribution != null && data.ExpenseDistribution.Any())
                                {
                                    AddExpenseDistribution(column, data);
                                }

                                if (data.SupplierPayments != null && data.SupplierPayments.Rows.Count > 0)
                                {
                                    AddSupplierPayments(column, data);
                                }

                                if (data.StaffSalaries != null && data.StaffSalaries.Rows.Count > 0)
                                {
                                    AddStaffSalaries(column, data);
                                }

                                if (data.ExpenseBreakdown != null && data.ExpenseBreakdown.Rows.Count > 0)
                                {
                                    AddExpenseBreakdown(column, data);
                                }

                                if (data.TurnoverTrend != null && data.TurnoverTrend.Any())
                                {
                                    AddTurnoverTrend(column, data);
                                }

                                if (data.ProfitLossTrend != null && data.ProfitLossTrend.Any())
                                {
                                    AddProfitLossTrend(column, data);
                                }

                                column.Item().AlignRight().Text($"Report generated on: {data.ReportDate}").FontSize(8).Italic();
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                            });
                    });
                });

                document.GeneratePdf(filePath);
            }
            catch (Exception ex)
            {
                CreateTextReportFallback(data, filePath);
            }
        }

        private static void AddFinancialSummary(ColumnDescriptor column, FinancePdfData data)
        {
            column.Item().Background(Colors.Grey.Lighten3).Padding(15).Column(summaryCol =>
            {
                summaryCol.Spacing(8);
                summaryCol.Item().Text("FINANCIAL SUMMARY").Bold().FontSize(14);

                summaryCol.Item().Row(row =>
                {
                    row.RelativeItem().Text("Monthly Turnover:");
                    row.ConstantItem(100).AlignRight().Text($"R {data.MonthlyTurnover:N2}");
                });

                summaryCol.Item().Row(row =>
                {
                    row.RelativeItem().Text("Monthly Expenses:");
                    row.ConstantItem(100).AlignRight().Text($"R {data.MonthlyExpenses:N2}");
                });

                var profitColor = data.ProfitLoss >= 0 ? Colors.Green.Darken3 : Colors.Red.Medium;
                summaryCol.Item().Row(row =>
                {
                    row.RelativeItem().Text("Profit/Loss:").FontColor(profitColor);
                    row.ConstantItem(100).AlignRight().Text($"R {data.ProfitLoss:N2}").FontColor(profitColor);
                });

                summaryCol.Item().Row(row =>
                {
                    row.RelativeItem().Text("Outstanding Payments:");
                    row.ConstantItem(100).AlignRight().Text($"R {data.OutstandingPayments:N2}");
                });
            });
        }

        private static void AddFiltersSection(ColumnDescriptor column, FinancePdfData data)
        {
            column.Item().Background(Colors.Grey.Lighten2).Padding(10).Column(filterCol =>
            {
                filterCol.Spacing(5);
                filterCol.Item().Text("APPLIED FILTERS").Bold().FontSize(12);
                filterCol.Item().Text(data.AppliedFilters);
            });
        }

        private static void AddExpenseDistribution(ColumnDescriptor column, FinancePdfData data)
        {
            column.Item().Background(Colors.Grey.Lighten3).Padding(15).Column(expenseCol =>
            {
                expenseCol.Spacing(8);
                expenseCol.Item().Text("EXPENSE DISTRIBUTION").Bold().FontSize(14);

                foreach (var expense in data.ExpenseDistribution.OrderByDescending(x => x.Value))
                {
                    expenseCol.Item().Row(row =>
                    {
                        row.RelativeItem().Text(expense.Key);
                        row.ConstantItem(100).AlignRight().Text($"R {expense.Value:N2}");
                    });
                }

                var totalExpenses = data.ExpenseDistribution.Sum(x => x.Value);
                expenseCol.Item().Row(row =>
                {
                    row.RelativeItem().Text("Total Expenses:").Bold();
                    row.ConstantItem(100).AlignRight().Text($"R {totalExpenses:N2}").Bold();
                });
            });
        }

        private static void AddSupplierPayments(ColumnDescriptor column, FinancePdfData data)
        {
            column.Item().PageBreak();
            column.Item().Text("SUPPLIER PAYMENTS").Bold().FontSize(14);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(100);
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(80);
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Supplier");
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Invoice");
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Date");
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Amount");
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Status");
                });

                foreach (DataRow row in data.SupplierPayments.Rows)
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(row["SupplierName"]?.ToString() ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(row["InvoiceNumber"]?.ToString() ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(row["InvoiceDate"]?.ToString() ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text($"R {Convert.ToDecimal(row["InvoiceAmount"]):N2}");

                    var status = row["Status"]?.ToString() ?? "";
                    var statusColor = status == "Paid" ? Colors.Green.Darken1 :
                                    status == "Pending" ? Colors.Red.Medium : Colors.Orange.Medium;
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(status).FontColor(statusColor);
                }
            });
        }

        private static void AddStaffSalaries(ColumnDescriptor column, FinancePdfData data)
        {
            column.Item().PageBreak();
            column.Item().Text("STAFF SALARIES").Bold().FontSize(14);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(100);
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(80);
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Name");
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Role");
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Salary");
                });

                foreach (DataRow row in data.StaffSalaries.Rows)
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(row["name"]?.ToString() ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(row["Role"]?.ToString() ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text($"R {Convert.ToDecimal(row["salary"]):N2}");
                }
            });
        }

        private static void AddExpenseBreakdown(ColumnDescriptor column, FinancePdfData data)
        {
            column.Item().PageBreak();
            column.Item().Text("EXPENSE BREAKDOWN").Bold().FontSize(14);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(100);
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(120);
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Type");
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Amount");
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Date");
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Description");
                });

                foreach (DataRow row in data.ExpenseBreakdown.Rows)
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(row["expenseType"]?.ToString() ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text($"R {Convert.ToDecimal(row["amount"]):N2}");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(row["Date"]?.ToString() ?? "");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(row["description"]?.ToString() ?? "");
                }
            });
        }

        private static void AddTurnoverTrend(ColumnDescriptor column, FinancePdfData data)
        {
            column.Item().PageBreak();
            column.Item().Text("TURNOVER TREND (Last 6 Months)").Bold().FontSize(14);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(100);
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Month");
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Turnover");
                });

                foreach (var trend in data.TurnoverTrend)
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(trend.Key);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text($"R {trend.Value:N2}");
                }
            });
        }

        private static void AddProfitLossTrend(ColumnDescriptor column, FinancePdfData data)
        {
            column.Item().PageBreak();
            column.Item().Text("PROFIT/LOSS TREND (Last 6 Months)").Bold().FontSize(14);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(100);
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Month");
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Profit/Loss");
                });

                foreach (var trend in data.ProfitLossTrend)
                {
                    var trendColor = trend.Value >= 0 ? Colors.Green.Darken3 : Colors.Red.Medium;
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(trend.Key);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(5).Text($"R {trend.Value:N2}").FontColor(trendColor);
                }
            });
        }

        private static void CreateTextReportFallback(FinancePdfData data, string filePath)
        {
            try
            {
                string textContent = $@"ADIX FINANCE REPORT
Generated: {data.ReportDate}

FINANCIAL SUMMARY:
==================
Monthly Turnover: R {data.MonthlyTurnover:N2}
Monthly Expenses: R {data.MonthlyExpenses:N2}
Profit/Loss: R {data.ProfitLoss:N2}
Outstanding Payments: R {data.OutstandingPayments:N2}

APPLIED FILTERS:
================
{data.AppliedFilters}

EXPENSE DISTRIBUTION:
=====================
";

                if (data.ExpenseDistribution != null && data.ExpenseDistribution.Any())
                {
                    foreach (var expense in data.ExpenseDistribution.OrderByDescending(x => x.Value))
                    {
                        textContent += $"{expense.Key}: R {expense.Value:N2}\n";
                    }
                    textContent += $"Total Expenses: R {data.ExpenseDistribution.Sum(x => x.Value):N2}\n";
                }

                textContent += $@"

TURNOVER TREND:
===============
";

                if (data.TurnoverTrend != null && data.TurnoverTrend.Any())
                {
                    foreach (var trend in data.TurnoverTrend)
                    {
                        textContent += $"{trend.Key}: R {trend.Value:N2}\n";
                    }
                }

                textContent += $@"

PROFIT/LOSS TREND:
==================
";

                if (data.ProfitLossTrend != null && data.ProfitLossTrend.Any())
                {
                    foreach (var trend in data.ProfitLossTrend)
                    {
                        textContent += $"{trend.Key}: R {trend.Value:N2}\n";
                    }
                }

                textContent += $@"

--- End of Report ---
This is a text fallback report. PDF generation failed.
";

                File.WriteAllText(filePath, textContent);
            }
            catch (Exception fallbackEx)
            {
                throw new Exception($"PDF generation failed and fallback also failed: {fallbackEx.Message}");
            }
        }
    }
}