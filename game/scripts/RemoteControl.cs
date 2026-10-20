using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Godot;
using SpacioMagus.Core;
using SpacioMagus.Core.Remote;

namespace SpacioMagus;

public partial class RemoteControl : Node
{
    public const int DefaultPort = 47821;
    public const int DefaultDelayMs = 900;

    private const string Flag = "--mcp";
    private const string PortPrefix = "--mcp-port=";
    private const string DelayPrefix = "--mcp-delai=";
    private const int MaxLineBytes = 64 * 1024;
    private const double DelayJitter = 0.25;

    private readonly List<byte> _buffer = new();
    private readonly byte[] _chunk = new byte[4096];
    private Main _main = null!;
    private int _port = DefaultPort;
    private double _delay = DefaultDelayMs / 1000.0;
    private TcpListener? _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private Pending? _pending;

    private sealed class Pending
    {
        public Pending(RemoteRequest request, double wait, Func<RemoteResponse?> execute)
        {
            Request = request;
            Wait = wait;
            Execute = execute;
        }

        public RemoteRequest Request { get; }

        public double Wait { get; set; }

        public Func<RemoteResponse?> Execute { get; }

        public Func<RemoteResponse>? Complete { get; set; }

        public bool Executed { get; set; }

        public int FramesSinceExecution { get; set; }
    }

    public static void AttachIfRequested(Main main)
    {
        var args = OS.GetCmdlineUserArgs();
        if (!args.Contains(Flag))
        {
            return;
        }

        var remote = new RemoteControl
        {
            Name = "RemoteControl",
            ProcessMode = ProcessModeEnum.Always,
            _main = main,
            _port = ReadInt(args, PortPrefix, DefaultPort, 1, 65535),
            _delay = ReadInt(args, DelayPrefix, DefaultDelayMs, 0, 60000) / 1000.0,
        };
        main.AddChild(remote);
    }

