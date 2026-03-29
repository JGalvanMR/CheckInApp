using Microsoft.Extensions.Logging;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;
using CheckInApp.Services;
using CheckInApp.ViewModels;
#if ANDROID
using CheckInApp.Platforms.Android;
#endif

namespace CheckInApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseBarcodeReader()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .ConfigureMauiHandlers(h =>
                {
                    h.AddHandler(typeof(ZXing.Net.Maui.Controls.CameraBarcodeReaderView), typeof(CameraBarcodeReaderViewHandler));
                    h.AddHandler(typeof(ZXing.Net.Maui.Controls.CameraView), typeof(CameraViewHandler));
                    h.AddHandler(typeof(ZXing.Net.Maui.Controls.BarcodeGeneratorView), typeof(BarcodeGeneratorViewHandler));
                });

#if ANDROID
            // BUG FIX 1: BarcodeService debe registrarse ANTES de MainViewModel y MainPage
            // para garantizar que BarcodeService.Instance no sea null cuando el scanner físico dispare.
            builder.Services.AddSingleton<IBarcodeService, BarcodeService>();
#endif

            // BUG FIX 2: MainViewModel y MainPage deben estar en DI para que
            // IBarcodeService se inyecte correctamente en MainPage vía constructor.
            builder.Services.AddSingleton<MainViewModel>();
            builder.Services.AddSingleton<MainPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
