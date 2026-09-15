using TahtaKilit.Core;
using Xunit;

namespace TahtaKilit.Core.Tests;

public class LockScheduleTests
{
    // 2026-09-14 Pazartesi, 2026-09-19 Cumartesi
    private static DateTime Pazartesi(int saat, int dakika = 0) => new(2026, 9, 14, saat, dakika, 0);
    private static DateTime Cumartesi(int saat, int dakika = 0) => new(2026, 9, 19, saat, dakika, 0);

    [Fact]
    public void Takvim_bossa_kural_isletilmez()
    {
        Assert.False(new LockSchedule().MustLockAt(Pazartesi(3)));
    }

    [Theory]
    [InlineData(8, 0)]    // ders saati
    [InlineData(7, 30)]   // aralik basi dahil
    [InlineData(16, 59)]  // aralik sonuna kadar
    public void Ders_saatinde_kilitlenmez(int saat, int dakika)
    {
        Assert.False(LockSchedule.WeekdaysSchoolHours().MustLockAt(Pazartesi(saat, dakika)));
    }

    [Theory]
    [InlineData(7, 29)]   // aralik baslamadan once
    [InlineData(17, 0)]   // aralik sonu haric
    [InlineData(22, 0)]
    [InlineData(3, 0)]
    public void Ders_saati_disinda_kilitlenir(int saat, int dakika)
    {
        Assert.True(LockSchedule.WeekdaysSchoolHours().MustLockAt(Pazartesi(saat, dakika)));
    }

    [Fact]
    public void Hafta_sonu_gun_boyu_kilitlenir()
    {
        Assert.True(LockSchedule.WeekdaysSchoolHours().MustLockAt(Cumartesi(10)));
    }

    [Fact]
    public void Gece_yarisini_asan_aralik_dogru_calisir()
    {
        // Pazartesi 22:00 - Sali 06:00 (gece nobetci sinifi gibi bir durum)
        var takvim = new LockSchedule(
            [new AllowedWindow([DayOfWeek.Monday], new TimeOnly(22, 0), new TimeOnly(6, 0))]);

        Assert.False(takvim.MustLockAt(new DateTime(2026, 9, 14, 23, 0, 0))); // Pzt gece
        Assert.False(takvim.MustLockAt(new DateTime(2026, 9, 15, 5, 0, 0)));  // Sali sabaha karsi
        Assert.True(takvim.MustLockAt(new DateTime(2026, 9, 15, 7, 0, 0)));   // Sali sabah
        Assert.True(takvim.MustLockAt(new DateTime(2026, 9, 16, 5, 0, 0)));   // Carsamba sabaha karsi
    }

    [Fact]
    public void Saat_geriye_kaymissa_takvim_isletilmez()
    {
        // Tahtanin saati sifirlanip 2020'ye dusmusse takvime gore
        // kilitlemek anlamsizdir; ders ortasinda kilitlenmesin.
        var takvim = LockSchedule.WeekdaysSchoolHours();
        var sonBilinen = Pazartesi(9);
        var kaymisSaat = new DateTime(2020, 1, 1, 3, 0, 0);

        Assert.True(takvim.MustLockAt(kaymisSaat));                 // saat bilgisi olmadan kilitlerdi
        Assert.False(takvim.MustLockAt(kaymisSaat, sonBilinen));    // kayma bilinince kilitlemez
    }

    [Fact]
    public void Kucuk_saat_farki_kayma_sayilmaz()
    {
        // Birkac dakikalik sapma normaldir; takvim isletilmeye devam eder.
        var takvim = LockSchedule.WeekdaysSchoolHours();
        var sonBilinen = Pazartesi(22, 5);

        Assert.True(takvim.MustLockAt(Pazartesi(22, 0), sonBilinen));
    }

    [Fact]
    public void Saat_ileri_giderse_kayma_sayilmaz()
    {
        // Normal ilerleme: saat her zaman ileri gider.
        Assert.False(LockSchedule.IsClockSuspicious(Pazartesi(10), Pazartesi(9)));
    }

    [Fact]
    public void Takvim_asla_kilit_acmaz()
    {
        // Sozlesme: MustLockAt yalnizca "kilitle" diyebilir. Ders saatinde
        // false doner (kilitleme), ama bu "ac" demek degildir — servis bunu
        // hicbir zaman kilit acmak icin kullanmaz.
        var takvim = LockSchedule.WeekdaysSchoolHours();

        Assert.False(takvim.MustLockAt(Pazartesi(9)));
    }
}

public class BoardConfigTests
{
    private sealed class FakeProtector : IDataProtector
    {
        public bool Bozuk { get; set; }

        // Gercek sifreleme degil; sadece "duz metin diske yazilmasin" davranisini taklit eder.
        public byte[] Protect(byte[] plain) => plain.Select(b => (byte)(b ^ 0x5A)).ToArray();

        public byte[]? Unprotect(byte[] data) =>
            Bozuk ? null : data.Select(b => (byte)(b ^ 0x5A)).ToArray();
    }

    private static BoardConfig OrnekConfig() => new()
    {
        BoardName = "Z-Blok 204",
        IdleLockMinutes = 10,
    };

    [Fact]
    public void Yapilandirma_diske_yazilip_geri_okunur()
    {
        var yol = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
        var store = new ConfigStore(yol, new FakeProtector());
        var config = OrnekConfig();
        config.SetSchedule(LockSchedule.WeekdaysSchoolHours());

        try
        {
            Assert.False(store.Exists);
            store.Save(config);

            var geri = store.Load();

            Assert.NotNull(geri);
            Assert.Equal(config.BoardName, geri.BoardName);
            Assert.Equal(10, geri.IdleLockMinutes);
            Assert.Single(geri.ToSchedule().Windows);
            Assert.Equal(new TimeOnly(7, 30), geri.ToSchedule().Windows[0].Start);
        }
        finally
        {
            File.Delete(yol);
        }
    }

    [Fact]
    public void Ayarlar_diske_duz_metin_olarak_yazilmaz()
    {
        // Sir yok, ama ayarlar da duz metin durmamali: tahta adini degistirmek
        // dosyayi bir metin duzenleyiciyle acmak kadar kolay olmasin.
        var yol = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
        var config = OrnekConfig();

        try
        {
            new ConfigStore(yol, new FakeProtector()).Save(config);

            var ham = File.ReadAllBytes(yol);
            var duz = System.Text.Encoding.UTF8.GetBytes(config.BoardName);

            Assert.False(Bulunuyor(ham, duz), "Tahta adi dosyada duz halde gorunuyor.");
        }
        finally
        {
            File.Delete(yol);
        }

        static bool Bulunuyor(byte[] saman, byte[] igne) =>
            Enumerable.Range(0, saman.Length - igne.Length + 1)
                .Any(i => saman.Skip(i).Take(igne.Length).SequenceEqual(igne));
    }

    [Fact]
    public void Cozulemeyen_dosya_null_doner()
    {
        // Baska bir makineye kopyalanan dosya DPAPI ile cozulemez.
        var yol = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
        var protector = new FakeProtector();

        try
        {
            new ConfigStore(yol, protector).Save(OrnekConfig());
            protector.Bozuk = true;

            Assert.Null(new ConfigStore(yol, protector).Load());
        }
        finally
        {
            File.Delete(yol);
        }
    }

    [Fact]
    public void Dosya_yoksa_null_doner()
    {
        var yol = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
        Assert.Null(new ConfigStore(yol, new FakeProtector()).Load());
    }
}
