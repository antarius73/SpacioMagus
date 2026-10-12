using System;
using Godot;

namespace SpacioMagus;

public static class SpellFx
{
    public const double AuraRingFadeDuration = 0.1;
    public const double AuraFlameFadeDuration = 0.2;
    public const double MagusFadeDuration = 0.2;
    public const double AuraSwitchDuration = 0.1;
    public const double TeleportHalfDuration = AuraRingFadeDuration + AuraFlameFadeDuration + MagusFadeDuration + AuraSwitchDuration;
    public const double TeleportDuration = 2 * TeleportHalfDuration;
    public const double AuraFrameDuration = 0.13;
    public const int AuraFrames = 7;
    public const int AuraSize = 96;
    public const int AuraOffsetX = -32;
    public const int AuraOffsetY = -35;
    public const int AuraFlameRow = 0;
    public const int AuraRingRow = 1;

    public const double ShockFadeInDuration = 0.1;
    public const double ShockMonsterFadeDuration = 0.2;
    public const double ShockHoldDuration = 0.1;
    public const double ShockFadeOutDuration = 0.2;
    public const double ShockSelectionFadeOutDuration = 0.15;
    public const double SwapDuration = ShockFadeInDuration + 2 * ShockMonsterFadeDuration + ShockHoldDuration + ShockFadeOutDuration;
    public const double ShockFrameDuration = 0.1;
    public const int ShockFrames = 7;
    public const int ShockSize = 44;
    public const int ShockOffset = (32 - ShockSize) / 2;

    public const int StunSwirlWidth = 26;
    public const int StunSwirlHeight = 21;
    public const int StunSwirlFrames = 20;
    public const double StunSwirlFrameDuration = 0.07;
    public const float StunOffsetX = 16f;
    public const float StunOffsetY = 2f;

    public const int MutationSize = 28;
    public const int MutationFrames = 22;
    public const double MutationFrameDuration = 0.06;
    public const float MutationOffsetY = -2f;

    public const int SakuraSize = 96;
    public const int SakuraFrames = 7;
    public const int SakuraColumns = 5;
    public const double SakuraFrameDuration = 0.065;
    public const double SakuraDuration = SakuraFrames * SakuraFrameDuration;

    private static Texture2D? _sakura;
    private static Texture2D? _mutation;
    private static Texture2D? _auraBack;
    private static Texture2D? _auraFront;
    private static Texture2D? _shock;
    private static Texture2D? _stunSwirl;

    public static Texture2D AuraBack => _auraBack ??= Load("res://assets/fx/aura_arriere.png");

    public static Texture2D AuraFront => _auraFront ??= Load("res://assets/fx/aura_avant.png");

    public static Texture2D Shock => _shock ??= Load("res://assets/fx/aura_electrique.png");

    public static Texture2D StunSwirl => _stunSwirl ??= Load("res://assets/fx/etourdi.png");

    public static Texture2D Mutation => _mutation ??= Load("res://assets/fx/mutation.png");

    public static Texture2D Sakura => _sakura ??= Load("res://assets/fx/sakura.png");

    public static bool IsDeparting(double elapsed) => elapsed < TeleportHalfDuration;

    public static float TeleportMagusAlpha(double elapsed)
    {
        var departing = IsDeparting(elapsed);
        var local = departing ? elapsed : elapsed - TeleportHalfDuration;
        var start = departing ? AuraRingFadeDuration : AuraFlameFadeDuration;
        var progress = (float)Math.Clamp((local - start) / MagusFadeDuration, 0.0, 1.0);
        return departing ? 1f - progress : progress;
    }

    public static void DrawTeleportAura(CanvasItem canvas, Texture2D layer, Vector2 cellOrigin, double elapsed)
    {
        var departing = IsDeparting(elapsed);
        var local = departing ? elapsed : elapsed - TeleportHalfDuration;
        var appear = departing ? AuraRingFadeDuration : AuraFlameFadeDuration;
        var vanish = departing ? AuraFlameFadeDuration : AuraRingFadeDuration;
        var fadeIn = Math.Clamp(local / appear, 0.0, 1.0);
        var fadeOut = Math.Clamp((TeleportHalfDuration - local) / vanish, 0.0, 1.0);
        var alpha = (float)Math.Min(fadeIn, fadeOut);
        if (alpha <= 0f)
        {
            return;
        }

        var switched = (float)Math.Clamp((local - appear - MagusFadeDuration) / AuraSwitchDuration, 0.0, 1.0);
        var flame = departing ? switched : 1f - switched;
        var frame = (int)(elapsed / AuraFrameDuration) % AuraFrames;
        var target = new Rect2(cellOrigin.X + AuraOffsetX, cellOrigin.Y + AuraOffsetY, AuraSize, AuraSize);
        DrawAuraRow(canvas, layer, target, frame, AuraFlameRow, alpha * flame);
        DrawAuraRow(canvas, layer, target, frame, AuraRingRow, alpha * (1f - flame));
    }

