using System;
using Godot;

namespace SpacioMagus;

public partial class SpellScene : Illustration
{
    protected const int Cell = 32;
    protected const int IdleRow = 8;
    protected static readonly Vector2 SceneSize = new(112, 64);
    protected static readonly Vector2 LeftCell = new(8, 32);
    protected static readonly Vector2 MiddleCell = new(40, 32);
    protected static readonly Vector2 RightCell = new(72, 32);

    private static Texture2D? _magus;
    private static Texture2D? _animations;

    protected static Texture2D Magus => _magus ??= Load("res://assets/sprites/magus_pixel.png");

    protected static Texture2D Animations => _animations ??= Load("res://assets/sprites/animations.png");

    protected void DrawRoom()
    {
        BeginScene(SceneSize, 2f);
        for (var x = 0; x < SceneSize.X; x += 16)
        {
            DrawTile(70, x, 0);
            DrawTile(87, x, 16);
            DrawTile(138, x, 32);
            DrawTile(138, x, 48);
        }
    }

    protected void DrawMagus(Vector2 cell, float alpha)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var frame = (int)(Time * 6) % 6;
        DrawTextureRectRegion(Magus, new Rect2(cell, Cell, Cell), new Rect2(frame * Cell, IdleRow * Cell, Cell, Cell), new Color(1f, 1f, 1f, alpha));
    }

    protected void DrawBlob(Vector2 cell, int row, bool stunned, float alpha)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var frame = stunned ? 0 : (int)(Time * 5) % 4;
        var tint = stunned ? new Color(0.6f, 0.6f, 0.75f, alpha) : new Color(1f, 1f, 1f, alpha);
        DrawTextureRectRegion(Animations, new Rect2(cell, Cell, Cell), new Rect2(frame * Cell, row * Cell, Cell, Cell), tint);
    }

    protected void EndScene() => DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
}

public partial class SpacioMagusIllustration : SpellScene
{
    private const double IdleDuration = 0.8;
    private static readonly Vector2 StartCell = new(20, 26);
    private static readonly Vector2 EndCell = new(60, 26);

    public override void _Draw()
    {
        DrawRoom();
        var leg = IdleDuration + SpellFx.TeleportDuration;
        var t = Time % (2 * leg);
        var outward = t < leg;
        var local = outward ? t : t - leg;
        var from = outward ? StartCell : EndCell;
        var to = outward ? EndCell : StartCell;

        if (local < IdleDuration)
        {
            DrawMagus(from, 1f);
            EndScene();
            return;
        }

        var elapsed = local - IdleDuration;
        var at = SpellFx.IsDeparting(elapsed) ? from : to;
        SpellFx.DrawTeleportAura(this, SpellFx.AuraBack, at, elapsed);
        DrawMagus(at, SpellFx.TeleportMagusAlpha(elapsed));
        SpellFx.DrawTeleportAura(this, SpellFx.AuraFront, at, elapsed);
        EndScene();
    }
}

public partial class SpacioCreaturaIllustration : SpellScene
{
    private const int GreenRow = 5;
    private const int RedRow = 6;
    private const double IdleDuration = 0.6;
    private const double SelectionDuration = 0.5;
    private const double StunnedDuration = 1.2;
    private const int StunTurns = 4;

