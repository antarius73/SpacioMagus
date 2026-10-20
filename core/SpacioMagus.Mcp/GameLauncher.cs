using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace SpacioMagus.Mcp;

public sealed class GameLauncher
{
    private const int OutputLines = 40;
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly McpSettings _settings;
    private readonly GameConnection _connection;
    private readonly ILogger<GameLauncher> _logger;
    private readonly Queue<string> _gameOutput = new();
    private readonly object _outputLock = new();
    private Process? _game;

    public GameLauncher(McpSettings settings, GameConnection connection, ILogger<GameLauncher> logger)
    {
        _settings = settings;
        _connection = connection;
        _logger = logger;
    }

    public async Task<string> LaunchAsync(bool build, CancellationToken cancellationToken)
    {
        if (await _connection.TryConnectAsync(cancellationToken))
        {
            return "Le jeu tourne déjà et répond.";
        }

        var (godot, project) = ValidatePaths();
        var report = new List<string>();
        if (build)
        {
            report.Add(await BuildAsync(project, cancellationToken));
        }

        StartGame(godot, project);
        report.Add($"Jeu lancé (processus {_game!.Id}), port {_settings.Port}, délai {_settings.DelayMs} ms.");

        var deadline = DateTime.UtcNow + StartTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_game.HasExited)
            {
                throw new McpException($"Le jeu s'est arrêté au démarrage (code {_game.ExitCode}).\n{GameOutput()}");
            }

            if (await _connection.TryConnectAsync(cancellationToken))
            {
                report.Add("Connexion établie.");
                return string.Join("\n", report);
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        throw new McpException($"Le jeu ne répond pas sur le port {_settings.Port} après {StartTimeout.TotalSeconds:0} s.\n{GameOutput()}");
    }

    private (string Godot, string Project) ValidatePaths()
    {
        if (string.IsNullOrWhiteSpace(_settings.GodotPath) || !File.Exists(_settings.GodotPath))
        {
            throw new McpException($"Exécutable Godot introuvable : « {_settings.GodotPath} ». Vérifiez l'argument --godot= dans la configuration du serveur MCP.");
        }

        if (string.IsNullOrWhiteSpace(_settings.ProjectPath) || !File.Exists(Path.Combine(_settings.ProjectPath, "project.godot")))
        {
            throw new McpException($"Projet Godot introuvable : « {_settings.ProjectPath} » ne contient pas project.godot. Vérifiez l'argument --projet=.");
        }

        return (_settings.GodotPath, _settings.ProjectPath);
    }

    private async Task<string> BuildAsync(string project, CancellationToken cancellationToken)
    {
        var csproj = Directory.GetFiles(project, "*.csproj").FirstOrDefault()
            ?? throw new McpException($"Aucun .csproj dans « {project} » : compilation impossible.");
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = project,
        };
        foreach (var argument in new[] { "build", csproj, "-nologo", "-v", "q", "-clp:ErrorsOnly" })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new McpException($"Impossible de lancer « dotnet » pour compiler : {exception.Message}");
        }

        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errors = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(BuildTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            throw new McpException($"Compilation trop longue (plus de {BuildTimeout.TotalMinutes:0} min) : abandon.");
        }

        var text = (await output + "\n" + await errors).Trim();
        if (process.ExitCode != 0)
        {
            var lines = text.Split('\n').Select(line => line.TrimEnd()).Where(line => line.Length > 0).TakeLast(OutputLines);
            throw new McpException($"La compilation du jeu a échoué (code {process.ExitCode}) :\n{string.Join("\n", lines)}");
        }

        return $"Compilation de {Path.GetFileName(csproj)} réussie.";
    }

    private void StartGame(string godot, string project)
    {
        lock (_outputLock)
        {
            _gameOutput.Clear();
        }

        var start = new ProcessStartInfo(godot)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = project,
        };
        foreach (var argument in new[] { "--path", project, "--", "--mcp", $"--mcp-port={_settings.Port}", $"--mcp-delai={_settings.DelayMs}" })
        {
            start.ArgumentList.Add(argument);
        }

        var game = new Process { StartInfo = start, EnableRaisingEvents = true };
        game.OutputDataReceived += (_, line) => Keep(line.Data);
        game.ErrorDataReceived += (_, line) => Keep(line.Data);
        try
        {
            game.Start();
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            game.Dispose();
            throw new McpException($"Impossible de lancer Godot « {godot} » : {exception.Message}");
        }

        game.BeginOutputReadLine();
        game.BeginErrorReadLine();
        _game?.Dispose();
        _game = game;
    }

    private void Keep(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        _logger.LogDebug("Godot : {Line}", line);
        lock (_outputLock)
        {
            _gameOutput.Enqueue(line);
            while (_gameOutput.Count > OutputLines)
            {
                _gameOutput.Dequeue();
            }
        }
    }

    private string GameOutput()
    {
        lock (_outputLock)
        {
            return _gameOutput.Count == 0 ? "(aucune sortie du jeu)" : "Dernières lignes du jeu :\n" + string.Join("\n", _gameOutput);
        }
    }
}
