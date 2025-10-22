using System;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps;

namespace ADIX.Services
{
    public static class PrintService
    {
        public static void SaveAsPng(FrameworkElement element, string filePath)
        {
            try
            {
                // Create print-optimized version that preserves XAML appearance
                FrameworkElement printElement = CreatePrintFriendlyElement(element);

                // Use fixed A4 dimensions for consistent layout
                double totalWidth = 794; // A4 width in pixels at 96 DPI
                double totalHeight = 1122; // A4 height in pixels at 96 DPI

                // Ensure the element is properly measured and arranged
                printElement.Measure(new Size(totalWidth, totalHeight));
                printElement.Arrange(new Rect(0, 0, totalWidth, totalHeight));

                // Wait for layout to complete
                printElement.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);

                // Create render bitmap with fixed dimensions
                RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                    (int)totalWidth,
                    (int)totalHeight,
                    96d, 96d, PixelFormats.Pbgra32);

                renderBitmap.Render(printElement);

                // Create PNG encoder
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                // Save to file
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
                    // Use print-friendly version instead of original element
                    FrameworkElement printElement = CreatePrintFriendlyElement(element);

                    // Use A4 dimensions for printing
                    double printableWidth = 794;
                    double printableHeight = 1122;

                    printElement.Measure(new Size(printableWidth, printableHeight));
                    printElement.Arrange(new Rect(0, 0, printableWidth, printableHeight));

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

        private static FrameworkElement CreatePrintFriendlyElement(FrameworkElement originalElement)
        {
            // Create a deep copy of the main content grid but remove buttons
            if (originalElement is Grid mainGrid)
            {
                return CreatePrintOptimizedVersion(mainGrid);
            }

            // Fallback: create a simple white background version
            return CreateFallbackPrintVersion(originalElement);
        }

        private static FrameworkElement CreatePrintOptimizedVersion(Grid originalGrid)
        {
            // Create a new grid that preserves the XAML structure but removes buttons
            Grid printGrid = new Grid();
            printGrid.Background = Brushes.White;
            printGrid.Width = 794; // A4 width in pixels at 96 DPI

            // Copy the row definitions from original grid
            foreach (var rowDef in originalGrid.RowDefinitions)
            {
                printGrid.RowDefinitions.Add(new RowDefinition { Height = rowDef.Height });
            }

            // Copy all child elements except buttons and the button row
            for (int i = 0; i < originalGrid.Children.Count; i++)
            {
                var child = originalGrid.Children[i];

                // Skip buttons and the button container (row 10)
                if (child is Button || IsButtonContainer(child, originalGrid))
                    continue;

                // Create a visual copy of the element
                FrameworkElement copy = CreateVisualCopy(child);
                if (copy != null)
                {
                    // Copy row and column positions
                    int row = Grid.GetRow(child);
                    int column = Grid.GetColumn(child);
                    int rowSpan = Grid.GetRowSpan(child);
                    int columnSpan = Grid.GetColumnSpan(child);

                    Grid.SetRow(copy, row);
                    Grid.SetColumn(copy, column);
                    Grid.SetRowSpan(copy, rowSpan);
                    Grid.SetColumnSpan(copy, columnSpan);

                    printGrid.Children.Add(copy);
                }
            }

            // Remove the button row definition and adjust the "Thank You" row
            if (printGrid.RowDefinitions.Count > 10)
            {
                // Move the "Thank You" section to be the last element
                AdjustThankYouSection(printGrid);

                // Remove the button row (row 10)
                printGrid.RowDefinitions.RemoveAt(10);
            }

            return printGrid;
        }

        private static bool IsButtonContainer(UIElement element, Grid parentGrid)
        {
            // Check if this element is in the button row (row 10)
            int row = Grid.GetRow(element);
            return row == 10;
        }

