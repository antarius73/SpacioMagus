using System.Text.RegularExpressions;
using SpacioMagus.Core.Loading;

namespace SpacioMagus.Core.Tests;

public class TextFileTests
{
    private const string ReferenceLanguage = "fr";
    private const string NameKey = "langue";
    private static readonly Regex Placeholder = new(@"\{(\d+)(?:[,:][^}]*)?\}", RegexOptions.Compiled);

    [Fact]
    public void ParsesKeysParagraphsAndComments()
    {
        var texts = TextFile.Parse("t.txt", "# commentaire\n\n[a]\nUn\n\nDeux\n\n[b]\r\nTrois  \r\n");

        Assert.Equal("Un\n\nDeux", texts["a"]);
        Assert.Equal("Trois", texts["b"]);
    }

    [Fact]
    public void KeepsAnEmptyText()
    {
        var texts = TextFile.Parse("t.txt", "[a]\n[b]\nB");

        Assert.Equal(string.Empty, texts["a"]);
    }

    [Fact]
    public void RejectsDuplicateKeys()
    {
        var exception = Assert.Throws<LevelLoadException>(() => TextFile.Parse("t.txt", "[a]\nUn\n[a]\nDeux"));

        Assert.Contains(exception.Issues, issue => issue.Line == 3);
    }

    [Fact]
    public void RejectsTextBeforeTheFirstKey()
    {
        var exception = Assert.Throws<LevelLoadException>(() => TextFile.Parse("t.txt", "Perdu\n[a]\nUn"));

        Assert.Contains(exception.Issues, issue => issue.Line == 1);
    }

    [Fact]
    public void RejectsEmptyKeys()
    {
        Assert.Throws<LevelLoadException>(() => TextFile.Parse("t.txt", "[ ]\nUn"));
    }

    [Fact]
    public void RealTextFilesAreValid()
    {
        var files = LoadAll();

        Assert.Contains(ReferenceLanguage, files.Keys);
        foreach (var (language, texts) in files)
        {
            Assert.True(texts.ContainsKey(NameKey), $"textes_{language}.txt : clé [{NameKey}] absente.");
        }
    }

    [Fact]
    public void RealTextFilesShareTheSameKeysAndPlaceholders()
    {
        var files = LoadAll();
        var reference = files[ReferenceLanguage];
        var problems = new List<string>();

        foreach (var (language, texts) in files.Where(file => file.Key != ReferenceLanguage))
        {
            problems.AddRange(reference.Keys.Except(texts.Keys).Select(key => $"textes_{language}.txt : clé [{key}] absente."));
            problems.AddRange(texts.Keys.Except(reference.Keys).Select(key => $"textes_{language}.txt : clé [{key}] inconnue en {ReferenceLanguage}."));
            foreach (var key in reference.Keys.Intersect(texts.Keys))
            {
                var expected = Placeholders(reference[key]);
                var actual = Placeholders(texts[key]);
                if (!expected.SetEquals(actual))
                {
                    problems.Add($"textes_{language}.txt : [{key}] utilise {Describe(actual)} au lieu de {Describe(expected)}.");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static Dictionary<string, IReadOnlyDictionary<string, string>> LoadAll()
    {
        var directory = Path.Combine(GameDirectory(), "data");
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        foreach (var path in Directory.GetFiles(directory, "textes_*.txt"))
        {
            var fileName = Path.GetFileName(path);
            var language = Path.GetFileNameWithoutExtension(path)["textes_".Length..];
            result[language] = TextFile.Parse(fileName, File.ReadAllText(path));
        }

        return result;
    }

    private static HashSet<int> Placeholders(string text) =>
        Placeholder.Matches(text).Select(match => int.Parse(match.Groups[1].Value)).ToHashSet();

    private static string Describe(HashSet<int> placeholders) =>
        placeholders.Count == 0 ? "aucun paramètre" : string.Join(", ", placeholders.Order().Select(index => $"{{{index}}}"));

    private static string GameDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "game");
            if (Directory.Exists(Path.Combine(candidate, "data")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Dossier « game » introuvable en remontant depuis {AppContext.BaseDirectory}.");
    }
}
