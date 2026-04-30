using CheckInApp.Converters;
using CheckInApp.Services;
using CheckInApp.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace CheckInApp;

public partial class MainPage : ContentPage
{
    private readonly IBarcodeService? _barcodeService;
    private bool _isProcessing;
    double _startY;
    double _currentY;
    const double OpenPosition = 0;
    const double ClosedPosition = 400;
    const double SnapThreshold = 120;

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

        WeakReferenceMessenger.Default.Register<CloseDetailMessage>(this, async (r, m) =>
        {
            await HideDetails();
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is MainViewModel vm)
        {
            vm.PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName == nameof(vm.MostrarDetalles))
                {
                    if (vm.MostrarDetalles)
                        await ShowSheet();
                    else
                        await HideSheet();
                }
            };
        }

        // Tap backdrop para cerrar
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) => await HideSheet();
        Backdrop.GestureRecognizers.Add(tap);
    }
    private async Task ShowSheet()
    {
        Backdrop.IsVisible = true;
        DetailSheet.IsVisible = true;

        DetailSheet.TranslationY = ClosedPosition;
        Backdrop.Opacity = 0;

        await Task.WhenAll(
            DetailSheet.TranslateTo(0, OpenPosition, 280, Easing.CubicOut),
            Backdrop.FadeTo(0.4, 280, Easing.CubicOut)
        );
    }
    private async Task HideSheet()
    {
        await Task.WhenAll(
            DetailSheet.TranslateTo(0, ClosedPosition, 220, Easing.CubicIn),
            Backdrop.FadeTo(0, 200, Easing.CubicIn)
        );

        Backdrop.IsVisible = false;
        DetailSheet.IsVisible = false;

        if (BindingContext is MainViewModel vm)
            vm.MostrarDetalles = false;
    }

    private async Task ShowDetails()
    {
        DetailSheet.TranslationY = 350;
        DetailSheet.IsVisible = true;

        await DetailSheet.TranslateTo(0, 0, 260, Easing.CubicOut);
    }

    private async Task HideDetails()
    {
        await DetailSheet.TranslateTo(0, 350, 200, Easing.CubicIn);

        DetailSheet.IsVisible = false;

        if (BindingContext is MainViewModel vm)
            vm.MostrarDetalles = false;
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
            MostrarPanelDetalle();
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
                MostrarPanelDetalle();
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
            MostrarPanelDetalle();
        }
    }

    private void OnCancelScanClicked(object sender, EventArgs e)
    {
        var viewModel = (MainViewModel)BindingContext;
        viewModel.IsScanning = false;
        viewModel.StatusMessage = "Escaneo cancelado";
    }


    #region PANEL DRAGGING (opcional, mejora UX)
    private async void MostrarPanelDetalle()
    {
        if (BindingContext is MainViewModel vm && vm.InvitadoSeleccionado != null)
        {
            OverlayGrid.IsVisible = true;
            DetailSheet.TranslationY = 400; // posición inicial abajo
            Backdrop.Opacity = 0;

            await Task.WhenAll(
                Backdrop.FadeTo(0.45, 250, Easing.CubicOut),
                DetailSheet.TranslateTo(0, 0, 350, Easing.CubicOut)
            );
        }
    }

    private async Task OcultarPanelDetalle()
    {
        await Task.WhenAll(
            Backdrop.FadeTo(0, 200, Easing.CubicIn),
            DetailSheet.TranslateTo(0, 400, 300, Easing.CubicIn)
        );

        // Limpiar selección y ocultar overlay
        if (BindingContext is MainViewModel vm)
            vm.LimpiarResultadoCommand.Execute(null);

        OverlayGrid.IsVisible = false;
    }

    // Manejo del Pan actualizado (arrastrar hacia abajo)
    private async void OnPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Running:
                // Mover el sheet hacia abajo mientras se arrastra
                double newY = Math.Max(0, DetailSheet.TranslationY + e.TotalY);
                DetailSheet.TranslationY = newY;
                // También puedes atenuar el backdrop
                double progress = newY / 400;
                Backdrop.Opacity = 0.45 * (1 - progress);
                break;

            case GestureStatus.Completed:
                if (DetailSheet.TranslationY > 150) // umbral para cerrar
                    await OcultarPanelDetalle();
                else
                {
                    // Volver a posición abierta
                    await Task.WhenAll(
                        Backdrop.FadeTo(0.45, 250, Easing.CubicOut),
                        DetailSheet.TranslateTo(0, 0, 250, Easing.CubicOut)
                    );
                }
                break;
        }
    }

    // Cerrar tocando el backdrop
    private async void OnBackdropTapped(object sender, TappedEventArgs e)
    {
        await OcultarPanelDetalle();
    }
    #endregion

}
