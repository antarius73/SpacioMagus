namespace SpacioMagus.Core.Remote;

public static class EventText
{
    public static IReadOnlyList<string> Describe(IEnumerable<GameEvent> events, Game game)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(game);
        return events.Select(gameEvent => Describe(gameEvent, game)).ToArray();
    }

    public static string Describe(GameEvent gameEvent, Game game)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        ArgumentNullException.ThrowIfNull(game);
        return gameEvent switch
        {
            MagusMoved moved => $"Le magus {Cause(moved.Cause)} de {Cell(moved.From)} à {Cell(moved.To)}.",
            MagicChanged magic => $"Magie : {magic.From} → {magic.To}.",
            PotionPicked potion => $"Fiole ramassée en {Cell(potion.Position)}.",
            MonsterMoved moved => $"{Monster(game, moved.MonsterId)} {Cause(moved.Cause)} de {Cell(moved.From)} à {Cell(moved.To)}.",
            MonsterSpotted spotted => $"{Monster(game, spotted.MonsterId)} a repéré le magus et le traque.",
            MonsterLostTrack lost => $"{Monster(game, lost.MonsterId)} a perdu la trace du magus.",
            CreaturesSwapped swapped => $"{Monster(game, swapped.FirstMonsterId)} et {Monster(game, swapped.SecondMonsterId)} échangent leurs places.",
            MonsterStunned stunned => $"{Monster(game, stunned.MonsterId)} est étourdi pour {stunned.Turns} tours.",
            MonsterRecovered recovered => $"{Monster(game, recovered.MonsterId)} reprend ses esprits.",
            GrilleOpened opened => $"La grille {Cell(opened.Position)} s'ouvre pour {opened.TurnsRemaining} tours.",
            LeverPulled lever => $"Manette {Cell(lever.Lever)} actionnée : elle commande la grille {Cell(lever.Grille)}.",
            GrilleCountdown countdown => $"La grille {Cell(countdown.Position)} se refermera dans {countdown.TurnsRemaining} tours.",
            GrilleClosed closed => $"La grille {Cell(closed.Position)} se referme.",
            GrilleCloseDelayed delayed => $"La grille {Cell(delayed.Position)} ne peut pas se refermer : passage occupé.",
            MonsterAfraid afraid => $"{Monster(game, afraid.MonsterId)} a peur et recule.",
            MonsterMutationStarted mutation => $"{Monster(game, mutation.MonsterId)} commence à muter ({mutation.Turns} tours).",
            MonsterMutated mutated => $"{Monster(game, mutated.MonsterId)} a muté : {KindName(mutated.From)} → {KindName(mutated.To)}.",
            ContainerBroken broken => broken.HadPotion
                ? $"Contenant brisé en {Cell(broken.Position)} : il contenait une fiole."
                : $"Contenant brisé en {Cell(broken.Position)} : vide.",
            MagusCaptured captured => $"{Monster(game, captured.MonsterId)} a capturé le magus.",
            MagusStranded stranded => $"Le magus est bloqué en {Cell(stranded.Position)}, sans issue.",
            MagusBitten bitten => $"{Monster(game, bitten.MonsterId)} mord le magus.",
            MagusFainted fainted => $"Le magus s'évanouit, mordu par {Monster(game, fainted.MonsterId)}.",
            AlcoveLit alcove => $"L'alcôve {Cell(alcove.Position)} s'allume.",
            PotionsRevealed => "Les fioles cachées dans les contenants sont révélées.",
            ChestToggled chest => chest.Open ? $"Le coffre {Cell(chest.Position)} s'ouvre : de l'or, rien d'utile." : $"Le coffre {Cell(chest.Position)} se referme.",
            LeverReset lever => $"La manette {Cell(lever.Lever)} revient en position initiale.",
            KeyPicked key => $"Clé ramassée en {Cell(key.Position)}.",
            DoorUnlocked door => $"La porte {Cell(door.Position)} est déverrouillée avec la clé.",
            SpikesRaised spikes => $"Les pointes sortent en {Cell(spikes.Position)}.",
            SpikesLowered spikes => $"Les pointes rentrent en {Cell(spikes.Position)}.",
            MagusImpaled impaled => $"Le magus est empalé par les pointes en {Cell(impaled.Position)}.",
            ObjectMoved moved => $"Un gros objet est déplacé de {Cell(moved.From)} à {Cell(moved.To)}.",
            ObjectsSwapped swapped => $"Spacio Creatura échange {Cell(swapped.First)} et {Cell(swapped.Second)}.",
            ScrollTaken => "Le magus prend le parchemin : Spacio Creatura peut désormais échanger aussi les gros objets.",
            LevelCompleted => "Niveau terminé : le magus a atteint l'escalier.",
            _ => gameEvent.GetType().Name,
        };
    }

    public static string KindName(MonsterKind kind) => kind switch
    {
        MonsterKind.BlobVert => "blob vert",
        MonsterKind.BlobRouge => "blob rouge",
        MonsterKind.ChauveSouris => "chauve-souris",
        _ => kind.ToString(),
    };

    private static string Monster(Game game, int id)
    {
        var monster = game.Monsters.FirstOrDefault(candidate => candidate.Id == id);
        if (monster is null)
        {
            return $"La créature n°{id}";
        }

        var article = monster.Kind == MonsterKind.ChauveSouris ? "La" : "Le";
        return $"{article} {KindName(monster.Kind)} n°{id}";
    }

    private static string Cell(Position position) => ResponseText.Cell(position);

    private static string Cause(MoveCause cause) => cause switch
    {
        MoveCause.Step => "se déplace",
        MoveCause.Teleport => "se téléporte",
        MoveCause.Swap => "est échangé",
        MoveCause.Pushed => "est repoussé",
        _ => "se déplace",
    };
}