    public override void _Ready()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            GD.Print($"Pilotage MCP : écoute sur 127.0.0.1:{_port}, délai {_delay * 1000:0} ms.");
        }
        catch (SocketException exception)
        {
            GD.PushError($"Pilotage MCP : port {_port} indisponible ({exception.Message}).");
            _listener = null;
        }
    }

    public override void _ExitTree()
    {
        DropClient();
        _listener?.Stop();
        _listener = null;
    }

    public override void _Process(double delta)
    {
        if (_listener is null)
        {
            return;
        }

        AcceptClient();
        ReadRequests();
        AdvancePending(delta);
    }

    private static int ReadInt(IEnumerable<string> args, string prefix, int fallback, int min, int max)
    {
        var argument = args.LastOrDefault(arg => arg.StartsWith(prefix, StringComparison.Ordinal));
        if (argument is null)
        {
            return fallback;
        }

        if (int.TryParse(argument[prefix.Length..], out var value) && value >= min && value <= max)
        {
            return value;
        }

        GD.PushWarning($"Pilotage MCP : « {argument} » invalide (attendu {min} à {max}), valeur par défaut {fallback}.");
        return fallback;
    }

    private void AcceptClient()
    {
        while (_listener!.Pending())
        {
            var client = _listener.AcceptTcpClient();
            if (_client is not null)
            {
                GD.Print("Pilotage MCP : nouvelle connexion, l'ancienne est fermée.");
                DropClient();
            }

            client.NoDelay = true;
            _client = client;
            _stream = client.GetStream();
            GD.Print("Pilotage MCP : client connecté.");
        }
    }

    private void DropClient()
    {
        _pending = null;
        _buffer.Clear();
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
    }

    private void ReadRequests()
    {
        if (_client is null || _stream is null)
        {
            return;
        }

        try
        {
            if (_client.Client.Poll(0, SelectMode.SelectRead) && _client.Available == 0)
            {
                GD.Print("Pilotage MCP : client déconnecté.");
                DropClient();
                return;
            }

            while (_stream.DataAvailable)
            {
                var read = _stream.Read(_chunk, 0, _chunk.Length);
                if (read == 0)
                {
                    DropClient();
                    return;
                }

                _buffer.AddRange(_chunk.AsSpan(0, read).ToArray());
            }
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException)
        {
            GD.PushWarning($"Pilotage MCP : lecture interrompue ({exception.Message}).");
            DropClient();
            return;
        }

        int newline;
        while ((newline = _buffer.IndexOf((byte)'\n')) >= 0)
        {
            var line = Encoding.UTF8.GetString(_buffer.GetRange(0, newline).ToArray()).Trim();
            _buffer.RemoveRange(0, newline + 1);
            if (line.Length > 0)
            {
                Handle(line);
            }

            if (_client is null)
            {
                return;
            }
        }

        if (_buffer.Count > MaxLineBytes)
        {
            GD.PushWarning("Pilotage MCP : requête trop longue, connexion fermée.");
            DropClient();
        }
    }

    private void Handle(string line)
    {
        RemoteRequest request;
        try
        {
            request = RemoteProtocol.ReadRequest(line);
        }
        catch (RemoteProtocolException exception)
        {
            Send(RemoteResponse.Failure(exception.Id, _main.CurrentScreen, exception.Error, exception.Message, CaptureState()));
            return;
        }

        if (_pending is not null)
        {
            Send(RemoteResponse.Failure(request.Id, _main.CurrentScreen, RemoteErrors.Busy, "Un coup est déjà en cours d'exécution."));
            return;
        }

        try
        {
            Dispatch(request);
        }
        catch (Exception exception)
        {
            GD.PushError($"Pilotage MCP : {request.Command} a échoué : {exception}");
            _pending = null;
            Send(RemoteResponse.Failure(request.Id, _main.CurrentScreen, RemoteErrors.Internal, exception.Message, CaptureState()));
        }
    }

    private void Dispatch(RemoteRequest request)
    {
        switch (request.Command)
        {
            case RemoteCommands.State:
                Send(RemoteResponse.Success(request.Id, _main.CurrentScreen, CaptureState()));
                break;
            case RemoteCommands.Levels:
                Send(new RemoteResponse(request.Id, true, _main.CurrentScreen, State: CaptureState(), Levels: _main.RemoteLevels()));
                break;
            case RemoteCommands.Start:
                Answer(request, _main.RemoteStart(request.Level!));
                break;
            case RemoteCommands.Resume:
                Answer(request, _main.RemoteResume());
                break;
            case RemoteCommands.Play:
                BeginPlay(request);
                break;
            case RemoteCommands.Ui:
                BeginUi(request);
                break;
            default:
                Send(RemoteResponse.Failure(request.Id, _main.CurrentScreen, RemoteErrors.UnknownCommand, $"Commande « {request.Command} » non gérée."));
                break;
        }
    }

    private void Answer(RemoteRequest request, (string Error, string Message)? failure)
    {
        Send(failure is { } error
            ? RemoteResponse.Failure(request.Id, _main.CurrentScreen, error.Error, error.Message, CaptureState())
            : RemoteResponse.Success(request.Id, _main.CurrentScreen, CaptureState()));
    }

    private double HumanDelay() => _delay * (1.0 + (Random.Shared.NextDouble() * 2.0 - 1.0) * DelayJitter);

    private void BeginPlay(RemoteRequest request)
    {
        if (Refusal(request, request.Turn) is { } refusal)
        {
            Send(refusal);
            return;
        }

        var game = _main.CurrentGame!;
        var turn = game.Turn;
        Pending? pending = null;
        pending = new Pending(request, HumanDelay(), () =>
        {
            if (!ReferenceEquals(_main.CurrentGame, game))
            {
                return RemoteResponse.Failure(request.Id, _main.CurrentScreen, RemoteErrors.StaleTurn, "La partie a changé pendant l'attente.", CaptureState());
            }

            if (Refusal(request, turn) is { } late)
            {
                return late;
            }

            var result = _main.RemotePlay(request.Action!);
            if (!result.Success)
            {
                return RemoteResponse.Refused(request.Id, _main.CurrentScreen, result.Error, CaptureState());
            }

            pending!.Complete = () => RemoteResponse.Success(
                request.Id,
                _main.CurrentScreen,
                PlayerView.Capture(game),
                EventText.Describe(result.Events, game));
            return null;
        });
        _pending = pending;
    }

    private void BeginUi(RemoteRequest request)
    {
        var id = request.Control!;
        var entry = CurrentUi().Find(id);
        if (entry is null)
        {
            Send(RemoteResponse.Failure(request.Id, _main.CurrentScreen, RemoteErrors.ControlNotFound, $"Contrôle « {id} » absent de l'écran actuel.", CaptureState()));
            return;
        }

        if (RemoteUi.Validate(entry.Control, request.Value) is { } invalid)
        {
            Send(RemoteResponse.Failure(request.Id, _main.CurrentScreen, invalid.Error, invalid.Message, CaptureState()));
            return;
        }

        if (entry.Focus is { FocusMode: not Control.FocusModeEnum.None } focus)
        {
            focus.GrabFocus();
        }

        Pending? pending = null;
        pending = new Pending(request, HumanDelay(), () =>
        {
            var current = CurrentUi().Find(id);
            if (current is null)
            {
                return RemoteResponse.Failure(request.Id, _main.CurrentScreen, RemoteErrors.ControlNotFound, $"Le contrôle « {id} » a disparu pendant l'attente.", CaptureState());
            }

            if (RemoteUi.Validate(current.Control, request.Value) is { } late)
            {
                return RemoteResponse.Failure(request.Id, _main.CurrentScreen, late.Error, late.Message, CaptureState());
            }

            if (current.Use(request.Value) is { } failure)
            {
                return RemoteResponse.Failure(request.Id, _main.CurrentScreen, failure.Error, failure.Message, CaptureState());
            }

            pending!.Complete = () => RemoteResponse.Success(request.Id, _main.CurrentScreen, CaptureState());
            return null;
        });
        _pending = pending;
    }

    private RemoteUi CurrentUi() => RemoteUi.Scan(_main.RemoteUiRoot, _main.RemoteNotes());

    private RemoteResponse? Refusal(RemoteRequest request, int? expectedTurn)
    {
        var game = _main.CurrentGame;
        var screen = _main.CurrentScreen;
        if (game is null)
        {
            return RemoteResponse.Failure(request.Id, screen, RemoteErrors.NoGame, "Aucune partie en cours : utilisez « start » ou « resume ».");
        }

        if (screen != RemoteScreen.Playing)
        {
            return RemoteResponse.Failure(request.Id, screen, RemoteErrors.ScreenOpen, $"Un écran est ouvert ({screen}) : la partie n'accepte pas de coup.", CaptureState());
        }

        if (expectedTurn is { } turn && turn != game.Turn)
        {
            return RemoteResponse.Failure(
                request.Id,
                screen,
                RemoteErrors.StaleTurn,
                $"Coup décidé au tour {turn}, la partie est au tour {game.Turn} : relisez l'état.",
                CaptureState());
        }

        return null;
    }

    private void AdvancePending(double delta)
    {
        if (_pending is not { } pending)
        {
            return;
        }

        if (!pending.Executed)
        {
            pending.Wait -= delta;
            if (pending.Wait > 0 || _main.IsAnimating)
            {
                return;
            }

            pending.Executed = true;
            RemoteResponse? immediate;
            try
            {
                immediate = pending.Execute();
            }
            catch (Exception exception)
            {
                GD.PushError($"Pilotage MCP : {pending.Request.Command} a échoué : {exception}");
                immediate = RemoteResponse.Failure(pending.Request.Id, _main.CurrentScreen, RemoteErrors.Internal, exception.Message, CaptureState());
            }

            if (immediate is not null || pending.Complete is null)
            {
                Finish(immediate ?? RemoteResponse.Success(pending.Request.Id, _main.CurrentScreen, CaptureState()));
            }

            return;
        }

        pending.FramesSinceExecution++;
        if (pending.FramesSinceExecution < 2 || _main.IsAnimating)
        {
            return;
        }

        Finish(pending.Complete!());
    }

    private void Finish(RemoteResponse response)
    {
        _pending = null;
        Send(response);
    }

    private PlayerView? CaptureState() => _main.CurrentGame is { } game ? PlayerView.Capture(game) : null;

    private void Send(RemoteResponse response)
    {
        if (_stream is null)
        {
            return;
        }

        try
        {
            var withUi = response with { Ui = CurrentUi().View };
            var bytes = Encoding.UTF8.GetBytes(RemoteProtocol.WriteResponse(withUi) + "\n");
            _stream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException)
        {
            GD.PushWarning($"Pilotage MCP : envoi impossible ({exception.Message}).");
            DropClient();
        }
    }
}
