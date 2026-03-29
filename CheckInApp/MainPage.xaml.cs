using CheckInApp.Services;
using CheckInApp.ViewModels;

namespace CheckInApp;

public partial class MainPage : ContentPage
{
    private readonly IBarcodeService? _barcodeService;
    private bool _isProcessing;

    // BUG FIX 3: MainViewModel se inyecta desde DI para que BindingContext esté
    // disponible desde el constructor y no dependa de la instancia creada en XAML.
    // BUG FIX 4: IBarcodeService ahora SÍ llega con valor real desde DI (ya no es null)
    // porque MauiProgram registra tanto IBarcodeService como MainPage en el contenedor.
    public MainPage(MainViewModel viewModel, IBarcodeService? barcodeService = null)
    {
        InitializeComponent();

        // Asignamos el ViewModel inyectado — esto reemplaza el <ContentPage.BindingContext>
        // que estaba en el XAML y que creaba una instancia separada ignorando el DI.
        BindingContext = viewModel;

        _barcodeService = barcodeService;

#if ANDROID
        if (_barcodeService != null)
            _barcodeService.BarcodeScanned += OnBarcodeScanned;
#endif

        barcodeReader.Options = new ZXing.Net.Maui.BarcodeReaderOptions
        {
            Formats = ZXing.Net.Maui.BarcodeFormat.Code39 |
                      ZXing.Net.Maui.BarcodeFormat.QrCode,
            AutoRotate = true,
            Multiple = false
        };
    }

    protected override void OnDisappearing()
    {
#if ANDROID
        if (_barcodeService != null)
            _barcodeService.BarcodeScanned -= OnBarcodeScanned;
#endif
        base.OnDisappearing();
    }

    // 🔫 Scanner físico (PA768 / Unitech)
    private void OnBarcodeScanned(string barcode)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await ProcesarCodigo(barcode);
        });
    }

    // 📷 Cámara (ZXing)
    private void BarcodesDetected(object sender, ZXing.Net.Maui.BarcodeDetectionEventArgs e)
    {
        if (e.Results != null && e.Results.Any())
        {
            var result = e.Results.First();

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await ProcesarCodigo(result.Value);
            });
        }
    }

    // 🔥 Método central unificado (físico + cámara)
    private async Task ProcesarCodigo(string codigo)
    {
        if (_isProcessing)
            return;

        _isProcessing = true;

        try
        {
            var viewModel = (MainViewModel)BindingContext;
            await viewModel.ProcesarCodigoResultadoCommand.ExecuteAsync(codigo);
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private void OnCancelScanClicked(object sender, EventArgs e)
    {
        var viewModel = (MainViewModel)BindingContext;
        viewModel.IsScanning = false;
        viewModel.StatusMessage = "Escaneo cancelado";
    }
}
