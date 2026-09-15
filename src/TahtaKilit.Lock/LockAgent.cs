using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using TahtaKilit.Core;
using TahtaKilit.Windows;

namespace TahtaKilit.Lock;

/// <summary>
/// Kullanici oturumunda surekli calisan kilit ajani.
///
/// Kilitliyken ekrani kaplar, acikken gizlenip bosta kalmayi izler. Gizli
/// anahtari hicbir zaman gormez: girilen sifreyi servise iletir, "acildi /
/// yanlis / bekle" cevabini alir.
/// </summary>
internal sealed class LockAgent : IDisposable
{
    /// <summary>HWND_MESSAGE: yalnizca ileti alan, gorunmeyen pencere.</summary>
    private static readonly IntPtr MessageOnlyWindow = new(-3);

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly LockPipeClient _client = new();
    /// <summary>Acik kilit pencereleri ve kapladiklari ekranlar.</summary>
    private readonly List<(Window Window, ScreenBounds Bounds)> _windows = [];

    private HwndSource? _messageWindow;
    private GlobalHotkey? _hotkey;
    private LockScreenWindow? _screen;
    private KeyboardBlocker? _blocker;
    private bool _locked;
    private bool _busy;
    private int _idleLockMinutes;
    private bool _closing;

    public void Start()
    {
        // Onceki calisma duzgun kapanamadiysa (oturum kapatma, zorla
        // sonlandirma) Gorev Yoneticisi kapali kalmis olabilir; once onu temizle.
        TaskManagerPolicy.Restore();

        // Surec beklenmedik sekilde biterse de politikayi geri almayi dene.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => TaskManagerPolicy.Restore();

        CreateMessageWindow();

        _timer.Tick += async (_, _) => await TickAsync();
        _timer.Start();
    }

    /// <summary>
    /// Elle kilitleme kisayolunu (Ctrl+Alt+L) alabilmek icin gorunmeyen bir
    /// pencere olusturur. Kilit ekrani gizliyken de calismalidir.
    /// </summary>
    private void CreateMessageWindow()
    {
        _messageWindow = new HwndSource(new HwndSourceParameters("TahtaKilitAjan")
        {
            Width = 0,
            Height = 0,
            ParentWindow = MessageOnlyWindow,
        });

        _messageWindow.AddHook(WndProc);
        _hotkey = new GlobalHotkey(_messageWindow.Handle);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_hotkey?.Matches(msg, wParam) == true)
        {
            handled = true;
            _ = RequestLockAsync();
        }

        return IntPtr.Zero;
    }

    private async Task TickAsync()
    {
        if (_busy || _closing)
            return;

        _busy = true;
        try
        {
            if (!await EnsureConnectedAsync())
                return;

            var status = await _client.SendAsync(new LockRequest(LockRequest.Durum));
            if (status is null)
            {
                _client.Dispose();
                return;
            }

            Apply(status);
            await CheckIdleAsync();
        }
        catch (Exception e) when (e is IOException or TimeoutException or InvalidOperationException)
        {
            // Servis yeniden basliyor olabilir; bir sonraki turda tekrar denenir.
            _client.Dispose();
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task<bool> EnsureConnectedAsync()
    {
        if (_client.IsConnected)
            return true;

        try
        {
            await _client.ConnectAsync(timeoutMs: 2000);
            return true;
        }
        catch (TimeoutException)
        {
            // Servis henuz hazir degil. Guvenli taraf: ekrani kapali tut.
            ShowLock();
            _screen?.ShowServiceWaiting();
            return false;
        }
    }

    private void Apply(LockStatus status)
    {
        _idleLockMinutes = status.IdleLockMinutes;

        if (status.Result == LockStatus.Acildi)
        {
            HideLock();
            return;
        }

        ShowLock();
        _screen?.Update(status);
        KeepOnTop();
    }

    /// <summary>
    /// Bosta kalinca kilitlenme. Olcum yalnizca kullanici oturumunda
    /// anlamli oldugu icin burada yapilir, serviste degil.
    /// </summary>
    private async Task CheckIdleAsync()
    {
        if (_locked || _idleLockMinutes <= 0)
            return;

        if (IdleTime.Elapsed() >= TimeSpan.FromMinutes(_idleLockMinutes))
            await RequestLockAsync();
    }

    private async Task RequestLockAsync()
    {
        try
        {
            if (!_client.IsConnected)
                await _client.ConnectAsync(timeoutMs: 2000);

            if (await _client.SendAsync(new LockRequest(LockRequest.Kilitle)) is { } status)
                Apply(status);
        }
        catch (Exception e) when (e is IOException or TimeoutException or InvalidOperationException)
        {
            _client.Dispose();
        }
    }

    private async Task<LockStatus?> SendUnlockAsync(string response)
    {
        try
        {
            return await _client.SendAsync(new LockRequest(LockRequest.Ac, response));
        }
        catch (Exception e) when (e is IOException or InvalidOperationException)
        {
            _client.Dispose();
            return null;
        }
    }

    // ------------------------------------------------------------ pencereler

    private void ShowLock()
    {
        if (_locked)
            return;

        _locked = true;

        var screens = ScreenLayout.All();

        // Ana ekran (0,0) noktasindan baslar; bulunamazsa ilk ekran kullanilir.
        var primary = screens.Count == 0
            ? new ScreenBounds(0, 0, 1024, 768)
            : screens.FirstOrDefault(s => s.Left == 0 && s.Top == 0, screens[0]);

        _screen = new LockScreenWindow(SendUnlockAsync);
        Place(_screen, primary, activate: true);
        _windows.Add((_screen, primary));

        // Diger ekranlar siyah ortulur; kilitliyken hicbir icerik gorunmemeli.
        foreach (var bounds in screens.Where(s => s != primary))
        {
            var blank = new BlankWindow();
            Place(blank, bounds, activate: false);
            _windows.Add((blank, bounds));
        }

        ApplyProtections();
        _screen.FocusEntry();
    }

    private static void Place(Window window, ScreenBounds bounds, bool activate)
    {
        window.Show();

        var handle = new WindowInteropHelper(window).Handle;
        ScreenLayout.ClearMaximizedStyle(handle);
        ScreenLayout.Fill(handle, bounds, activate);
    }

    /// <summary>
    /// Baska bir program one cikarsa kilit ekranini geri getirir. Her turda
    /// calisir; watchdog'un pencere seviyesindeki karsiligidir.
    /// </summary>
    private void KeepOnTop()
    {
        foreach (var (window, bounds) in _windows)
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
                ScreenLayout.Fill(handle, bounds, activate: window == _screen);
        }
    }

    private void HideLock()
    {
        if (!_locked)
            return;

        _locked = false;
        RemoveProtections();

        foreach (var (window, _) in _windows)
        {
            if (window is LockScreenWindow screen)
                screen.AllowClose();
            else if (window is BlankWindow blank)
                blank.AllowClose();

            window.Close();
        }

        _windows.Clear();
        _screen = null;
    }

    private void ApplyProtections()
    {
        _blocker ??= new KeyboardBlocker();
        TaskManagerPolicy.Disable();
    }

    private void RemoveProtections()
    {
        _blocker?.Dispose();
        _blocker = null;

        TaskManagerPolicy.Restore();
    }

    public void Dispose()
    {
        _closing = true;
        _timer.Stop();

        // Kilit acikken cikiliyorsa politika geri alinir; kilitliyken zaten
        // servis ajani yeniden baslatacaktir.
        RemoveProtections();

        _hotkey?.Dispose();
        _messageWindow?.Dispose();
        _client.Dispose();
    }
}
