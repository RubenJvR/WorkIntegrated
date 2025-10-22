using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ADIX.Services
{
    public static class PrintService
    {
        public static void SaveAsPng(FrameworkElement element, string filePath)
        {
            try
            {
                // Create print-optimized version
                FrameworkElement printElement = CreatePrintFriendlyElement(element);

                // Use fixed A4 dimensions
                double totalWidth = 794; // A4 width in pixels at 96 DPI
                double totalHeight = 1122; // A4 height in pixels at 96 DPI

                // Calculate required height for content
                totalHeight = CalculateRequiredHeight(printElement, totalWidth);

                // Ensure proper layout
                printElement.Measure(new Size(totalWidth, totalHeight));
                printElement.Arrange(new Rect(0, 0, totalWidth, totalHeight));

                // Force layout completion
                printElement.UpdateLayout();

                // Create render bitmap
                RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                    (int)totalWidth,
                    (int)totalHeight,
                    96d, 96d, PixelFormats.Pbgra32);

                renderBitmap.Render(printElement);

                // Save as PNG
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }

                MessageBox.Show($"Document saved successfully at:\n{filePath}", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving document: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void PrintVisual(FrameworkElement element, string description)
        {
            try
            {
                PrintDialog printDialog = new PrintDialog();

                if (printDialog.ShowDialog() == true)
                {
                    FrameworkElement printElement = CreatePrintFriendlyElement(element);

                    double printableWidth = printDialog.PrintableAreaWidth;
                    double printableHeight = printDialog.PrintableAreaHeight;

                    printElement.Measure(new Size(printableWidth, printableHeight));
                    printElement.Arrange(new Rect(0, 0, printableWidth, printableHeight));
                    printElement.UpdateLayout();

                    printDialog.PrintVisual(printElement, description);

                    MessageBox.Show("Document sent to printer successfully!", "Print Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error printing document: {ex.Message}", "Print Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static double CalculateRequiredHeight(FrameworkElement element, double width)
        {
            double baseHeight = 1122; // A4 base height

            // Find DataGrid and calculate required height
            var dataGrid = FindDataGrid(element);
            if (dataGrid != null && dataGrid.Items.Count > 0)
            {
                // More accurate height calculation
                double headerHeight = dataGrid.ColumnHeaderHeight;
                double rowHeight = dataGrid.RowHeight > 0 ? dataGrid.RowHeight : 22; // Default row height
                double borderHeight = dataGrid.BorderThickness.Top + dataGrid.BorderThickness.Bottom;
                double marginHeight = dataGrid.Margin.Top + dataGrid.Margin.Bottom;

                // Calculate total DataGrid height
                double dataGridHeight = headerHeight + (dataGrid.Items.Count * rowHeight) +
                                      borderHeight + marginHeight + 10; // +10 for padding

                // Adjust total height if needed
                if (dataGridHeight > 400) // If DataGrid is larger than typical A4 section
                {
                    baseHeight = Math.Max(baseHeight, 800 + dataGridHeight);
                }
            }

            return Math.Max(baseHeight, 1122);
        }

        private static FrameworkElement CreatePrintFriendlyElement(FrameworkElement originalElement)
        {
            if (originalElement is Grid mainGrid)
            {
                return CreatePrintOptimizedVersion(mainGrid);
            }
            return CreateFallbackPrintVersion(originalElement);
        }

        private static FrameworkElement CreatePrintOptimizedVersion(Grid originalGrid)
        {
            Grid printGrid = new Grid();
            printGrid.Background = Brushes.White;
            printGrid.Width = 794;

            // Copy row definitions
            foreach (var rowDef in originalGrid.RowDefinitions)
            {
                var newRowDef = new RowDefinition();

                // For DataGrid rows, set to Auto to accommodate content
                if (IsDataGridRow(originalGrid, printGrid.RowDefinitions.Count))
                {
                    newRowDef.Height = GridLength.Auto;
                }
                else
                {
                    newRowDef.Height = rowDef.Height;
                }

                printGrid.RowDefinitions.Add(newRowDef);
            }

            // Copy column definitions
            foreach (var colDef in originalGrid.ColumnDefinitions)
            {
                printGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = colDef.Width });
            }

            // Copy non-button elements
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(originalGrid); i++)
            {
                var child = VisualTreeHelper.GetChild(originalGrid, i) as UIElement;
                if (child == null) continue;

                // Skip buttons and button containers
                if (child is Button || IsButtonContainer(child, originalGrid))
                    continue;

                FrameworkElement copy = CreateVisualCopy(child);
                if (copy != null)
                {
                    // Copy layout properties
                    Grid.SetRow(copy, Grid.GetRow(child));
                    Grid.SetColumn(copy, Grid.GetColumn(child));
                    Grid.SetRowSpan(copy, Grid.GetRowSpan(child));
                    Grid.SetColumnSpan(copy, Grid.GetColumnSpan(child));

                    printGrid.Children.Add(copy);
                }
            }

            // Remove button row if it exists
            if (printGrid.RowDefinitions.Count > 10)
            {
                printGrid.RowDefinitions.RemoveAt(10);
            }

            return printGrid;
        }

        private static bool IsDataGridRow(Grid grid, int rowIndex)
        {
            foreach (UIElement child in grid.Children)
            {
                if (Grid.GetRow(child) == rowIndex && child is DataGrid)
                {
                    return true;
                }
            }
            return false;
        }

        private static FrameworkElement CreateVisualCopy(UIElement original)
        {
            if (original is Border originalBorder)
            {
                Border copyBorder = new Border();
                copyBorder.Background = Brushes.White; // Force white background
                copyBorder.BorderBrush = originalBorder.BorderBrush;
                copyBorder.BorderThickness = originalBorder.BorderThickness;
                copyBorder.CornerRadius = originalBorder.CornerRadius;
                copyBorder.Margin = originalBorder.Margin;
                copyBorder.Padding = originalBorder.Padding;
                copyBorder.Width = originalBorder.Width;
                copyBorder.Height = originalBorder.Height;
                copyBorder.HorizontalAlignment = originalBorder.HorizontalAlignment;
                copyBorder.VerticalAlignment = originalBorder.VerticalAlignment;

                // Apply green styling for specific sections
                if (IsHeadingBorder(originalBorder) || IsSectionBorder(originalBorder))
                {
                    copyBorder.Background = new SolidColorBrush(Color.FromRgb(0, 128, 0)); // Green background
                    copyBorder.BorderBrush = Brushes.Black; // Black border
                }

                if (originalBorder.Child != null)
                {
                    var childCopy = CreateVisualCopy(originalBorder.Child as UIElement);
                    if (childCopy != null)
                        copyBorder.Child = childCopy;
                }

                return copyBorder;
            }
            else if (original is TextBlock originalTextBlock)
            {
                TextBlock copyTextBlock = new TextBlock
                {
                    Text = originalTextBlock.Text,
                    FontSize = originalTextBlock.FontSize,
                    FontWeight = originalTextBlock.FontWeight,
                    FontFamily = originalTextBlock.FontFamily,
                    Foreground = Brushes.Black, // Force black text
                    Background = Brushes.Transparent, // Allow parent background to show through
                    HorizontalAlignment = originalTextBlock.HorizontalAlignment,
                    VerticalAlignment = originalTextBlock.VerticalAlignment,
                    TextAlignment = originalTextBlock.TextAlignment,
                    TextWrapping = originalTextBlock.TextWrapping,
                    Margin = originalTextBlock.Margin,
                    Padding = originalTextBlock.Padding
                };

                // Apply white text for green backgrounds
                if (IsHeadingText(originalTextBlock) || IsSectionText(originalTextBlock))
                {
                    copyTextBlock.Foreground = Brushes.White; // White text on green background
                    copyTextBlock.FontWeight = FontWeights.Bold;
                }

                return copyTextBlock;
            }
            else if (original is DataGrid originalDataGrid)
            {
                return CreatePrintStyledDataGrid(originalDataGrid);
            }
            else if (original is StackPanel originalStackPanel)
            {
                StackPanel copyStackPanel = new StackPanel();
                copyStackPanel.Orientation = originalStackPanel.Orientation;
                copyStackPanel.Margin = originalStackPanel.Margin;
                copyStackPanel.HorizontalAlignment = originalStackPanel.HorizontalAlignment;
                copyStackPanel.VerticalAlignment = originalStackPanel.VerticalAlignment;
                copyStackPanel.Background = Brushes.Transparent; // Allow parent background

                foreach (UIElement child in originalStackPanel.Children)
                {
                    var childCopy = CreateVisualCopy(child);
                    if (childCopy != null)
                        copyStackPanel.Children.Add(childCopy);
                }

                return copyStackPanel;
            }
            else if (original is Grid originalGrid)
            {
                Grid copyGrid = new Grid();
                copyGrid.Background = Brushes.Transparent; // Allow parent background
                copyGrid.Margin = originalGrid.Margin;

                // Copy column definitions
                foreach (var colDef in originalGrid.ColumnDefinitions)
                {
                    copyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = colDef.Width });
                }

                // Copy row definitions
                foreach (var rowDef in originalGrid.RowDefinitions)
                {
                    copyGrid.RowDefinitions.Add(new RowDefinition { Height = rowDef.Height });
                }

                // Copy children
                foreach (UIElement child in originalGrid.Children)
                {
                    var childCopy = CreateVisualCopy(child);
                    if (childCopy != null)
                    {
                        Grid.SetRow(childCopy, Grid.GetRow(child));
                        Grid.SetColumn(childCopy, Grid.GetColumn(child));
                        Grid.SetRowSpan(childCopy, Grid.GetRowSpan(child));
                        Grid.SetColumnSpan(childCopy, Grid.GetColumnSpan(child));
                        copyGrid.Children.Add(childCopy);
                    }
                }

                return copyGrid;
            }
            else if (original is TextBox originalTextBox)
            {
                // Convert TextBox to TextBlock for print
                return new TextBlock
                {
                    Text = originalTextBox.Text,
                    FontSize = originalTextBox.FontSize,
                    FontFamily = originalTextBox.FontFamily,
                    Foreground = Brushes.Black, // Force black text
                    Background = Brushes.Transparent, // Allow parent background
                    TextWrapping = TextWrapping.Wrap,
                    Margin = originalTextBox.Margin,
                    Padding = originalTextBox.Padding,
                    HorizontalAlignment = originalTextBox.HorizontalAlignment,
                    VerticalAlignment = originalTextBox.VerticalAlignment
                };
            }
            else if (original is ScrollViewer originalScrollViewer)
            {
                // Remove scroll viewer and show content directly for printing
                if (originalScrollViewer.Content is UIElement content)
                {
                    return CreateVisualCopy(content);
                }
            }
            else if (original is Image originalImage)
            {
                // Handle Image elements (logo) - FIXED: Use the original image directly
                return CreateImageCopy(originalImage);
            }

            return null;
        }

        private static Image CreateImageCopy(Image originalImage)
        {
            Image copyImage = new Image();

            // Copy all properties from original image
            copyImage.Source = originalImage.Source; // Use same source
            copyImage.Width = originalImage.Width;
            copyImage.Height = originalImage.Height;
            copyImage.Stretch = originalImage.Stretch;
            copyImage.StretchDirection = originalImage.StretchDirection;
            copyImage.Margin = originalImage.Margin;
            copyImage.HorizontalAlignment = originalImage.HorizontalAlignment;
            copyImage.VerticalAlignment = originalImage.VerticalAlignment;
            copyImage.Opacity = originalImage.Opacity;

            // Ensure the image is visible
            copyImage.Visibility = Visibility.Visible;

            return copyImage;
        }

        private static bool IsHeadingBorder(Border border)
        {
            // Check if this is a heading border (both Quote and Invoice) by examining visual tree
            if (border.Child is TextBlock textBlock)
            {
                return IsHeadingText(textBlock);
            }
            return false;
        }

        private static bool IsSectionBorder(Border border)
        {
            // Check if this border contains section headers like "Bill to", "Payment", etc.
            if (border.Child is TextBlock textBlock)
            {
                return IsSectionText(textBlock);
            }
            return false;
        }

        private static bool IsHeadingText(TextBlock textBlock)
        {
            // Identify heading by text content and/or styling (both Quote and Invoice)
            string text = textBlock.Text ?? "";
            return text.Contains("Quote", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Quotation", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Invoice", StringComparison.OrdinalIgnoreCase) ||
                   (textBlock.FontWeight == FontWeights.Bold &&
                    textBlock.FontSize > 14); // Likely a heading
        }

        private static bool IsSectionText(TextBlock textBlock)
        {
            // Identify section headers that should be green
            string text = textBlock.Text ?? "";
            return text.Contains("Bill to", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Payment", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Comments", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Total", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Subtotal", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Balance Due", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("Amount Due", StringComparison.OrdinalIgnoreCase);
        }

        private static DataGrid CreatePrintStyledDataGrid(DataGrid original)
        {
            DataGrid printGrid = new DataGrid();

            // Basic properties - ensure everything is visible
            printGrid.Background = Brushes.White;
            printGrid.Foreground = Brushes.Black;
            printGrid.BorderBrush = Brushes.Black;
            printGrid.BorderThickness = new Thickness(1);
            printGrid.Margin = new Thickness(5); // Reduced margin to prevent cutting
            printGrid.Padding = new Thickness(2);

            // Layout properties - ensure full width and auto height
            printGrid.Width = 780; // Slightly less than A4 width to prevent cutting
            printGrid.HorizontalAlignment = HorizontalAlignment.Left;
            printGrid.VerticalAlignment = VerticalAlignment.Top;
            printGrid.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            printGrid.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

            // Data properties
            printGrid.ItemsSource = original.ItemsSource;
            printGrid.IsReadOnly = true;
            printGrid.AutoGenerateColumns = false; // Use explicit columns to prevent cutting
            printGrid.HeadersVisibility = DataGridHeadersVisibility.All;

            // Row properties
            printGrid.RowHeight = 25;
            printGrid.ColumnHeaderHeight = 30;
            printGrid.AlternatingRowBackground = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)); // Light gray alternating

            // Clear and copy columns with proper widths to prevent cutting
            printGrid.Columns.Clear();

            // Define column widths as percentages of total width to prevent cutting
            double totalWidth = 750; // Available width for columns
            foreach (var column in original.Columns)
            {
                if (column is DataGridTextColumn textColumn)
                {
                    DataGridTextColumn newColumn = new DataGridTextColumn
                    {
                        Header = textColumn.Header,
                        Binding = textColumn.Binding,
                        Width = new DataGridLength(120, DataGridLengthUnitType.Pixel) // Fixed width for all columns
                    };

                    // Style for cell content - ensure black text and white background
                    var elementStyle = new Style(typeof(TextBlock));
                    elementStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Black));
                    elementStyle.Setters.Add(new Setter(TextBlock.BackgroundProperty, Brushes.White));
                    elementStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4)));
                    elementStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Left));
                    elementStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
                    elementStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
                    newColumn.ElementStyle = elementStyle;

                    printGrid.Columns.Add(newColumn);
                }
            }

            // Adjust column widths if there are specific columns that were getting cut
            AdjustColumnWidths(printGrid);

            // Header style - ensure green background with white text and black borders
            printGrid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader));
            printGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 128, 0)))); // Green
            printGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White)); // White text for green background
            printGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            printGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Black));
            printGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            printGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4)));
            printGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            printGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            // Cell style - ensure visible borders and black text
            printGrid.CellStyle = new Style(typeof(DataGridCell));
            printGrid.CellStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Black));
            printGrid.CellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0.5)));
            printGrid.CellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(2)));
            printGrid.CellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
            printGrid.CellStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black));
            printGrid.CellStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            printGrid.CellStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            // Row style
            printGrid.RowStyle = new Style(typeof(DataGridRow));
            printGrid.RowStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
            printGrid.RowStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black));
            printGrid.RowStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty, printGrid.RowHeight));

            return printGrid;
        }

        private static void AdjustColumnWidths(DataGrid dataGrid)
        {
            // Adjust specific columns that were getting cut off
            foreach (var column in dataGrid.Columns)
            {
                var header = column.Header?.ToString() ?? "";

                // Set appropriate widths for commonly cut columns
                if (header.Contains("Unit Price", StringComparison.OrdinalIgnoreCase) ||
                    header.Contains("Price", StringComparison.OrdinalIgnoreCase))
                {
                    column.Width = new DataGridLength(80, DataGridLengthUnitType.Pixel);
                }
                else if (header.Contains("Item", StringComparison.OrdinalIgnoreCase) &&
                        !header.Contains("Description", StringComparison.OrdinalIgnoreCase))
                {
                    column.Width = new DataGridLength(80, DataGridLengthUnitType.Pixel);
                }
                else if (header.Contains("Disc", StringComparison.OrdinalIgnoreCase) ||
                        header.Contains("Discount", StringComparison.OrdinalIgnoreCase))
                {
                    column.Width = new DataGridLength(70, DataGridLengthUnitType.Pixel);
                }
                else if (header.Contains("Amount", StringComparison.OrdinalIgnoreCase) ||
                        header.Contains("Total", StringComparison.OrdinalIgnoreCase))
                {
                    column.Width = new DataGridLength(80, DataGridLengthUnitType.Pixel);
                }
                else if (header.Contains("Description", StringComparison.OrdinalIgnoreCase))
                {
                    column.Width = new DataGridLength(150, DataGridLengthUnitType.Pixel);
                }
                else
                {
                    column.Width = new DataGridLength(100, DataGridLengthUnitType.Pixel);
                }
            }
        }

        private static DataGrid FindDataGrid(DependencyObject parent)
        {
            if (parent == null) return null;

            if (parent is DataGrid dataGrid)
                return dataGrid;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var result = FindDataGrid(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static bool IsButtonContainer(UIElement element, Grid parentGrid)
        {
            int row = Grid.GetRow(element);
            return row == 10; // Assuming row 10 is the button row
        }

        private static FrameworkElement CreateFallbackPrintVersion(FrameworkElement originalElement)
        {
            Border container = new Border();
            container.Background = Brushes.White;
            container.Padding = new Thickness(40);
            container.Width = 794;
            container.Height = 1122;

            TextBlock fallbackText = new TextBlock
            {
                Text = "Print preview not available",
                FontSize = 14,
                Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            container.Child = fallbackText;
            return container;
        }
    }
}