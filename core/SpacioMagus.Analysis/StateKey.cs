using System.Text;
using SpacioMagus.Core;

namespace SpacioMagus.Analysis;

public static class StateKey
{
    public static string Of(Game game)
    {
        var key = new StringBuilder(96);
        key.Append(game.Turn % game.PatrolPeriod).Append('|');
        key.Append(game.Magus.Position.X).Append(',').Append(game.Magus.Position.Y).Append(',').Append(game.Magus.Magic).Append('|');
        foreach (var monster in game.Monsters)
        {
            key.Append((int)monster.Kind).Append(',')
                .Append(monster.Position.X).Append(',').Append(monster.Position.Y).Append(',')
                .Append(monster.StunTurns).Append(',')
                .Append(monster.IsTracking ? 1 : 0).Append(',')
                .Append(monster.FearTurns).Append(',')
                .Append(monster.MutationTurns).Append(';');
        }

        key.Append('|');
        foreach (var grille in game.Grilles.OrderBy(grille => grille.Position.Y).ThenBy(grille => grille.Position.X))
        {
            key.Append(grille.TurnsRemaining).Append(';');
        }

        key.Append('|');
        foreach (var potion in game.Potions.OrderBy(potion => potion.Y).ThenBy(potion => potion.X))
        {
            key.Append(potion.X).Append(',').Append(potion.Y).Append(';');
        }

        key.Append('|');
        foreach (var container in game.Containers.OrderBy(container => container.Y).ThenBy(container => container.X))
        {
            key.Append(container.X).Append(',').Append(container.Y).Append(';');
        }

        return key.ToString();
    }
}
