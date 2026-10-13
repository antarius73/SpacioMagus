using System.Text.RegularExpressions;
using SpacioMagus.Core.Progress;

namespace SpacioMagus.Core.Loading;

public sealed record DecorItem(Position Position, string Id);

public sealed record LoadedLevel(LevelDefinition Definition, RuleSet Rules, IReadOnlyList<DecorItem> Decor, StarThresholds? Stars = null);

public static class LevelFile
{
    private const string LevelSection = "niveau";
    private const string MapSection = "carte";
    private const string DecorSection = "decor";
    private const string MarkersSection = "reperes";

    private static readonly string[] Sections = { LevelSection, MapSection, DecorSection, MarkersSection };

    private static readonly Regex ParameterPattern = new(
        @"(?<key>[a-z_]+)\s*=\s*(?<value>[^=]+?)\s*(?=,\s*[a-z_]+\s*=|$)",
        RegexOptions.CultureInvariant);

    private static readonly Regex CoordinatesPattern = new(
        @"^\s*(?<x>\d+)\s*,\s*(?<y>\d+)\s*$",
        RegexOptions.CultureInvariant);

    public static LoadedLevel Parse(string fileName, string content, Legend legend, RuleSet baseRules)
    {
        ArgumentNullException.ThrowIfNull(legend);
        ArgumentNullException.ThrowIfNull(baseRules);

        var issues = new List<LoadIssue>();
        var document = SectionedText.Parse(fileName, content, issues, Sections, null);

        var name = Path.GetFileNameWithoutExtension(fileName);
        StarThresholds? stars = null;
        var rules = baseRules;
        foreach (var (line, rawKey, value) in SectionedText.KeyValues(fileName, document.Lines(LevelSection), issues))
        {
            var key = rawKey.ToLowerInvariant();
            if (key == "nom")
            {
                name = value;
            }
            else if (key == "etoiles")
            {
                stars = ParseStars(fileName, line, value, issues);
            }
            else if (RuleKeys.IsRuleKey(key))
            {
                rules = RuleKeys.Apply(rules, fileName, line, key, value, issues);
            }
            else
            {
                issues.Add(new LoadIssue(fileName, line.Number, 0, $"Clé inconnue « {key} » dans [{LevelSection}]. Clés possibles : nom, etoiles, {string.Join(", ", RuleKeys.Names)}."));
            }
        }

        RuleKeys.Validate(rules, fileName, issues);

        var markers = ParseMarkers(fileName, document.Lines(MarkersSection), issues);

        var rows = TrimBlankLines(document.Lines(MapSection), trimStart: true);
        if (rows.Count == 0)
        {
            issues.Add(new LoadIssue(fileName, document.HeaderLine(MapSection), 0, $"Section [{MapSection}] absente ou vide."));
            throw new LevelLoadException(issues);
        }

        var width = rows.Max(row => row.Text.Length);
        var height = rows.Count;
        var cells = new Terrain[width * height];
        var starts = new List<(Position Position, SourceLine Line, int Column)>();
        var monsters = new List<MonsterSpawn>();
        var potions = new List<Position>();
        var usedMarkers = new HashSet<char>();

        for (var y = 0; y < height; y++)
        {
            var row = rows[y];
            for (var x = 0; x < width; x++)
            {
                var symbol = x < row.Text.Length ? row.Text[x] : ' ';
                var position = new Position(x, y);
                string elementId;
                Position? post = null;
                IReadOnlyList<Position>? route = null;

                if (symbol == ' ')
                {
                    elementId = ElementIds.Void;
                }
                else if (char.IsDigit(symbol))
                {
                    if (!markers.TryGetValue(symbol, out var marker))
                    {
                        issues.Add(new LoadIssue(fileName, row.Number, x + 1, $"Repère « {symbol} » absent de la section [{MarkersSection}]."));
                        continue;
                    }

                    usedMarkers.Add(symbol);
                    elementId = marker.Id;
                    post = marker.Post;
                    route = marker.Route;
                }
                else if (legend.TryGet(symbol, out var entry) && entry.Layer == LegendLayer.Map)
                {
                    elementId = entry.Id;
                }
                else
                {
                    var reason = legend.TryGet(symbol, out _) ? "est un élément de décor, interdit dans la carte" : "est absent de la légende";
                    issues.Add(new LoadIssue(fileName, row.Number, x + 1, $"Le caractère « {symbol} » {reason}."));
                    continue;
                }

                cells[y * width + x] = elementId switch
                {
                    ElementIds.Wall => Terrain.Wall,
                    ElementIds.Void => Terrain.Void,
                    ElementIds.Grille => Terrain.Grille,
                    ElementIds.Stairs => Terrain.Stairs,
                    ElementIds.Brazier => Terrain.Brazier,
                    ElementIds.Water => Terrain.Water,
                    ElementIds.Barrel or ElementIds.BarrelPotion => Terrain.Barrel,
                    ElementIds.Crate or ElementIds.CratePotion => Terrain.Crate,
                    _ => Terrain.Floor,
                };

                switch (elementId)
                {
                    case ElementIds.Start:
                        starts.Add((position, row, x + 1));
                        break;
                    case ElementIds.BlobVert:
                        monsters.Add(new MonsterSpawn(MonsterKind.BlobVert, position, post ?? position, route));
                        break;
                    case ElementIds.BlobRouge:
                        monsters.Add(new MonsterSpawn(MonsterKind.BlobRouge, position, post ?? position, route));
                        break;
                    case ElementIds.Potion or ElementIds.BarrelPotion or ElementIds.CratePotion:
                        potions.Add(position);
                        break;
                }
            }
        }

        if (starts.Count == 0)
        {
            issues.Add(new LoadIssue(fileName, document.HeaderLine(MapSection), 0, "La carte n'a aucun départ du magus."));
        }

        foreach (var extra in starts.Skip(1))
        {
            issues.Add(new LoadIssue(fileName, extra.Line.Number, extra.Column, "Départ du magus en double : un seul est autorisé."));
        }

        if (!cells.Contains(Terrain.Stairs))
        {
            issues.Add(new LoadIssue(fileName, document.HeaderLine(MapSection), 0, "La carte n'a aucun escalier."));
        }

        foreach (var marker in markers.Values.Where(marker => !usedMarkers.Contains(marker.Symbol)))
        {
            issues.Add(new LoadIssue(fileName, marker.Line, 0, $"Repère « {marker.Symbol} » défini mais absent de la carte."));
        }

        foreach (var marker in markers.Values.Where(marker => marker.Post is { } post && (post.X >= width || post.Y >= height)))
        {
            issues.Add(new LoadIssue(fileName, marker.Line, 0, $"Le poste {marker.Post} du repère « {marker.Symbol} » est hors de la carte ({width} × {height})."));
        }

        var layer = ParseDecor(fileName, document.Lines(DecorSection), legend, width, height, issues);
        var torches = layer.Where(item => item.Id == ElementIds.Torch).Select(item => item.Position).ToList();
        var decor = layer.Where(item => item.Id != ElementIds.Torch).ToList();
        foreach (var item in decor.Where(item => ElementIds.WallDecor.Contains(item.Id)))
        {
            if (!IsWallFace(cells, width, height, item.Position))
            {
                issues.Add(new LoadIssue(fileName, 0, 0, $"Le décor « {item.Id} » en {item.Position} doit être sur un mur avec du sol juste en dessous."));
            }
        }

        if (issues.Count > 0)
        {
            throw new LevelLoadException(issues);
        }

        try
        {
            var definition = new LevelDefinition(name, width, height, cells, starts[0].Position, monsters, potions, torches);
            return new LoadedLevel(definition, rules, decor, stars);
        }
        catch (LevelDefinitionException exception)
        {
            throw new LevelLoadException(new[]
            {
                new LoadIssue(fileName, 0, 0, $"{exception.Message} Les positions sont (colonne, ligne) dans la carte, à partir de 0 en haut à gauche."),
            });
        }
    }

