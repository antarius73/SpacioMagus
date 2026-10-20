using System.Text;

namespace SpacioMagus.Core.Remote;

public static class MapText
{
    public const string Legend =
        "@ magus, b blob vert, r blob rouge, c chauve-souris, ! fiole, " +
        "# mur, (espace) vide, . sol, > escalier de sortie, < escalier montant (décor), " +
        "P porte fermée, H herse fermée (manette), / porte ou herse ouverte, m manette, a arche, " +
        "* brasero, ~ eau, u tonneau, k caisse, j vase (U K J : contenant avec fiole révélée), " +
        "A alcôve éteinte, L alcôve allumée. Coordonnées (x, y) : origine en haut à gauche, y vers le bas.";

    public static IReadOnlyList<string> Rows(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var level = game.Level;
        var alcoves = game.Script is null ? new HashSet<Position>() : new HashSet<Position>(level.Alcoves);
        var potions = new HashSet<Position>(game.Potions);
        var rows = new string[level.Height];
        var line = new StringBuilder(level.Width);
        for (var y = 0; y < level.Height; y++)
        {
            line.Clear();
            for (var x = 0; x < level.Width; x++)
            {
                var position = new Position(x, y);
                line.Append(Symbol(game, position, alcoves, potions));
            }

            rows[y] = line.ToString();
        }

        return rows;
    }

    public static string Render(IReadOnlyList<string> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var width = rows.Count == 0 ? 0 : rows.Max(row => row.Length);
        var label = Math.Max(1, (rows.Count - 1).ToString().Length);
        var margin = new string(' ', label + 1);
        var text = new StringBuilder();
        if (width > 10)
        {
            text.Append(margin);
            for (var x = 0; x < width; x++)
            {
                text.Append(x % 10 == 0 ? (char)('0' + x / 10 % 10) : ' ');
            }

            text.Append('\n');
        }

        text.Append(margin);
        for (var x = 0; x < width; x++)
        {
            text.Append((char)('0' + x % 10));
        }

        text.Append('\n');
        for (var y = 0; y < rows.Count; y++)
        {
            text.Append(y.ToString().PadLeft(label)).Append(' ').Append(rows[y].PadRight(width)).Append('\n');
        }

        return text.ToString();
    }

    private static char Symbol(Game game, Position position, HashSet<Position> alcoves, HashSet<Position> potions)
    {
        if (game.Magus.Position == position)
        {
            return '@';
        }

        if (game.MonsterAt(position) is { } monster)
        {
            return monster.Kind switch
            {
                MonsterKind.BlobVert => 'b',
                MonsterKind.BlobRouge => 'r',
                MonsterKind.ChauveSouris => 'c',
                _ => '?',
            };
        }

        if (potions.Contains(position))
        {
            return '!';
        }

        if (alcoves.Contains(position))
        {
            return game.IsActivated(position) ? 'L' : 'A';
        }

        var terrain = game.Level.TerrainAt(position);
        if (terrain == Terrain.Grille)
        {
            if (game.GrilleAt(position) is { IsOpen: true })
            {
                return '/';
            }

            return game.Level.IsDoor(position) ? 'P' : 'H';
        }

        if (LevelDefinition.IsContainer(terrain) && !game.IsContainer(position))
        {
            return '.';
        }

        var revealed = game.PotionsAreRevealed && game.ContainerHasPotion(position);
        return terrain switch
        {
            Terrain.Void => ' ',
            Terrain.Floor => '.',
            Terrain.Wall => '#',
            Terrain.Stairs => '>',
            Terrain.StairsUp => '<',
            Terrain.Brazier => '*',
            Terrain.Water => '~',
            Terrain.Barrel => revealed ? 'U' : 'u',
            Terrain.Crate => revealed ? 'K' : 'k',
            Terrain.Vase => revealed ? 'J' : 'j',
            Terrain.Lever => 'm',
            Terrain.Arch => 'a',
            _ => '?',
        };
    }
}
