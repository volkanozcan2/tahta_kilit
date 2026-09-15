using System.Diagnostics;
using System.Runtime.Versioning;
using TahtaKilit.Core;
using TahtaKilit.Windows;

namespace TahtaKilit.Service;

/// <summary>
/// Servisin ana dongusu. SYSTEM olarak calisir ve uc isi yapar:
///
/// 1. Dogrulama: gizli anahtar yalnizca burada durur; kilit ekrani cevabi
///    boru uzerinden gonderir, sonucu alir.
/// 2. Watchdog: kilit ekrani oldurulurse bir saniye icinde geri getirir.
/// 3. Takvim: ders saati disinda tahtayi kilitler (asla acmaz).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LockWorker(ILogger<LockWorker> logger) : BackgroundService
{
    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ClockSaveInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ConfigPollInterval = TimeSpan.FromSeconds(10);

    private readonly ConfigStore _store = new(WindowsPaths.ConfigFile, new DpapiProtector());

    private BoardConfig? _config;
    private LockCoordinator? _coordinator;
    private int? _agentPid;
    private DateTime _lastClockSave = DateTime.MinValue;
    private DateTime _configStamp;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Tahta Kilit servisi basladi.");

        _config = await WaitForConfigAsync(stoppingToken);
        if (_config is null)
            return;

        logger.LogInformation("Tahta: {Ad}", _config.BoardName);

        _configStamp = ConfigStamp();
        _coordinator = new LockCoordinator(_config);

        using var pipe = new LockPipeServer(HandleRequest);
        var serving = pipe.RunAsync(stoppingToken);

        await WatchdogLoopAsync(stoppingToken);
        await serving;
    }

    /// <summary>
    /// Kurulum sihirbazi henuz calistirilmadiysa yapilandirma yoktur.
    /// Servis hata vermez; yapilandirma gorunene kadar bekler.
    /// </summary>
    private async Task<BoardConfig?> WaitForConfigAsync(CancellationToken stoppingToken)
    {
        var uyarildi = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_store.Load() is { } config)
                return config;

            if (!uyarildi)
            {
                logger.LogWarning("Yapilandirma bulunamadi. Kurulum sihirbazi bekleniyor: {Yol}", _store.Path);
                uyarildi = true;
            }

            await Task.Delay(ConfigPollInterval, stoppingToken).ConfigureAwait(false);
        }

        return null;
    }

    private LockStatus HandleRequest(LockRequest request)
    {
        var status = _coordinator!.Handle(request);

        switch (status.Result)
        {
            case LockStatus.Acildi when request.Op == LockRequest.Ac:
                logger.LogInformation("Kilit acildi.");
                break;

            case LockStatus.Yanlis:
                logger.LogWarning("Yanlis sifre girildi.");
                break;

            case LockStatus.Kilitli when request.Op == LockRequest.Kilitle:
                logger.LogInformation("Tahta kilitlendi.");
                break;
        }

        return status;
    }

    private async Task WatchdogLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ReloadConfigIfChanged();
                EnsureAgentRunning();
                ApplySchedule();
                SaveClock();
            }
            catch (Exception e)
            {
                // Watchdog hicbir kosulda durmamali; durursa tahta savunmasiz kalir.
                logger.LogError(e, "Watchdog dongusunde hata.");
            }

            await Task.Delay(WatchdogInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private DateTime ConfigStamp() =>
        File.Exists(_store.Path) ? File.GetLastWriteTimeUtc(_store.Path) : DateTime.MinValue;

    /// <summary>
    /// Kurulum sihirbazi ayarlari degistirdiginde servisin yeniden
    /// baslatilmasi gerekmesin diye dosya degisimi izlenir.
    /// </summary>
    private void ReloadConfigIfChanged()
    {
        var stamp = ConfigStamp();
        if (stamp == _configStamp)
            return;

        _configStamp = stamp;

        if (_store.Load() is not { } yeni)
        {
            logger.LogWarning("Yapilandirma degismis ama okunamadi; eski ayarlar kullanilmaya devam ediyor.");
            return;
        }

        _config = yeni;
        logger.LogInformation("Ayarlar guncellendi.");
    }

    /// <summary>
    /// Kilit ajani kullanici oturumunda surekli calisir: kilitliyken ekrani
    /// kaplar, acikken gizlenip bosta kalmayi izler. Oldurulurse geri getirilir.
    /// </summary>
    private void EnsureAgentRunning()
    {
        if (SessionLauncher.ActiveSessionId() is null)
        {
            _agentPid = null; // oturum kapali; ajan gerekmiyor
            return;
        }

        if (_agentPid is { } pid && IsAlive(pid))
            return;

        if (_agentPid is not null)
            logger.LogWarning("Kilit ekrani kapatilmis; yeniden baslatiliyor.");

        var yol = Path.Combine(AppContext.BaseDirectory, "TahtaKilit.Lock.exe");
        if (!File.Exists(yol))
        {
            logger.LogError("Kilit ekrani bulunamadi: {Yol}", yol);
            return;
        }

        try
        {
            _agentPid = SessionLauncher.LaunchInActiveSession(yol);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Kilit ekrani baslatilamadi.");
            _agentPid = null;
        }
    }

    private static bool IsAlive(int pid)
    {
        try
        {
            return !Process.GetProcessById(pid).HasExited;
        }
        catch (ArgumentException)
        {
            return false; // surec yok
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Ders saati takvimi. Yalnizca kilitler; kilidi asla acmaz. Saat
    /// kaymissa takvim isletilmez (bkz. <see cref="LockSchedule"/>).
    /// </summary>
    private void ApplySchedule()
    {
        if (_coordinator is null || !_coordinator.IsUnlocked)
            return;

        if (_config!.ToSchedule().MustLockAt(DateTime.Now, _config.LastKnownTime))
        {
            logger.LogInformation("Ders saati disina cikildi; tahta kilitleniyor.");
            _coordinator.Lock();
        }
    }

    /// <summary>
    /// Son bilinen zamani ara sira diske yazar. Saat geriye alinirsa bu deger
    /// sayesinde anlasilir; takvim o durumda isletilmez.
    /// </summary>
    private void SaveClock()
    {
        var now = DateTime.Now;
        if (now - _lastClockSave < ClockSaveInterval)
            return;

        _lastClockSave = now;

        if (LockSchedule.IsClockSuspicious(now, _config!.LastKnownTime))
            logger.LogWarning("Sistem saati geriye gitmis gorunuyor; takvim kurali gecici olarak isletilmiyor.");

        _config.LastKnownTime = now;
        SaveConfig();
    }

    private void SaveConfig()
    {
        try
        {
            _store.Save(_config!);

            // Kendi yazdigimiz degisiklik "disaridan degisti" sayilmamali.
            _configStamp = ConfigStamp();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Yapilandirma yazilamadi.");
        }
    }
}
