using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace POS.Services
{
    public enum BarcodeScanStatus
    {
        Success,
        Failed,
        Canceled,
        NoReaderConnected
    }

    public class BarcodeScanResult
    {
        public BarcodeScanStatus Status { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? RawData { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool Handled { get; set; }
    }

    /// <summary>
    /// Barcode service for hardware wedge scanners and keyboard input handling.
    /// Integrates with MainWindow PreviewKeyDown event for Enter-key barcode entry.
    /// </summary>
    public sealed class BarcodeService
    {
        // Event raised when a barcode is scanned (from hardware wedge or keyboard Enter)
        public event Action<BarcodeScanResult>? ScanReceived;

        // Scanner connection status (placeholder for actual hardware detection)
        private bool _scannerConnected = false;
        private readonly ObservableCollection<BarcodeScanResult> _scanHistory = new ObservableCollection<BarcodeScanResult>();

        /// <summary>
        /// Gets the history of recent barcode scans (last 50 by default).
        /// Useful for UI display or analytics.
        /// </summary>
        public ObservableCollection<BarcodeScanResult> ScanHistory => _scanHistory;

        /// <summary>
        /// Checks if a barcode scanner is currently connected/recognized.
        /// In a real deployment, this would query the OS for attached HID devices
        /// or check if the scanner is emulating keyboard input.
        /// </summary>
        public bool ScannerConnected
        {
            get => _scannerConnected;
            set
            {
                if (_scannerConnected != value)
                {
                    _scannerConnected = value;
                    ScannerStatusChanged?.Invoke(value);
                    ScanReceived?.Invoke(new BarcodeScanResult
                    {
                        Status = value ? BarcodeScanStatus.Success : BarcodeScanStatus.NoReaderConnected,
                        Code = value ? "Scanner connected" : string.Empty,
                        Timestamp = DateTime.Now
                    });
                }
            }
        }

        /// <summary>
        /// Event raised when scanner connection status changes.
        /// UI can bind to this to show/hide scanner status indicators.
        /// </summary>
        public event Action<bool>? ScannerStatusChanged;

        /// <summary>
        /// Process a scanned barcode from hardware wedge or keyboard entry.
        /// Searches the product catalog and adds the product to the current cart.
        /// </summary>
        /// <param name="code">The scanned barcode or SKU string</param>
        /// <param name="productService">Reference to ProductsViewModel for catalog lookup</param>
        public void ProcessScan(string code, POS.ViewModels.ProductsViewModel? productService = null)
        {
            var result = new BarcodeScanResult
            {
                Status = BarcodeScanStatus.Success,
                Code = code,
                Handled = true,
                RawData = code
            };

            try
            {
                if (productService != null)
                {
                    var product = productService.AllProducts.FirstOrDefault(p =>
                        p.Barcode == code || p.Barcode == code.Trim());

                    if (product != null)
                    {
                        result.Code = product.Barcode;
                        result.RawData = product.Name;
                        System.Diagnostics.Debug.WriteLine($"[Barcode] Found product: {product.Name} (ID:{product.Id})");
                    }
                    else
                    {
                        var matches = productService.AllProducts.Where(p =>
                            p.Name.IndexOf(code, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            p.Barcode.IndexOf(code, StringComparison.OrdinalIgnoreCase) >= 0)
                            .Take(3).ToList();

                        if (matches.Any())
                        {
                            result.Status = BarcodeScanStatus.Success;
                            result.Code = matches.First().Barcode;
                            result.RawData = $"Found {matches.Count} matches";
                            System.Diagnostics.Debug.WriteLine($"[Barcode] Partial matches for: {code}");
                        }
                        else
                        {
                            result.Status = BarcodeScanStatus.Failed;
                            result.Code = code;
                            System.Diagnostics.Debug.WriteLine($"[Barcode] No product found for: {code}");
                        }
                    }
                }
                else
                {
                    result.Status = BarcodeScanStatus.Failed;
                    result.Code = code;
                }
            }
            catch (Exception ex)
            {
                result.Status = BarcodeScanStatus.Failed;
                result.Code = code;
                System.Diagnostics.Debug.WriteLine($"[Barcode] Error: {ex.Message}");
            }
            finally
            {
                if (result.Status == BarcodeScanStatus.Success)
                {
                    _scanHistory.Add(result);
                    if (_scanHistory.Count > 50)
                        _scanHistory.RemoveAt(0);
                }

                ScanReceived?.Invoke(result);
            }
        }

        public void Reset()
        {
            _scanHistory.Clear();
            ScannerConnected = false;
        }
    }
}
