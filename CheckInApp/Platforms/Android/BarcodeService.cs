using Android.Util;
using CheckInApp.Services;

namespace CheckInApp.Platforms.Android
{
    public class BarcodeService : IBarcodeService
    {
        public static BarcodeService Instance { get; private set; }

        public event Action<string>? BarcodeScanned;

        public BarcodeService()
        {
            Instance = this;
            Log.Debug("PA768", "BarcodeService iniciado");
        }

        public void Start()
        {
            Log.Debug("PA768", "Scanner listo");
        }

        public void Stop()
        {
            Log.Debug("PA768", "Scanner detenido");
        }

        public void RaiseBarcode(string barcode)
        {
            BarcodeScanned?.Invoke(barcode);
        }
    }
}