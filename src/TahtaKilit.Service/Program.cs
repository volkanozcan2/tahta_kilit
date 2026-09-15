using Microsoft.Extensions.Hosting.WindowsServices;
using TahtaKilit.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "TahtaKilit");
builder.Services.AddHostedService<LockWorker>();

// Olaylar hem Windows olay gunlugune hem de tahtadaki metin dosyasina yazilir.
if (WindowsServiceHelpers.IsWindowsService())
    builder.Logging.AddEventLog(settings => settings.SourceName = "TahtaKilit");

builder.Logging.AddProvider(new FileLoggerProvider());

await builder.Build().RunAsync();