        private static FrameworkElement CreateVisualCopy(UIElement original)
        {
            if (original is Border originalBorder)
            {
                Border copyBorder = new Border();
                copyBorder.Background = Brushes.White; // Use white background for print
                copyBorder.BorderBrush = originalBorder.BorderBrush;
                copyBorder.BorderThickness = originalBorder.BorderThickness;
                copyBorder.CornerRadius = originalBorder.CornerRadius;
                copyBorder.Margin = originalBorder.Margin;
                copyBorder.Padding = originalBorder.Padding;
                copyBorder.Width = originalBorder.Width;
                copyBorder.Height = originalBorder.Height;
                copyBorder.HorizontalAlignment = originalBorder.HorizontalAlignment;
                copyBorder.VerticalAlignment = originalBorder.VerticalAlignment;

                if (originalBorder.Child != null)
                {
                    var childCopy = CreateVisualCopy(originalBorder.Child);
                    if (childCopy != null)
                        copyBorder.Child = childCopy;
                }

                return copyBorder;
            }
            else if (original is TextBlock originalTextBlock)
            {
                TextBlock copyTextBlock = new TextBlock();
                copyTextBlock.Text = originalTextBlock.Text;
                copyTextBlock.FontSize = originalTextBlock.FontSize;
                copyTextBlock.FontWeight = originalTextBlock.FontWeight;
                copyTextBlock.Foreground = Brushes.Black; // Use black for print
                copyTextBlock.Background = Brushes.Transparent;
                copyTextBlock.HorizontalAlignment = originalTextBlock.HorizontalAlignment;
                copyTextBlock.VerticalAlignment = originalTextBlock.VerticalAlignment;
                copyTextBlock.TextAlignment = originalTextBlock.TextAlignment;
                copyTextBlock.TextWrapping = originalTextBlock.TextWrapping;
                copyTextBlock.Margin = originalTextBlock.Margin;
                copyTextBlock.Padding = originalTextBlock.Padding;

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
                copyStackPanel.Background = Brushes.Transparent;

                foreach (var child in originalStackPanel.Children)
                {
                    var childCopy = CreateVisualCopy(child as UIElement);
                    if (childCopy != null)
                        copyStackPanel.Children.Add(childCopy);
                }

                return copyStackPanel;
            }
            else if (original is ScrollViewer)
            {
                // Skip scroll viewer for print
                return null;
            }
            else if (original is Grid originalGrid)
            {
                Grid copyGrid = new Grid();
                copyGrid.Background = Brushes.Transparent;
                copyGrid.Margin = originalGrid.Margin;
                copyGrid.Width = originalGrid.Width;
                copyGrid.Height = originalGrid.Height;

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
                foreach (var child in originalGrid.Children)
                {
                    var childCopy = CreateVisualCopy(child as UIElement);
                    if (childCopy != null)
                    {
                        int row = Grid.GetRow(child as UIElement);
                        int column = Grid.GetColumn(child as UIElement);
                        int rowSpan = Grid.GetRowSpan(child as UIElement);
                        int columnSpan = Grid.GetColumnSpan(child as UIElement);

                        Grid.SetRow(childCopy, row);
                        Grid.SetColumn(childCopy, column);
                        Grid.SetRowSpan(childCopy, rowSpan);
                        Grid.SetColumnSpan(childCopy, columnSpan);

                        copyGrid.Children.Add(childCopy);
                    }
                }

                return copyGrid;
            }
            else if (original is Image originalImage)
            {
                Image copyImage = new Image();
                copyImage.Source = originalImage.Source;
                copyImage.Stretch = originalImage.Stretch;
                copyImage.Width = originalImage.Width;
                copyImage.Height = originalImage.Height;
                copyImage.Margin = originalImage.Margin;
                copyImage.HorizontalAlignment = originalImage.HorizontalAlignment;
                copyImage.VerticalAlignment = originalImage.VerticalAlignment;

                return copyImage;
            }
            else if (original is TextBox originalTextBox)
            {
                // Convert TextBox to TextBlock for print
                TextBlock textBlock = new TextBlock();
                textBlock.Text = originalTextBox.Text;
                textBlock.FontSize = originalTextBox.FontSize;
                textBlock.Foreground = Brushes.Black; // Use black for print
                textBlock.Background = Brushes.Transparent;
                textBlock.TextWrapping = originalTextBox.TextWrapping;
                textBlock.Margin = originalTextBox.Margin;
                textBlock.Padding = originalTextBox.Padding;
                textBlock.HorizontalAlignment = originalTextBox.HorizontalAlignment;
                textBlock.VerticalAlignment = originalTextBox.VerticalAlignment;

                return textBlock;
            }

            return null;
        }

        private static void AdjustThankYouSection(Grid printGrid)
        {
            // Find and adjust the "Thank You" text block to be more prominent
            foreach (var child in printGrid.Children)
            {
                if (child is TextBlock textBlock && textBlock.Text == "Thank You For Your Business!")
                {
                    textBlock.Foreground = Brushes.Black;
                    textBlock.FontSize = 18;
                    textBlock.FontWeight = FontWeights.Bold;
                    textBlock.Margin = new Thickness(0, 30, 0, 40);
                    break;
                }
            }
        }

        private static DataGrid CreatePrintStyledDataGrid(DataGrid original)
        {
            DataGrid printGrid = new DataGrid();
            printGrid.Width = original.Width;
            printGrid.Height = original.Height;
            printGrid.Background = Brushes.White;
            printGrid.Foreground = Brushes.Black;
            printGrid.FontSize = original.FontSize;
            printGrid.HeadersVisibility = original.HeadersVisibility;
            printGrid.IsReadOnly = true;
            printGrid.AutoGenerateColumns = original.AutoGenerateColumns;
            printGrid.BorderBrush = Brushes.Black;
            printGrid.BorderThickness = original.BorderThickness;
            printGrid.Margin = original.Margin;
            printGrid.ItemsSource = original.ItemsSource;
            printGrid.RowHeight = original.RowHeight;
            printGrid.ColumnHeaderHeight = original.ColumnHeaderHeight;
            printGrid.AlternatingRowBackground = Brushes.LightGray;

            // Copy columns
            printGrid.Columns.Clear();
            foreach (var column in original.Columns)
            {
                if (column is DataGridTextColumn textColumn)
                {
                    DataGridTextColumn newColumn = new DataGridTextColumn
                    {
                        Header = textColumn.Header,
                        Binding = textColumn.Binding,
                        Width = textColumn.Width
                    };

                    // Style for black text
                    var elementStyle = new Style(typeof(TextBlock));
                    elementStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Black));
                    elementStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(5)));
                    newColumn.ElementStyle = elementStyle;

                    printGrid.Columns.Add(newColumn);
                }
            }

            // Style headers for print
            printGrid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader));
            printGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.LightGray));
            printGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black));
            printGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            printGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Black));
            printGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            printGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(5)));
            printGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            printGrid.ColumnHeaderStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));

            // Style cells for print
            printGrid.CellStyle = new Style(typeof(DataGridCell));
            printGrid.CellStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Black));
            printGrid.CellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0.5)));
            printGrid.CellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(5, 2, 5, 2)));
            printGrid.CellStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            printGrid.CellStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            printGrid.CellStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black));
            printGrid.CellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));

            // Style rows for print
            printGrid.RowStyle = new Style(typeof(DataGridRow));
            printGrid.RowStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
            printGrid.RowStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black));

            return printGrid;
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