using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpacioMagus.Mcp;

McpSettings settings;
try
{
    settings = McpSettings.Parse(args);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services
    .AddSingleton(settings)
    .AddSingleton<GameConnection>()
    .AddSingleton<GameLauncher>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "spaciomagus", Title = "Mob le Magus", Version = "1.0.0" };
        options.ServerInstructions =
            "Pilote le jeu Godot « Mob le Magus » affiché sur l'écran de l'utilisateur, comme un joueur humain. " +
            "Commencer par lancer_jeu, lire regles une fois, puis enchaîner etat / jouer pendant la partie et interface pour les menus. " +
            "Ne jouer que les actions listées dans « Actions possibles ce tour ». Coordonnées (x,y) : x colonne, y ligne, origine en haut à gauche.";
    })
    .WithStdioServerTransport()
    .WithTools<GameTools>();

await builder.Build().RunAsync();
return 0;
