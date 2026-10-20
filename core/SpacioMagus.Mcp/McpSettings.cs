namespace SpacioMagus.Mcp;

public sealed record McpSettings(string? GodotPath, string? ProjectPath, int Port, int DelayMs, bool Build)
{
    public const int DefaultPort = 47821;
    public const int DefaultDelayMs = 900;

    private const string GodotPrefix = "--godot=";
    private const string ProjectPrefix = "--projet=";
    private const string PortPrefix = "--port=";
    private const string DelayPrefix = "--delai=";
    private const string NoBuildFlag = "--sans-compilation";

    public static McpSettings Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? godot = null;
        string? project = null;
        var port = DefaultPort;
        var delay = DefaultDelayMs;
        var build = true;
        foreach (var arg in args)
        {
            if (arg.StartsWith(GodotPrefix, StringComparison.Ordinal))
            {
                godot = Unquote(arg[GodotPrefix.Length..]);
            }
            else if (arg.StartsWith(ProjectPrefix, StringComparison.Ordinal))
            {
                project = Unquote(arg[ProjectPrefix.Length..]);
            }
            else if (arg.StartsWith(PortPrefix, StringComparison.Ordinal))
            {
                port = ParseInt(arg, PortPrefix, 1, 65535);
            }
            else if (arg.StartsWith(DelayPrefix, StringComparison.Ordinal))
            {
                delay = ParseInt(arg, DelayPrefix, 0, 60000);
            }
            else if (arg == NoBuildFlag)
            {
                build = false;
            }
            else
            {
                throw new ArgumentException(
                    $"Argument inconnu « {arg} ». Attendus : {GodotPrefix}<godot.exe> {ProjectPrefix}<dossier du projet> [{PortPrefix}{DefaultPort}] [{DelayPrefix}{DefaultDelayMs}] [{NoBuildFlag}].");
            }
        }

        return new McpSettings(godot, project, port, delay, build);
    }

    private static int ParseInt(string arg, string prefix, int min, int max)
    {
        if (int.TryParse(arg[prefix.Length..], out var value) && value >= min && value <= max)
        {
            return value;
        }

        throw new ArgumentException($"« {arg} » invalide : entier de {min} à {max} attendu.");
    }

    private static string Unquote(string value) => value.Trim().Trim('"');
}
