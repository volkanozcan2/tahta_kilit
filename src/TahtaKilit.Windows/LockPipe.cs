using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using TahtaKilit.Core;

namespace TahtaKilit.Windows;

/// <summary>
/// Kilit ekrani (kullanici oturumu) ile servis (SYSTEM) arasindaki
/// adlandirilmis boru sunucusu.
///
/// Gizli anahtar yalnizca serviste durur; boru uzerinden yalnizca cagri,
/// girilen cevap ve sonuc gecer. Kilit ekrani ele gecirilse bile anahtar
/// sizmaz.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class LockPipeServer(Func<LockRequest, LockStatus> handler) : IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    /// <summary>Istekleri karsilamaya baslar; iptal edilene kadar surer.</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

        while (!linked.IsCancellationRequested)
        {
            try
            {
                await ServeOneAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // Kilit ekrani kapandi; yeni baglanti bekle.
            }
        }
    }

    private async Task ServeOneAsync(CancellationToken cancellationToken)
    {
        using var server = NamedPipeServerStreamAcl.Create(
            WindowsPaths.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            CreateSecurity());

        await server.WaitForConnectionAsync(cancellationToken);

        using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

        while (server.IsConnected && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;

            var request = LockMessages.ParseRequest(line) ?? new LockRequest(LockRequest.Durum);
            await writer.WriteLineAsync(LockMessages.Serialize(handler(request)));
        }
    }

    /// <summary>
    /// Boruya yalnizca yerel oturum acmis kullanicilar ve yoneticiler baglanabilir.
    /// Aga aciklik yoktur; boru yerel makineyle sinirlidir.
    /// </summary>
    private static PipeSecurity CreateSecurity()
    {
        var security = new PipeSecurity();

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.InteractiveSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return security;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}

/// <summary>Kilit ekraninin servise baglanmak icin kullandigi istemci.</summary>
[SupportedOSPlatform("windows")]
public sealed class LockPipeClient : IDisposable
{
    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public bool IsConnected => _pipe?.IsConnected ?? false;

    public async Task ConnectAsync(int timeoutMs = 5000, CancellationToken cancellationToken = default)
    {
        Dispose();

        _pipe = new NamedPipeClientStream(
            ".", WindowsPaths.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        await _pipe.ConnectAsync(timeoutMs, cancellationToken);

        _reader = new StreamReader(_pipe, Encoding.UTF8, leaveOpen: true);
        _writer = new StreamWriter(_pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
    }

    /// <summary>Servise bir istek gonderip cevabini bekler.</summary>
    public async Task<LockStatus?> SendAsync(LockRequest request, CancellationToken cancellationToken = default)
    {
        if (_writer is null || _reader is null)
            throw new InvalidOperationException("Once ConnectAsync cagrilmali.");

        await _writer.WriteLineAsync(LockMessages.Serialize(request).AsMemory(), cancellationToken);

        var line = await _reader.ReadLineAsync(cancellationToken);
        return line is null ? null : LockMessages.ParseStatus(line);
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _pipe?.Dispose();

        _reader = null;
        _writer = null;
        _pipe = null;
    }
}
