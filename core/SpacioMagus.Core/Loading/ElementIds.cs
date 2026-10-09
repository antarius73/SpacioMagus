namespace SpacioMagus.Core.Loading;

public static class ElementIds
{
    public const string Wall = "mur";
    public const string Floor = "sol";
    public const string Void = "vide";
    public const string Grille = "grille";
    public const string Stairs = "escalier";
    public const string Start = "depart";
    public const string BlobVert = "blob_vert";
    public const string Potion = "fiole";

    public static readonly IReadOnlySet<string> MapElements = new HashSet<string>
    {
        Wall, Floor, Void, Grille, Stairs, Start, BlobVert, Potion,
    };
}