    private static StarThresholds? ParseStars(string fileName, SourceLine line, string value, List<LoadIssue> issues)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var three)
            || !int.TryParse(parts[1], out var two)
            || three < 1
            || two < three)
        {
            issues.Add(new LoadIssue(fileName, line.Number, 0, $"« etoiles » attend deux nombres de tours croissants « 3 étoiles, 2 étoiles », par exemple « 14, 18 » : « {value} »."));
            return null;
        }

        return new StarThresholds(three, two);
    }

    private static bool IsWallFace(Terrain[] cells, int width, int height, Position position)
    {
        if (position.Y + 1 >= height || cells[position.Y * width + position.X] != Terrain.Wall)
        {
            return false;
        }

        return LevelDefinition.IsGround(cells[(position.Y + 1) * width + position.X]);
    }

    private static List<SourceLine> TrimBlankLines(IReadOnlyList<SourceLine> lines, bool trimStart)
    {
        var result = lines.ToList();
        while (trimStart && result.Count > 0 && result[0].Text.Trim().Length == 0)
        {
            result.RemoveAt(0);
        }

        while (result.Count > 0 && result[^1].Text.Trim().Length == 0)
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    private static Dictionary<char, Marker> ParseMarkers(string fileName, IReadOnlyList<SourceLine> lines, List<LoadIssue> issues)
    {
        var markers = new Dictionary<char, Marker>();
        foreach (var (line, key, value) in SectionedText.KeyValues(fileName, lines, issues))
        {
            if (key.Length != 1 || !char.IsDigit(key[0]))
            {
                issues.Add(new LoadIssue(fileName, line.Number, 0, $"Un repère est un chiffre de 1 à 9 : « {key} »."));
                continue;
            }

            var separator = value.IndexOf(',');
            var id = (separator < 0 ? value : value[..separator]).Trim().ToLowerInvariant();
            var parameters = separator < 0 ? string.Empty : value[(separator + 1)..];
            if (!ElementIds.MapElements.Contains(id) || id == ElementIds.Start)
            {
                issues.Add(new LoadIssue(fileName, line.Number, 0, $"Élément de repère inconnu ou interdit « {id} »."));
                continue;
            }

            Position? post = null;
            IReadOnlyList<Position>? route = null;
            var valid = true;
            foreach (Match match in ParameterPattern.Matches(parameters))
            {
                var parameter = match.Groups["key"].Value;
                var parameterValue = match.Groups["value"].Value;
                if (parameter == "poste" && id is ElementIds.BlobVert or ElementIds.BlobRouge)
                {
                    var coordinates = CoordinatesPattern.Match(parameterValue);
                    if (!coordinates.Success)
                    {
                        issues.Add(new LoadIssue(fileName, line.Number, 0, $"« poste » attend « colonne,ligne » : « {parameterValue} »."));
                        valid = false;
                        continue;
                    }

                    post = new Position(int.Parse(coordinates.Groups["x"].Value), int.Parse(coordinates.Groups["y"].Value));
                }
                else if (parameter == "ronde" && id is ElementIds.BlobVert or ElementIds.BlobRouge)
                {
                    var waypoints = new List<Position>();
                    foreach (var token in parameterValue.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var coordinates = CoordinatesPattern.Match(token);
                        if (!coordinates.Success)
                        {
                            issues.Add(new LoadIssue(fileName, line.Number, 0, $"« ronde » attend des points « colonne,ligne » séparés par des espaces : « {token} »."));
                            valid = false;
                            break;
                        }

                        waypoints.Add(new Position(int.Parse(coordinates.Groups["x"].Value), int.Parse(coordinates.Groups["y"].Value)));
                    }

                    if (valid && waypoints.Count == 0)
                    {
                        issues.Add(new LoadIssue(fileName, line.Number, 0, "« ronde » ne contient aucun point."));
                        valid = false;
                    }

                    if (valid)
                    {
                        route = PatrolRoute.Expand(waypoints);
                    }
                }
                else
                {
                    issues.Add(new LoadIssue(fileName, line.Number, 0, $"Paramètre « {parameter} » non pris en charge pour « {id} »."));
                    valid = false;
                }
            }

            if (parameters.Trim().Length > 0 && ParameterPattern.Matches(parameters).Count == 0)
            {
                issues.Add(new LoadIssue(fileName, line.Number, 0, $"Paramètres illisibles : « {parameters.Trim()} »."));
                valid = false;
            }

            if (!valid)
            {
                continue;
            }

            if (!markers.TryAdd(key[0], new Marker(key[0], id, post, route, line.Number)))
            {
                issues.Add(new LoadIssue(fileName, line.Number, 0, $"Repère « {key} » défini deux fois."));
            }
        }

        return markers;
    }

    private static List<DecorItem> ParseDecor(
        string fileName,
        IReadOnlyList<SourceLine> lines,
        Legend legend,
        int width,
        int height,
        List<LoadIssue> issues)
    {
        var decor = new List<DecorItem>();
        var rows = TrimBlankLines(lines, trimStart: false);
        if (rows.Count > height)
        {
            issues.Add(new LoadIssue(fileName, rows[height].Number, 0, $"La couche [{DecorSection}] a plus de lignes que la carte ({height})."));
        }

        for (var y = 0; y < Math.Min(rows.Count, height); y++)
        {
            var row = rows[y];
            for (var x = 0; x < row.Text.Length; x++)
            {
                var symbol = row.Text[x];
                if (symbol == ' ')
                {
                    continue;
                }

                if (x >= width)
                {
                    issues.Add(new LoadIssue(fileName, row.Number, x + 1, $"Décor hors de la carte (largeur {width})."));
                    continue;
                }

                if (!legend.TryGet(symbol, out var entry) || entry.Layer != LegendLayer.Decor)
                {
                    issues.Add(new LoadIssue(fileName, row.Number, x + 1, $"Le caractère « {symbol} » n'est pas un élément de décor de la légende."));
                    continue;
                }

                decor.Add(new DecorItem(new Position(x, y), entry.Id));
            }
        }

        return decor;
    }

    private sealed record Marker(char Symbol, string Id, Position? Post, IReadOnlyList<Position>? Route, int Line);
}
