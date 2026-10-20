namespace SpacioMagus.Core.Loading;

public static class ElementIds
{
    public const string Wall = "mur";
    public const string Floor = "sol";
    public const string Void = "vide";
    public const string Grille = "grille";
    public const string Door = "porte";
    public const string Stairs = "escalier";
    public const string Start = "depart";
    public const string BlobVert = "blob_vert";
    public const string Potion = "fiole";
    public const string BlobRouge = "blob_rouge";
    public const string Brazier = "brasero";
    public const string Water = "eau";
    public const string Torch = "torche";
    public const string Barrel = "tonneau";
    public const string BarrelPotion = "tonneau_fiole";
    public const string Crate = "caisse";
    public const string CratePotion = "caisse_fiole";
    public const string Vase = "vase";
    public const string VasePotion = "vase_fiole";
    public const string Lever = "levier";
    public const string Bat = "chauve_souris";
    public const string Alcove = "alcove";
    public const string StairsUp = "escalier_montant";
    public const string Arch = "arche";
    public const string Chest = "coffre";
    public const string Key = "cle";
    public const string LockedDoor = "porte_verrouillee";
    public const string Spikes = "piege";
    public const string Altar = "autel";
    public const string Coins = "pieces";
    public const string RedHanging = "tenture_rouge";
    public const string RedBoneHanging = "tenture_rouge_os";
    public const string RedTornHanging = "tenture_rouge_dechiree";
    public const string GreenHanging = "tenture_verte";
    public const string GreenBoneHanging = "tenture_verte_os";
    public const string Shield = "bouclier";

    public static readonly IReadOnlySet<string> WallDecor = new HashSet<string>
    {
        RedHanging, RedBoneHanging, RedTornHanging, GreenHanging, GreenBoneHanging, Shield,
    };

    public static readonly IReadOnlySet<string> MapElements = new HashSet<string>
    {
        Wall, Floor, Void, Grille, Door, Stairs, Start, BlobVert, BlobRouge, Potion, Brazier, Water, Barrel, BarrelPotion, Crate, CratePotion,
        Vase, VasePotion, Lever, Bat, StairsUp, Arch, Chest, Key, LockedDoor, Spikes, Altar,
    };

    public static readonly IReadOnlySet<string> Monsters = new HashSet<string>
    {
        BlobVert, BlobRouge, Bat,
    };
}
