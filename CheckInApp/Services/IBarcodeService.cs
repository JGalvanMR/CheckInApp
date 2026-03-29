namespace CheckInApp.Services;

public interface IBarcodeService
{
    event Action<string>? BarcodeScanned;
    void Start();
    void Stop();
}