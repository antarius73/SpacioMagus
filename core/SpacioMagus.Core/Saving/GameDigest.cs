using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SpacioMagus.Core.Saving;

public static class GameDigest
{
    public static string Compute(Game game)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"tour {game.Turn} {game.Status}\n");
        text.Append(CultureInfo.InvariantCulture, $"magus {game.Magus.Position.X} {game.Magus.Position.Y} {game.Magus.Magic}\n");
        foreach (var monster in game.Monsters.OrderBy(monster => monster.Id))
        {
            text.Append(CultureInfo.InvariantCulture,
                $"monstre {monster.Id} {monster.Kind} {monster.Position.X} {monster.Position.Y} {monster.StunTurns} {monster.IsTracking} {monster.FearTurns} {monster.MutationTurns}\n");
        }

        foreach (var grille in game.Grilles.OrderBy(grille => grille.Position.Y).ThenBy(grille => grille.Position.X))
        {
            text.Append(CultureInfo.InvariantCulture, $"porte {grille.Position.X} {grille.Position.Y} {grille.TurnsRemaining}\n");
        }

        foreach (var potion in game.Potions.OrderBy(potion => potion.Y).ThenBy(potion => potion.X))
        {
            text.Append(CultureInfo.InvariantCulture, $"fiole {potion.X} {potion.Y}\n");
        }

        return Hash(text.ToString());
    }

    public static string Fingerprint(params string[] sources)
    {
        var text = new StringBuilder();
        foreach (var source in sources)
        {
            var normalized = (source ?? string.Empty).TrimStart('﻿').Replace("\r\n", "\n");
            text.Append(normalized.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(normalized);
        }

        return Hash(text.ToString());
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
