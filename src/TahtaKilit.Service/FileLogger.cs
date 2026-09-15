using System.Text;
using TahtaKilit.Windows;

namespace TahtaKilit.Service;

/// <summary>
/// Tahtadaki metin dosyasina yazan basit gunlukcu. Okulda BT gorevlisi
/// "kim ne zaman acti, kac kez yanlis girildi" sorusunu buradan yanitlar.
/// Dosya belirli bir boyutu asinca bir kez donduruluyor.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly object Gate = new();

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName);

    public void Dispose() { }

    private static void Write(string line)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(WindowsPaths.DataDirectory);

                var path = WindowsPaths.LogFile;
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                    File.Move(path, path + ".1", overwrite: true);

                File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
            }
            catch (IOException)
            {
                // Gunluk yazilamiyorsa kilit calismaya devam etmeli.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class FileLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var mesaj = formatter(state, exception);
            var satir = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {category}: {mesaj}";

            if (exception is not null)
                satir += Environment.NewLine + exception;

            Write(satir);
        }
    }
}
