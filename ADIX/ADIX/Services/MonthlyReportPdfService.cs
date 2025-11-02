using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ADIX.Services
{
    public static class MonthlyReportPdfService
    {
        public static void GeneratePdf(MonthlyReportData report,
                                     List<Transaction> transactions,
                                     string month,
                                     string year,
                                     string filePath)
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
                        .Text($"Monthly Report - {month} {year}")
                        .SemiBold().FontSize(16).FontColor(Colors.Black);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(20);

                            // Financial Summary Section
                            column.Item().Component(new FinancialSummaryComponent(report));

                            // Transactions Table
                            column.Item().Component(new TransactionsTableComponent(transactions));

                            // Expense Breakdown
                            column.Item().Component(new ExpensesComponent(report));

                            // Profit & Loss
                            column.Item().Component(new ProfitLossComponent(report));
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
    }

    // Financial Summary Component
    public class FinancialSummaryComponent : IComponent
    {
        private MonthlyReportData _report;

        public FinancialSummaryComponent(MonthlyReportData report)
        {
            _report = report;
        }

        public void Compose(IContainer container)
        {
            container.Background(Colors.Grey.Lighten3).Padding(10).Column(column =>
            {
                column.Spacing(5);

                column.Item().Text("Financial Summary").Bold().FontSize(12);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Card: R {_report.CardAmount:F2}");
                    row.RelativeItem().Text($"Cash: R {_report.CashAmount:F2}");
                    row.RelativeItem().Text($"EFT: R {_report.EFTAmount:F2}");
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Returns: R {_report.ReturnAmount:F2}");
                    row.RelativeItem().Text($"Credit: R {_report.CreditAmount:F2}");
                    row.RelativeItem().Text($"Total: R {_report.TotalTurnover:F2}").Bold();
                });
            });
        }
    }

    // Transactions Table Component
    public class TransactionsTableComponent : IComponent
    {
        private List<Transaction> _transactions;

        public TransactionsTableComponent(List<Transaction> transactions)
        {
            _transactions = transactions;
        }

        public void Compose(IContainer container)
        {
            container.Column(column =>
            {
                column.Spacing(5);

                column.Item().Text($"Transactions ({_transactions.Count} total)").Bold().FontSize(12);

                if (_transactions.Any())
                {
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.5f); // Date
                            columns.RelativeColumn(2);    // Customer
                            columns.RelativeColumn(1.5f); // Staff
                            columns.RelativeColumn(1.5f); // Paid
                            columns.RelativeColumn(1.5f); // Amount
                            columns.RelativeColumn(1.5f); // Method
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Date").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Customer").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Staff").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Paid").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Amount").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Method").FontColor(Colors.White).Bold();
                        });

                        foreach (var transaction in _transactions)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(transaction.Date.ToString("dd/MM/yyyy"));
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(transaction.CustomerName);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(transaction.SalesStaff);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text($"R {transaction.Paid:F2}");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text($"R {transaction.PurchaseAmount:F2}");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(transaction.PaymentMethod);
                        }
                    });
                }
                else
                {
                    column.Item().Text("No transactions found for this period.").Italic();
                }
            });
        }
    }

    // Expenses Component
    public class ExpensesComponent : IComponent
    {
        private MonthlyReportData _report;

        public ExpensesComponent(MonthlyReportData report)
        {
            _report = report;
        }

        public void Compose(IContainer container)
        {
            container.Background(Colors.Grey.Lighten3).Padding(10).Column(column =>
            {
                column.Spacing(5);

                column.Item().Text("Monthly Expenses").Bold().FontSize(12);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Rent: R {_report.RentExpense:F2}");
                    row.RelativeItem().Text($"Utilities: R {_report.UtilitiesExpense:F2}");
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Salaries: R {_report.SalaryExpense:F2}");
                    row.RelativeItem().Text($"Other: R {_report.OtherExpense:F2}");
                });

                column.Item().Text($"Total Expenses: R {_report.TotalExpenses:F2}").Bold();
            });
        }
    }

    // Profit & Loss Component
    public class ProfitLossComponent : IComponent
    {
        private MonthlyReportData _report;

        public ProfitLossComponent(MonthlyReportData report)
        {
            _report = report;
        }

        public void Compose(IContainer container)
        {
            var profitColor = _report.NetProfit >= 0 ? Colors.Green.Darken3 : Colors.Red.Medium;

            container.Background(Colors.Grey.Lighten3).Padding(10).Column(column =>
            {
                column.Spacing(5);

                column.Item().Text("Profit & Loss Statement").Bold().FontSize(12);

                column.Item().Text($"Cost of Business: R {_report.MonthlyCostOfBusiness:F2}");
                column.Item().Text($"Gross Profit: R {_report.GrossProfit:F2}");
                column.Item().Text($"Net Profit: R {_report.NetProfit:F2}").FontColor(profitColor).Bold();
                column.Item().Text($"Profit Margin: {_report.ProfitMargin:F2}%").FontColor(profitColor).Bold();
            });
        }
    }
}