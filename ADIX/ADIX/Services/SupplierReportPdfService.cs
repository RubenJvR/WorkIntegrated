using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ADIX.Services
{
    public static class SupplierReportPdfService
    {
        public static void GeneratePdf(SupplierReportData report, string filePath)
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
                        .Column(column =>
                        {
                            column.Item().AlignCenter().Text($"Supplier Report - {report.SupplierName}")
                                .SemiBold().FontSize(16).FontColor(Colors.Black);
                            column.Item().AlignCenter().Text($"{report.MonthName} {report.Year}")
                                .FontSize(12).FontColor(Colors.Grey.Darken1);
                        });

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(20);

                            // Summary Section
                            column.Item().Component(new SupplierSummaryComponent(report));

                            // Items Table
                            column.Item().Component(new SupplierItemsTableComponent(report));
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                            x.Span($" • Generated on {DateTime.Now:dd/MM/yyyy HH:mm}");
                        });
                });
            });

            document.GeneratePdf(filePath);
        }
    }

    // Supplier Summary Component
    public class SupplierSummaryComponent : IComponent
    {
        private SupplierReportData _report;

        public SupplierSummaryComponent(SupplierReportData report)
        {
            _report = report;
        }

        public void Compose(IContainer container)
        {
            container.Background(Colors.Green.Lighten4).Padding(15).Column(column =>
            {
                column.Spacing(8);

                column.Item().Text("Summary").Bold().FontSize(14);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Total Items").FontSize(9).FontColor(Colors.Grey.Darken1);
                        col.Item().Text(_report.TotalItems.ToString()).Bold().FontSize(13);
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Total Stock Value").FontSize(9).FontColor(Colors.Grey.Darken1);
                        col.Item().Text($"R {_report.TotalStockValue:F2}").Bold().FontSize(13).FontColor(Colors.Green.Darken2);
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Monthly Sales Value").FontSize(9).FontColor(Colors.Grey.Darken1);
                        col.Item().Text($"R {_report.TotalSalesValue:F2}").Bold().FontSize(13).FontColor(Colors.Blue.Darken2);
                    });
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Total Stock Quantity").FontSize(9).FontColor(Colors.Grey.Darken1);
                        col.Item().Text(_report.TotalStockQuantity.ToString()).FontSize(11);
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Total Monthly Sales").FontSize(9).FontColor(Colors.Grey.Darken1);
                        col.Item().Text(_report.TotalMonthlySales.ToString()).FontSize(11);
                    });

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Average Price").FontSize(9).FontColor(Colors.Grey.Darken1);
                        col.Item().Text($"R {(_report.TotalItems > 0 ? _report.TotalSalesValue / _report.TotalItems : 0):F2}").FontSize(11);
                    });
                });
            });
        }
    }

    // Supplier Items Table Component
    public class SupplierItemsTableComponent : IComponent
    {
        private SupplierReportData _report;

        public SupplierItemsTableComponent(SupplierReportData report)
        {
            _report = report;
        }

        public void Compose(IContainer container)
        {
            container.Column(column =>
            {
                column.Spacing(5);

                column.Item().Text($"Items ({_report.Items.Count} total)").Bold().FontSize(12);

                if (_report.Items.Any())
                {
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);    // Description
                            columns.RelativeColumn(1.2f); // Current Stock
                            columns.RelativeColumn(1.2f); // Monthly Sales
                            columns.RelativeColumn(1.5f); // Retail Price
                            columns.RelativeColumn(1.5f); // Cost Price
                            columns.RelativeColumn(1.5f); // Stock Value
                            columns.RelativeColumn(1.5f); // Sales Value
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Item Description").FontColor(Colors.White).Bold().FontSize(9);
                            header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Stock").FontColor(Colors.White).Bold().FontSize(9);
                            header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Sold").FontColor(Colors.White).Bold().FontSize(9);
                            header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Retail").FontColor(Colors.White).Bold().FontSize(9);
                            header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Cost").FontColor(Colors.White).Bold().FontSize(9);
                            header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Stock Value").FontColor(Colors.White).Bold().FontSize(9);
                            header.Cell().Background(Colors.Green.Medium).Padding(5).Text("Sales Value").FontColor(Colors.White).Bold().FontSize(9);
                        });

                        foreach (var item in _report.Items)
                        {
                            var isLowStock = item.CurrentStock < 5; // Highlight low stock

                            table.Cell()
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Background(isLowStock ? Colors.Red.Lighten4 : Colors.White)
                                .Padding(5)
                                .Text(item.Description)
                                .FontSize(9);

                            table.Cell()
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Background(isLowStock ? Colors.Red.Lighten4 : Colors.White)
                                .Padding(5)
                                .AlignRight()
                                .Text(item.CurrentStock.ToString())
                                .FontSize(9);

                            table.Cell()
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(5)
                                .AlignRight()
                                .Text(item.MonthlySales.ToString())
                                .FontSize(9);

                            table.Cell()
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(5)
                                .AlignRight()
                                .Text($"R {item.RetailPrice:F2}")
                                .FontSize(9);

                            table.Cell()
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(5)
                                .AlignRight()
                                .Text($"R {item.CostPrice:F2}")
                                .FontSize(9);

                            table.Cell()
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(5)
                                .AlignRight()
                                .Text($"R {item.StockValue:F2}")
                                .FontSize(9)
                                .FontColor(Colors.Green.Darken2);

                            table.Cell()
                                .BorderBottom(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(5)
                                .AlignRight()
                                .Text($"R {item.SalesValue:F2}")
                                .FontSize(9)
                                .FontColor(Colors.Blue.Darken2);
                        }

                        // Totals row
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("TOTALS").Bold().FontSize(9);
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(_report.TotalStockQuantity.ToString()).Bold().FontSize(9);
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(_report.TotalMonthlySales.ToString()).Bold().FontSize(9);
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("").FontSize(9);
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("").FontSize(9);
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text($"R {_report.TotalStockValue:F2}").Bold().FontSize(9);
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text($"R {_report.TotalSalesValue:F2}").Bold().FontSize(9);
                    });
                }
                else
                {
                    column.Item().Text("No items found for this supplier.").Italic();
                }
            });
        }
    }

    // Data classes
    public class SupplierReportData
    {
        public string SupplierName { get; set; }
        public string MonthName { get; set; }
        public string Year { get; set; }
        public int TotalItems { get; set; }
        public decimal TotalStockValue { get; set; }
        public decimal TotalSalesValue { get; set; }
        public int TotalStockQuantity { get; set; }
        public int TotalMonthlySales { get; set; }
        public List<SupplierItemData> Items { get; set; } = new List<SupplierItemData>();
    }

    public class SupplierItemData
    {
        public string Description { get; set; }
        public int CurrentStock { get; set; }
        public int MonthlySales { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal StockValue { get; set; }
        public decimal SalesValue { get; set; }
    }
}