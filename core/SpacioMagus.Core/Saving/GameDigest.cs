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

        foreach (var container in game.Containers.OrderBy(container => container.Y).ThenBy(container => container.X))
        {
            text.Append(CultureInfo.InvariantCulture, $"contenant {container.X} {container.Y} {game.ContainerHasPotion(container)}\n");
        }

        foreach (var potion in game.Potions.OrderBy(potion => potion.Y).ThenBy(potion => potion.X))
        {
            text.Append(CultureInfo.InvariantCulture, $"fiole {potion.X} {potion.Y}\n");
        }

        foreach (var cell in game.ActivatedCells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X))
        {
            text.Append(CultureInfo.InvariantCulture, $"active {cell.X} {cell.Y}\n");
        }

        text.Append(CultureInfo.InvariantCulture, $"fioles_revelees {game.PotionsAreRevealed}\n");
        foreach (var (chest, open) in game.Chests.OrderBy(entry => entry.Key.Y).ThenBy(entry => entry.Key.X))
        {
            text.Append(CultureInfo.InvariantCulture, $"coffre {chest.X} {chest.Y} {open}\n");
        }

        foreach (var key in game.Keys.OrderBy(key => key.Y).ThenBy(key => key.X))
        {
            text.Append(CultureInfo.InvariantCulture, $"cle {key.X} {key.Y}\n");
        }

        foreach (var door in game.UnlockedDoors.OrderBy(door => door.Y).ThenBy(door => door.X))
        {
            text.Append(CultureInfo.InvariantCulture, $"deverrouillee {door.X} {door.Y}\n");
        }

        foreach (var lever in game.PulledLevers.OrderBy(lever => lever.Y).ThenBy(lever => lever.X))
        {
            text.Append(CultureInfo.InvariantCulture, $"manette_tiree {lever.X} {lever.Y}\n");
        }

        text.Append(CultureInfo.InvariantCulture, $"cles_tenues {game.KeysHeld}\n");
        text.Append(CultureInfo.InvariantCulture, $"objets_deplacables {game.CanSwapObjects}\n");
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
