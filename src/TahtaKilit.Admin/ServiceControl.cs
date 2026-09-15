using System.Runtime.Versioning;
using System.ServiceProcess;

namespace TahtaKilit.Admin;

/// <summary>Kilit servisini durdurup baslatir.</summary>
[SupportedOSPlatform("windows")]
internal static class ServiceControl
{
    public const string ServiceName = "TahtaKilit";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>Servis kurulu mu?</summary>
    public static bool IsInstalled()
    {
        try
        {
            using var service = new ServiceController(ServiceName);
            _ = service.Status;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Servisi durdurur. Basarisiz olursa hata metnini dondurur.</summary>
    public static string? Stop()
    {
        try
        {
            using var service = new ServiceController(ServiceName);
            if (service.Status == ServiceControllerStatus.Stopped)
                return null;

            service.Stop();
            service.WaitForStatus(ServiceControllerStatus.Stopped, Timeout);
            return null;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ServiceProcess.TimeoutException)
        {
            return e.Message;
        }
    }

    /// <summary>Servisi baslatir. Basarisiz olursa hata metnini dondurur.</summary>
    public static string? Start()
    {
        try
        {
            using var service = new ServiceController(ServiceName);
            if (service.Status == ServiceControllerStatus.Running)
                return null;

            service.Start();
            service.WaitForStatus(ServiceControllerStatus.Running, Timeout);
            return null;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ServiceProcess.TimeoutException)
        {
            return e.Message;
        }
    }
}
