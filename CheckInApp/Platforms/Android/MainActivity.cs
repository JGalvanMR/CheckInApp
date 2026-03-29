using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using CheckInApp.Platforms.Android;

namespace CheckInApp;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize |
                           ConfigChanges.Orientation |
                           ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize |
                           ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // BUG FIX 6: Antes se usaba `new ScanBroadcastReceiver()` que resolvía al receptor
    // del namespace raíz (CheckInApp.ScanBroadcastReceiver) — el antiguo que usaba
    // MessagingCenter (obsoleto y sin suscriptores). Ahora se usa explícitamente
    // CheckInApp.Platforms.Android.ScanBroadcastReceiver, que llama a BarcodeService.
    ScanBroadcastReceiver _scanReceiver;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _scanReceiver = new ScanBroadcastReceiver();
    }

    protected override void OnResume()
    {
        base.OnResume();

        var filter = new IntentFilter("unitech.scanservice.data");

        // FIX CRÍTICO: Android 13+ (API 33) exige declarar si el receptor acepta
        // broadcasts de procesos externos. unitech.scanservice es un proceso externo,
        // por lo que se requiere ReceiverFlags.Exported. Sin este flag en
        // targetSdk >= 33 el intent se descarta silenciosamente — exactamente
        // el síntoma del logcat: tecla de scanner detectada pero ningún
        // "Código recibido" aparecía jamás.
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            RegisterReceiver(_scanReceiver, filter, ReceiverFlags.Exported);
            Log.Debug("PA768", "Receiver registrado EXPORTED (API 33+)");
        }
        else
        {
            RegisterReceiver(_scanReceiver, filter);
            Log.Debug("PA768", "Receiver registrado (API < 33)");
        }
    }

    protected override void OnPause()
    {
        base.OnPause();
        UnregisterReceiver(_scanReceiver);
    }
}