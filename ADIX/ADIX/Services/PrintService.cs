using System;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
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
                // Ensure the element is properly measured and arranged
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                element.Arrange(new Rect(element.DesiredSize));

                // Create render bitmap
                RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                    (int)element.ActualWidth,
                    (int)element.ActualHeight,
                    96d, 96d, PixelFormats.Pbgra32);

                renderBitmap.Render(element);

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

                    // Ensure proper sizing
                    printElement.Measure(new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight));
                    printElement.Arrange(new Rect(new Point(0, 0), printElement.DesiredSize));

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
            if (originalElement is Page page)
            {
                // Look for the PrintVersionGrid
                var printGrid = page.FindName("PrintVersionGrid") as Grid;
                if (printGrid != null)
                {
                    // Make it visible for printing
                    printGrid.Visibility = Visibility.Visible;
                    printGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    printGrid.Arrange(new Rect(printGrid.DesiredSize));

                    // Create a container with explicit white background
                    Border printContainer = new Border();
                    printContainer.Background = Brushes.White;
                    printContainer.Child = printGrid;

                    // Render to bitmap with white background
                    RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                        (int)printGrid.ActualWidth,
                        (int)printGrid.ActualHeight,
                        96d, 96d, PixelFormats.Pbgra32);

                    renderBitmap.Render(printContainer);

                    // Create image from the bitmap
                    Image printImage = new Image();
                    printImage.Source = renderBitmap;
                    printImage.Width = printGrid.ActualWidth;
                    printImage.Height = printGrid.ActualHeight;
                    printImage.Stretch = Stretch.None;

                    // Hide the original print grid
                    printGrid.Visibility = Visibility.Collapsed;

                    return printImage;
                }
            }

            // Fallback: create a simple white background version
            Border fallback = new Border();
            fallback.Background = Brushes.White;
            fallback.Child = new Border()
            {
                Background = Brushes.White, // Explicit white background
                Child = new Border()
                {
                    Background = new VisualBrush(originalElement)
                    {
                        Stretch = Stretch.Uniform
                    }
                }
            };
            return fallback;
        }
    }


}