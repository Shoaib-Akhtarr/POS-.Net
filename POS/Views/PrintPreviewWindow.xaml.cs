using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Xml;
using POS.Services;

namespace POS.Views
{
    public partial class PrintPreviewWindow : Window
    {
        private readonly FlowDocument _originalDocument;
        private readonly string _jobTitle;
        private double _currentZoom = 1.0;

        public PrintPreviewWindow(FlowDocument document, string jobTitle)
        {
            InitializeComponent();
            _originalDocument = document;
            _jobTitle = jobTitle;

            TxtTitle.Text = jobTitle;

            // Display document formatted for A4 preview
            ConfigureForPreview(_originalDocument);
            DocViewer.Document = _originalDocument;
        }

        private static void ConfigureForPreview(FlowDocument doc)
        {
            doc.PageWidth = 794; // Standard A4 width @ 96 DPI
            doc.PagePadding = new Thickness(48);
            doc.ColumnWidth = 794 - 96;
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentZoom < 2.0)
            {
                _currentZoom = Math.Min(2.0, _currentZoom + 0.15);
                ApplyZoom();
            }
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (_currentZoom > 0.5)
            {
                _currentZoom = Math.Max(0.5, _currentZoom - 0.15);
                ApplyZoom();
            }
        }

        private void ApplyZoom()
        {
            DocViewer.Zoom = _currentZoom * 100.0;
            TxtZoomLevel.Text = $"{(int)(_currentZoom * 100)}%";
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var printDoc = PrintService.CloneDocument(_originalDocument);
                    double width = dlg.PrintableAreaWidth > 0 ? dlg.PrintableAreaWidth : 794;
                    double height = dlg.PrintableAreaHeight > 0 ? dlg.PrintableAreaHeight : 1123;

                    printDoc.PageWidth = width;
                    printDoc.PageHeight = height;
                    printDoc.PagePadding = new Thickness(40);
                    printDoc.ColumnWidth = width - 80;

                    var paginator = ((IDocumentPaginatorSource)printDoc).DocumentPaginator;
                    paginator.PageSize = new Size(width, height);

                    dlg.PrintDocument(paginator, _jobTitle);
                    MessageBox.Show("Document sent to printer successfully.", "Printing", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Printing failed: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SavePdf_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new PrintDialog();

            // Auto-select PDF printer if installed
            var pdfPrinter = PrintService.GetInstalledPrinters().FirstOrDefault(p =>
                p.Contains("PDF", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(pdfPrinter))
            {
                try
                {
                    dlg.PrintQueue = new System.Printing.PrintServer().GetPrintQueue(pdfPrinter);
                }
                catch
                {
                    // Fall back to default printer
                }
            }

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var printDoc = PrintService.CloneDocument(_originalDocument);
                    double width = dlg.PrintableAreaWidth > 0 ? dlg.PrintableAreaWidth : 794;
                    double height = dlg.PrintableAreaHeight > 0 ? dlg.PrintableAreaHeight : 1123;

                    printDoc.PageWidth = width;
                    printDoc.PageHeight = height;
                    printDoc.PagePadding = new Thickness(40);
                    printDoc.ColumnWidth = width - 80;

                    var paginator = ((IDocumentPaginatorSource)printDoc).DocumentPaginator;
                    paginator.PageSize = new Size(width, height);

                    dlg.PrintDocument(paginator, _jobTitle);
                    MessageBox.Show("PDF export completed.", "PDF Export", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"PDF Export failed: {ex.Message}", "PDF Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }
    }
}
