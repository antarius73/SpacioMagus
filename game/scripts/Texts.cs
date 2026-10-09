using System.Collections.Generic;
using System.Linq;
using SpacioMagus.Core;

namespace SpacioMagus;

public static class Texts
{
    public const string StoryTitle = "Spacio Magus";

    public const string Story =
        "Vous êtes l'apprenti de Zorgus, un mage qui vous a confié la surveillance de son donjon pendant son absence.\n\n" +
        "Vous avez voulu expérimenter une magie de votre cru, et cela a mal tourné.\n\n" +
        "Votre amie Isabela, une autre apprentie, vous a donné une potion mange-magie qui devrait régler le problème.\n\n" +
        "Mais à votre retour, l'expérience a tourné à la catastrophe et les sécurités du donjon se sont mises en place. " +
        "Il vous faut maintenant, au péril de votre vie, descendre jusqu'à la salle d'expérimentation pour dissiper la magie.";

    public const string PowersTitle = "Vos pouvoirs";

    public const string Powers =
        "Votre magie est celle de l'espace. Elle est pacifiste : tuer ou blesser détruirait votre don.\n\n" +
        "La zone éclairée autour du magus montre la portée de votre magie.\n\n" +
        "Touchez une case voisine pour vous déplacer.\n\n" +
        "Spacio Magus : touchez une case libre éclairée pour vous y téléporter.\n\n" +
        "Spacio Creatura : touchez une créature éclairée, puis une seconde, pour échanger leur place. Elles restent étourdies 4 tours.\n\n" +
        "Chaque sort coûte une sphère de magie. Touchez le magus pour vous reposer et en récupérer une.\n\n" +
        "Un monstre qui vous atteint vous tue.";

    public const string VictoryTitle = "Victoire !";

    public const string Victory = "Vous avez atteint l'escalier.";

    public const string DefeatTitle = "Capturé…";

    public const string Defeat = "Un monstre a attrapé le magus.";

    public const string PauseTitle = "Pause";

    public const string CreditsTitle = "Crédits";

    public const string Credits =
        "Spacio Magus\n\n" +
        "Police Digital Disco : jeti (fontenddev.com), licence CC BY 4.0.\n\n" +
        "Donjon et interface : CraftPix.net.\n\n" +
        "Magus, blob, fiole et toiles : packs de sprites d'origine, licence à confirmer.";

    public static string LevelStart(string levelName) => $"{levelName}\nTouchez ? pour revoir vos pouvoirs.";

    public const string SwapSecondHint = "Touchez la seconde créature à échanger.";

    public const string SwapCancelled = "Échange annulé.";

    public static string ForError(ActionError error) => error switch
    {
        ActionError.GameOver => "La partie est terminée.",
        ActionError.NotAdjacent => "Touchez une case voisine.",
        ActionError.Blocked => "Passage bloqué.",
        ActionError.CornerBlocked => "Impossible de passer en diagonale contre un angle de mur.",
        ActionError.OccupiedByMonster => "Un monstre occupe cette case.",
        ActionError.NotAGrille => "Ce n'est pas une porte.",
        ActionError.GrilleAlreadyOpen => "Cette porte est déjà ouverte.",
        ActionError.NotEnoughMagic => "Plus de magie. Touchez le magus pour vous reposer.",
        ActionError.OutOfRange => "Hors de portée.",
        ActionError.NotVisible => "Le magus ne voit pas cette case.",
        ActionError.NotFree => "Cette case n'est pas libre.",
        ActionError.NotAMonster => "Touchez une créature.",
        ActionError.SameCreature => "Choisissez une autre créature.",
        ActionError.NotEnoughCreatures => "Il faut deux créatures à portée.",
        _ => string.Empty,
    };

    public static string ForEvents(IReadOnlyList<GameEvent> events)
    {
        if (events.OfType<MagusCaptured>().Any())
        {
            return "Un monstre vous attrape !";
        }

        if (events.OfType<LevelCompleted>().Any())
        {
            return "Vous atteignez l'escalier !";
        }

        if (events.OfType<MonsterSpotted>().Any())
        {
            return "Un blob vous a repéré !";
        }

        if (events.OfType<PotionPicked>().Any())
        {
            return "La fiole recharge votre magie.";
        }

        if (events.OfType<CreaturesSwapped>().Any())
        {
            return "Spacio Creatura ! Les créatures sont étourdies.";
        }

        if (events.OfType<GrilleOpened>().FirstOrDefault() is { } opened)
        {
            return $"La porte s'ouvre pour {opened.TurnsRemaining} tours.";
        }

        if (events.OfType<GrilleCloseDelayed>().Any())
        {
            return "Quelque chose empêche la porte de se fermer.";
        }

        if (events.OfType<GrilleClosed>().Any())
        {
            return "Une porte se referme.";
        }

        if (events.OfType<MagusMoved>().Any(moved => moved.Cause == MoveCause.Teleport))
        {
            return "Spacio Magus !";
        }

        if (events.OfType<MagusMoved>().Any(moved => moved.Cause == MoveCause.Pushed))
        {
            return "La porte vous repousse.";
        }

        return string.Empty;
    }
}
