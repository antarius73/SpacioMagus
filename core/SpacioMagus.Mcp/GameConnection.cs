using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using SpacioMagus.Core.Remote;

namespace SpacioMagus.Mcp;

public sealed class GameConnection : IAsyncDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(90);

    private readonly McpSettings _settings;
    private readonly ILogger<GameConnection> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private long _nextId;

    public GameConnection(McpSettings settings, ILogger<GameConnection> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public PlayerView? LastState { get; private set; }

    public async Task<bool> TryConnectAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await EnsureConnectedAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RemoteResponse> SendAsync(Func<long, RemoteRequest> build, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!await EnsureConnectedAsync(cancellationToken))
            {
                throw new McpException(
                    $"Le jeu n'est pas joignable sur 127.0.0.1:{_settings.Port}. Lancez-le avec l'outil lancer_jeu (il doit tourner avec l'option --mcp).");
            }

            var request = build(Interlocked.Increment(ref _nextId));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ResponseTimeout);
            try
            {
                await _writer!.WriteLineAsync(RemoteProtocol.WriteRequest(request).AsMemory(), timeout.Token);
                while (true)
                {
                    var line = await _reader!.ReadLineAsync(timeout.Token)
                        ?? throw new IOException("Le jeu a fermé la connexion.");
                    var response = RemoteProtocol.ReadResponse(line);
                    if (response.Id != request.Id && response.Id != 0)
                    {
                        _logger.LogWarning("Réponse {Id} ignorée : la requête attendue est {Expected}.", response.Id, request.Id);
                        continue;
                    }

                    Remember(response);
                    return response;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Drop();
                throw new McpException($"Le jeu n'a pas répondu en {ResponseTimeout.TotalSeconds:0} s : connexion réinitialisée, relisez l'état.");
            }
            catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException or RemoteProtocolException)
            {
                Drop();
                throw new McpException($"Liaison avec le jeu interrompue : {exception.Message}");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            Drop();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private void Remember(RemoteResponse response)
    {
        if (response.State is { } state)
        {
            LastState = state;
        }
        else if (response.Screen is RemoteScreen.Title or RemoteScreen.LevelSelect)
        {
            LastState = null;
        }
    }

    private async Task<bool> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client is { Connected: true })
        {
            return true;
        }

        Drop();
        var client = new TcpClient { NoDelay = true };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConnectTimeout);
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, _settings.Port, timeout.Token);
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            client.Dispose();
            return false;
        }

        var stream = client.GetStream();
        _client = client;
        _reader = new StreamReader(stream, new UTF8Encoding(false));
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
        _logger.LogInformation("Connecté au jeu sur le port {Port}.", _settings.Port);
        return true;
    }

    private void Drop()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _client?.Dispose();
        _reader = null;
        _writer = null;
        _client = null;
    }
}
