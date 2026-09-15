using System.Windows;

namespace TahtaKilit.Lock;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Oturum basina tek ornek: servisin watchdog'u yanlislikla ikinci bir
        // kilit ekrani baslatirsa ikincisi hemen cikar.
        using var mutex = new Mutex(initiallyOwned: true, @"Local\TahtaKilit.Lock", out var ilkOrnek);
        if (!ilkOrnek)
            return;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var agent = new LockAgent();

        app.Startup += (_, _) => agent.Start();
        app.Exit += (_, _) => agent.Dispose();

        app.Run();
    }
}
