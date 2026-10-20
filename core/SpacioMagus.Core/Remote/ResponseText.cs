using System.Text;

namespace SpacioMagus.Core.Remote;

public static class ResponseText
{
    public static string Format(RemoteResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var text = new StringBuilder();
        if (!response.Ok)
        {
            text.Append("ÉCHEC [").Append(response.Error).Append("] ").AppendLine(response.Message);
            if (response.Refusal is { } refusal)
            {
                text.Append("Refus du moteur : ").AppendLine(refusal.ToString());
            }

            text.AppendLine();
        }

        text.Append("Écran : ").AppendLine(ScreenName(response.Screen));
        if (response.Events is { Count: > 0 } events)
        {
            text.AppendLine().AppendLine("Événements du tour :");
            foreach (var line in events)
            {
                text.Append("- ").AppendLine(line);
            }
        }

        if (response.State is { } state)
        {
            text.AppendLine();
            AppendState(text, state);
        }

        if (response.Levels is { Count: > 0 } levels)
        {
            text.AppendLine().AppendLine("Niveaux :");
            foreach (var level in levels)
            {
                text.Append("- ").Append(level.File).Append(" « ").Append(level.Name).Append(" » : ")
                    .Append(level.Unlocked ? "déverrouillé" : "verrouillé");
                if (level.Stars is { } stars)
                {
                    text.Append(", ").Append(stars).Append("/3 étoiles");
                }

                if (level.BestTurns is { } turns)
                {
                    text.Append(", record ").Append(turns).Append(" tours");
                }

                text.AppendLine();
            }
        }

        if (response.Ui is { } ui)
        {
            AppendUi(text, ui);
        }

        return text.ToString().TrimEnd() + "\n";
    }

    public static string ScreenName(RemoteScreen screen) => screen switch
    {
        RemoteScreen.Title => "titre (menu principal)",
        RemoteScreen.LevelSelect => "choix du niveau",
        RemoteScreen.Story => "histoire / aide",
        RemoteScreen.Playing => "en jeu",
        RemoteScreen.Paused => "pause",
        RemoteScreen.Victory => "victoire",
        RemoteScreen.Defeat => "défaite",
        _ => "autre (menu, réglages, confirmation…)",
    };

    public static string Cell(Position position) => $"({position.X},{position.Y})";

    private static void AppendState(StringBuilder text, PlayerView state)
    {
        text.Append("Niveau « ").Append(state.Level).Append(" » — tour ").Append(state.Turn)
            .Append(" — ").AppendLine(StatusName(state));
        var magus = state.Magus;
        text.Append("Magus en ").Append(Cell(magus.Position)).Append(", magie ").Append(magus.Magic).Append('/').Append(magus.MagicMax)
            .Append(" (sort : coût ").Append(magus.SpellCost).Append(", portée ").Append(magus.SpellRange).AppendLine(")");
        if (state.Stranded)
        {
            text.AppendLine("Le magus est coincé : seul « attendre » est possible.");
        }

        text.AppendLine().AppendLine("Carte (x en colonnes, y en lignes) :").Append(MapText.Render(state.Map));

        if (state.Monsters.Count > 0)
        {
            text.AppendLine().AppendLine("Créatures :");
            foreach (var monster in state.Monsters)
            {
                text.Append("- n°").Append(monster.Id).Append(' ').Append(EventText.KindName(monster.Kind)).Append(" en ").Append(Cell(monster.Position));
                var flags = new List<string>();
                if (monster.Tracking)
                {
                    flags.Add("vous traque");
                }

                if (monster.StunTurns > 0)
                {
                    flags.Add($"étourdi {monster.StunTurns} tour(s)");
                }

                if (monster.Mutating)
                {
                    flags.Add("en mutation");
                }

                if (flags.Count > 0)
                {
                    text.Append(" — ").Append(string.Join(", ", flags));
                }

                text.AppendLine();
            }
        }

        if (state.Grilles.Count > 0)
        {
            text.AppendLine().AppendLine("Portes et herses :");
            foreach (var grille in state.Grilles)
            {
                text.Append("- ").Append(grille.Kind == GrilleKind.Door ? "porte" : "herse (manette)").Append(" en ").Append(Cell(grille.Position)).Append(" : ")
                    .AppendLine(grille.Open ? $"ouverte, se referme dans {grille.TurnsRemaining} tour(s)" : "fermée");
            }
        }

        AppendCells(text, "Fioles au sol", state.Potions);
        if (state.Containers.Count > 0)
        {
            text.Append("Contenants : ").AppendLine(string.Join(", ", state.Containers.Select(container =>
                Cell(container.Position) + container.Potion switch
                {
                    true => " (fiole)",
                    false => " (vide)",
                    null => string.Empty,
                })));
        }

        AppendCells(text, "Manettes", state.Levers);
        if (state.Alcoves.Count > 0)
        {
            text.Append("Alcôves : ").AppendLine(string.Join(", ", state.Alcoves.Select(alcove => Cell(alcove.Position) + (alcove.Lit ? " (allumée)" : " (éteinte)"))));
        }

        AppendCells(text, "Sorties", state.Exits);
        AppendCells(text, "Cases que les blobs verts évitent (feu)", state.Fear.GreenBlobAvoids);
        AppendCells(text, "Cases que les blobs rouges évitent (eau)", state.Fear.RedBlobAvoids);

        var legal = state.Legal;
        if (state.Status != GameStatus.Playing)
        {
            return;
        }

        text.AppendLine().AppendLine("Actions possibles ce tour :");
        if (legal.Wait)
        {
            text.AppendLine("- attendre");
        }

        AppendActions(text, "deplacer", legal.Move);
        AppendActions(text, "ouvrir_porte", legal.OpenGrille);
        AppendActions(text, "briser", legal.Break);
        AppendActions(text, "manette", legal.Lever);
        AppendActions(text, "activer", legal.Activate);
        AppendActions(text, "spacio_magus", legal.SpacioMagus);
        if (legal.SpacioCreatura.Count >= 2)
        {
            text.Append("- spacio_creatura : échange de deux créatures parmi ").AppendLine(string.Join(" ", legal.SpacioCreatura.Select(Cell)));
        }
    }

