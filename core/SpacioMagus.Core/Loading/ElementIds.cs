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
    public const string BlobRouge = "blob_rouge";
    public const string Brazier = "brasero";
    public const string Water = "eau";
    public const string Torch = "torche";

    public static readonly IReadOnlySet<string> MapElements = new HashSet<string>
    {
        Wall, Floor, Void, Grille, Stairs, Start, BlobVert, BlobRouge, Potion, Brazier, Water,
    };
}
