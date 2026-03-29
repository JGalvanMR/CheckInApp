using Android.Content;
using Android.Util;
using CheckInApp.Services;

namespace CheckInApp.Platforms.Android
{
    // BUG FIX 5: Se elimina el atributo [BroadcastReceiver(Enabled = true, Exported = true)]
    // porque MainActivity ya hace registro dinámico (OnResume/OnPause).
    // Tener AMBOS (atributo de manifiesto + registro dinámico) con la misma acción
    // causa que el broadcast se procese dos veces, o que el receptor de manifiesto
    // nunca reciba nada en Android 8+ (las implicit broadcasts están bloqueadas en manifiesto).
    // El registro dinámico en MainActivity es suficiente y funciona en todas las versiones.
    public class ScanBroadcastReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            if (intent.Action != "unitech.scanservice.data")
                return;

            var barcode = intent.GetStringExtra("text");

            Log.Debug("PA768", $"Código recibido: {barcode}");

            if (string.IsNullOrEmpty(barcode))
                return;

            // BUG FIX 6 (relacionado): BarcodeService.Instance será no-null aquí porque
            // MauiProgram registra IBarcodeService como Singleton y MainPage lo pide en
            // su constructor, forzando la creación del servicio al arrancar la app.
            BarcodeService.Instance?.RaiseBarcode(barcode);
        }
    }
}