    public static float SwapMonsterAlpha(double elapsed, out bool atOrigin)
    {
        var outStart = ShockFadeInDuration;
        var inStart = outStart + ShockMonsterFadeDuration + ShockHoldDuration;
        atOrigin = elapsed < inStart;
        if (atOrigin)
        {
            return 1f - (float)Math.Clamp((elapsed - outStart) / ShockMonsterFadeDuration, 0.0, 1.0);
        }

        return (float)Math.Clamp((elapsed - inStart) / ShockMonsterFadeDuration, 0.0, 1.0);
    }

    public static (float First, float Second) SwapShockAlphas(double elapsed)
    {
        var fadeOut = (float)Math.Clamp((SwapDuration - elapsed) / ShockFadeOutDuration, 0.0, 1.0);
        var secondIn = (float)Math.Clamp(elapsed / ShockFadeInDuration, 0.0, 1.0);
        return (fadeOut, Math.Min(secondIn, fadeOut));
    }

    public static void DrawShock(CanvasItem canvas, Vector2 cellOrigin, float alpha, double time)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var frame = (int)(time / ShockFrameDuration) % ShockFrames;
        var target = new Rect2(cellOrigin.X + ShockOffset, cellOrigin.Y + ShockOffset, ShockSize, ShockSize);
        canvas.DrawTextureRectRegion(Shock, target, new Rect2(frame * ShockSize, 0, ShockSize, ShockSize), new Color(1f, 1f, 1f, alpha));
    }

    public static void DrawStun(CanvasItem canvas, Vector2 cellOrigin, int turns, double time)
    {
        var center = new Vector2(cellOrigin.X + StunOffsetX, cellOrigin.Y + StunOffsetY);
        var frame = (int)(time / StunSwirlFrameDuration) % StunSwirlFrames;
        var target = new Rect2(center.X - StunSwirlWidth / 2, center.Y - StunSwirlHeight / 2, StunSwirlWidth, StunSwirlHeight);
        canvas.DrawTextureRectRegion(StunSwirl, target, new Rect2(frame * StunSwirlWidth, 0, StunSwirlWidth, StunSwirlHeight));
        GuiAssets.DrawNumber(canvas, turns, center);
    }

    public static void DrawSakura(CanvasItem canvas, Vector2 cellOrigin, double elapsed)
    {
        if (elapsed < 0 || elapsed >= SakuraDuration)
        {
            return;
        }

        var frame = (int)(elapsed / SakuraFrameDuration);
        var source = new Rect2(frame % SakuraColumns * SakuraSize, frame / SakuraColumns * SakuraSize, SakuraSize, SakuraSize);
        var target = new Rect2(cellOrigin.X + 16 - SakuraSize / 2f, cellOrigin.Y + 16 - SakuraSize / 2f, SakuraSize, SakuraSize);
        canvas.DrawTextureRectRegion(Sakura, target, source);
    }

    public static void DrawMutation(CanvasItem canvas, Vector2 cellOrigin, double time)
    {
        var frame = (int)(time / MutationFrameDuration) % MutationFrames;
        var target = new Rect2(
            cellOrigin.X + StunOffsetX - MutationSize / 2f,
            cellOrigin.Y + MutationOffsetY - MutationSize / 2f,
            MutationSize,
            MutationSize);
        canvas.DrawTextureRectRegion(Mutation, target, new Rect2(frame * MutationSize, 0, MutationSize, MutationSize));
    }

    private static void DrawAuraRow(CanvasItem canvas, Texture2D layer, Rect2 target, int frame, int row, float alpha)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var source = new Rect2(frame * AuraSize, row * AuraSize, AuraSize, AuraSize);
        canvas.DrawTextureRectRegion(layer, target, source, new Color(1f, 1f, 1f, alpha));
    }

    private static Texture2D Load(string path) =>
        GD.Load<Texture2D>(path) ?? throw new InvalidOperationException($"Texture introuvable : {path}");
}
