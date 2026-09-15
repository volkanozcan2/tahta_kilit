using System.Windows;

namespace TahtaKilit.Admin;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.Run(new AdminWindow());
    }
}