    private static void AppendUi(StringBuilder text, UiView ui)
    {
        if (ui.Texts.Count > 0)
        {
            text.AppendLine().Append("Textes affichés : ").AppendLine(string.Join(" | ", ui.Texts.Select(line => line.Replace('\n', ' '))));
        }

        if (ui.Controls.Count == 0)
        {
            return;
        }

        text.AppendLine().AppendLine("Contrôles à l'écran (outil interface, par id) :");
        foreach (var control in ui.Controls)
        {
            text.Append("- [").Append(control.Id).Append("] ").Append(KindName(control.Kind)).Append(" « ").Append(control.Label).Append(" »");
            if (control.Options is { Count: > 0 } options)
            {
                text.Append(" options : ").Append(string.Join(", ", options.Select((option, index) => $"{index}={option}")));
            }

            if (control.Value is { } value)
            {
                text.Append(" valeur actuelle ").Append(value);
            }

            if (control.Kind == UiKind.Slider)
            {
                text.Append(" (").Append(control.Min).Append(" à ").Append(control.Max).Append(')');
            }

            if (!control.Enabled)
            {
                text.Append(" — désactivé");
            }

            text.AppendLine();
        }
    }

    private static string KindName(UiKind kind) => kind switch
    {
        UiKind.Button => "bouton",
        UiKind.Choice => "liste",
        UiKind.Slider => "curseur",
        UiKind.Door => "porte de niveau",
        UiKind.Close => "fermer",
        _ => kind.ToString(),
    };

    private static string StatusName(PlayerView state) => state.Status switch
    {
        GameStatus.Playing => "en cours",
        GameStatus.Won => "GAGNÉ",
        GameStatus.Lost => state.LossReason switch
        {
            LossReason.Captured => "PERDU (capturé)",
            LossReason.Stranded => "PERDU (coincé)",
            LossReason.Fainted => "PERDU (évanoui)",
            _ => "PERDU",
        },
        _ => state.Status.ToString(),
    };

    private static void AppendCells(StringBuilder text, string title, IReadOnlyList<Position> cells)
    {
        if (cells.Count > 0)
        {
            text.Append(title).Append(" : ").AppendLine(string.Join(" ", cells.Select(Cell)));
        }
    }

    private static void AppendActions(StringBuilder text, string name, IReadOnlyList<Position> cells)
    {
        if (cells.Count > 0)
        {
            text.Append("- ").Append(name).Append(" : ").AppendLine(string.Join(" ", cells.Select(Cell)));
        }
    }
}