    public override void _Draw()
    {
        DrawRoom();
        var leg = IdleDuration + SelectionDuration + SpellFx.SwapDuration + StunnedDuration;
        var t = Time % (2 * leg);
        var swapped = t >= leg;
        var local = swapped ? t - leg : t;
        var greenCell = swapped ? RightCell : LeftCell;
        var redCell = swapped ? LeftCell : RightCell;

        DrawMagus(MiddleCell, 1f);

        var swapStart = IdleDuration + SelectionDuration;
        if (local < swapStart)
        {
            DrawBlob(greenCell, GreenRow, false, 1f);
            DrawBlob(redCell, RedRow, false, 1f);
            if (local >= IdleDuration)
            {
                var alpha = (float)Math.Clamp((local - IdleDuration) / SpellFx.ShockFadeInDuration, 0.0, 1.0);
                SpellFx.DrawShock(this, greenCell, alpha, Time);
            }

            EndScene();
            return;
        }

        var elapsed = local - swapStart;
        if (elapsed < SpellFx.SwapDuration)
        {
            var alpha = SpellFx.SwapMonsterAlpha(elapsed, out var atOrigin);
            DrawBlob(atOrigin ? greenCell : redCell, GreenRow, !atOrigin, alpha);
            DrawBlob(atOrigin ? redCell : greenCell, RedRow, !atOrigin, alpha);
            var (first, second) = SpellFx.SwapShockAlphas(elapsed);
            SpellFx.DrawShock(this, greenCell, first, Time);
            SpellFx.DrawShock(this, redCell, second, Time);
            EndScene();
            return;
        }

        DrawBlob(redCell, GreenRow, true, 1f);
        DrawBlob(greenCell, RedRow, true, 1f);
        SpellFx.DrawStun(this, redCell, StunTurns, Time);
        SpellFx.DrawStun(this, greenCell, StunTurns, Time);
        EndScene();
    }
}

public partial class StoryIllustration : SpellScene
{
    private const double ScrollSpeed = 24.0;
    private const int PatternWidth = 128;
    private const int DoorX = 0;
    private const int TorchX = 72;
    private const int WalkEastRow = 2;
    private const int BlobRow = 5;
    private const int BatRow = 9;
    private const int BatFrames = 3;
    private const int BatHeight = 26;
    private const int FireFrames = 6;
    private const double FireFrameDuration = 0.125;

    private static Texture2D? _doors;
    private static Texture2D? _fire;

    private static Texture2D Doors => _doors ??= Load("res://assets/sprites/donjon_portes.png");

    private static Texture2D Fire => _fire ??= Load("res://assets/sprites/donjon_feu.png");

    public override void _Draw()
    {
        BeginScene(SceneSize, 2f);
        var offset = -Mathf.Floor((float)(Time * ScrollSpeed % PatternWidth));
        for (var copy = 0; copy < 2; copy++)
        {
            var origin = offset + copy * PatternWidth;
            for (var x = 0; x < PatternWidth; x += 16)
            {
                DrawTile(53, origin + x, 0);
                DrawTile(70, origin + x, 16);
                DrawTile(87, origin + x, 32);
                DrawTile(138, origin + x, 48);
                DrawTile(250, origin + x, 48);
            }

            DrawTextureRectRegion(Doors, new Rect2(origin + DoorX, 16, 32, 32), new Rect2(32, 16 + 4 * 48, 32, 32));
            var fireFrame = (int)(Time / FireFrameDuration) % FireFrames;
            DrawTextureRectRegion(Fire, new Rect2(origin + TorchX, -6, 32, 48), new Rect2(39, fireFrame * 48, 32, 48));
        }

        var batFrame = (int)(Time * 8) % BatFrames;
        DrawCreature(new Vector2(4, 0 + Bob(0.0, 2f)), BatRow, (batFrame + 1) % BatFrames, BatHeight);
        DrawCreature(new Vector2(30, 6 + Bob(0.4, 2f)), BatRow, batFrame, BatHeight);

        var blobFrame = (int)(Time * 6) % 4;
        DrawCreature(new Vector2(14, 34 + Bob(0.2, 1f)), BlobRow, (blobFrame + 2) % 4, Cell);
        DrawCreature(new Vector2(42, 34 + Bob(0.6, 1f)), BlobRow, blobFrame, Cell);

        var magusFrame = (int)(Time * 12) % 6;
        DrawTextureRectRegion(Magus, new Rect2(78, 32, Cell, Cell), new Rect2(magusFrame * Cell, WalkEastRow * Cell, Cell, Cell));
        EndScene();
    }

    private float Bob(double phase, float height) => Mathf.Round((float)Math.Sin((Time + phase) * 9.0) * height);

    private void DrawCreature(Vector2 position, int row, int frame, int height) =>
        DrawTextureRectRegion(Animations, new Rect2(position, Cell, height), new Rect2(frame * Cell, row * Cell, Cell, height));
}
