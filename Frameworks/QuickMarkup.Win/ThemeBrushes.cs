namespace QuickMarkup.WinUI;

// The ThemeBrushes class is modified and inspired from Theme class
// of https://github.com/microsoft/microsoft-ui-reactor/blob/main/src/Reactor/Core/Theme.cs

public static class ThemeBrushes
{
    // ── Accent / Fill ────────────────────────────────────────────────
    public static Reference<Brush?> Accent            => ThemeResources.Get<Brush>("AccentFillColorDefaultBrush");
    public static Reference<Brush?> AccentSecondary   => ThemeResources.Get<Brush>("AccentFillColorSecondaryBrush");
    public static Reference<Brush?> AccentTertiary    => ThemeResources.Get<Brush>("AccentFillColorTertiaryBrush");
    public static Reference<Brush?> AccentDisabled    => ThemeResources.Get<Brush>("AccentFillColorDisabledBrush");

    // ── Text ─────────────────────────────────────────────────────────
    public static Reference<Brush?> PrimaryText       => ThemeResources.Get<Brush>("TextFillColorPrimaryBrush");
    public static Reference<Brush?> SecondaryText     => ThemeResources.Get<Brush>("TextFillColorSecondaryBrush");
    public static Reference<Brush?> TertiaryText      => ThemeResources.Get<Brush>("TextFillColorTertiaryBrush");
    public static Reference<Brush?> DisabledText      => ThemeResources.Get<Brush>("TextFillColorDisabledBrush");
    public static Reference<Brush?> AccentText        => ThemeResources.Get<Brush>("AccentTextFillColorPrimaryBrush");

    // ── Surfaces / Fill ──────────────────────────────────────────────
    public static Reference<Brush?> SolidBackground   => ThemeResources.Get<Brush>("SolidBackgroundFillColorBaseBrush");
    public static Reference<Brush?> CardBackground    => ThemeResources.Get<Brush>("CardBackgroundFillColorDefaultBrush");
    public static Reference<Brush?> SmokeFill         => ThemeResources.Get<Brush>("SmokeFillColorDefaultBrush");
    public static Reference<Brush?> SubtleFill        => ThemeResources.Get<Brush>("SubtleFillColorSecondaryBrush");
    public static Reference<Brush?> LayerFill         => ThemeResources.Get<Brush>("LayerFillColorDefaultBrush");

    // ── Control Fill ─────────────────────────────────────────────────
    public static Reference<Brush?> ControlFill              => ThemeResources.Get<Brush>("ControlFillColorDefaultBrush");
    public static Reference<Brush?> ControlFillSecondary     => ThemeResources.Get<Brush>("ControlFillColorSecondaryBrush");
    public static Reference<Brush?> ControlFillTertiary      => ThemeResources.Get<Brush>("ControlFillColorTertiaryBrush");
    public static Reference<Brush?> ControlFillDisabled      => ThemeResources.Get<Brush>("ControlFillColorDisabledBrush");
    public static Reference<Brush?> ControlFillInputActive   => ThemeResources.Get<Brush>("ControlFillColorInputActiveBrush");

    // ── Stroke / Border ──────────────────────────────────────────────
    public static Reference<Brush?> CardStroke        => ThemeResources.Get<Brush>("CardStrokeColorDefaultBrush");
    public static Reference<Brush?> SurfaceStroke     => ThemeResources.Get<Brush>("SurfaceStrokeColorDefaultBrush");
    public static Reference<Brush?> DividerStroke     => ThemeResources.Get<Brush>("DividerStrokeColorDefaultBrush");
    public static Reference<Brush?> ControlStroke     => ThemeResources.Get<Brush>("ControlStrokeColorDefaultBrush");
    public static Reference<Brush?> ControlStrokeSecondary => ThemeResources.Get<Brush>("ControlStrokeColorSecondaryBrush");

    // ── Signal ───────────────────────────────────────────────────────
    public static Reference<Brush?> SystemAttention   => ThemeResources.Get<Brush>("SystemFillColorAttentionBrush");
    public static Reference<Brush?> SystemSuccess     => ThemeResources.Get<Brush>("SystemFillColorSuccessBrush");
    public static Reference<Brush?> SystemCaution     => ThemeResources.Get<Brush>("SystemFillColorCautionBrush");
    public static Reference<Brush?> SystemCritical    => ThemeResources.Get<Brush>("SystemFillColorCriticalBrush");
    public static Reference<Brush?> SystemNeutral     => ThemeResources.Get<Brush>("SystemFillColorNeutralBrush");
    public static Reference<Brush?> SystemSolidNeutral => ThemeResources.Get<Brush>("SystemFillColorSolidNeutralBrush");

    public static Reference<Brush?> SystemAttentionBackground => ThemeResources.Get<Brush>("SystemFillColorAttentionBackgroundBrush");
    public static Reference<Brush?> SystemSuccessBackground   => ThemeResources.Get<Brush>("SystemFillColorSuccessBackgroundBrush");
    public static Reference<Brush?> SystemCautionBackground   => ThemeResources.Get<Brush>("SystemFillColorCautionBackgroundBrush");
    public static Reference<Brush?> SystemCriticalBackground  => ThemeResources.Get<Brush>("SystemFillColorCriticalBackgroundBrush");
    public static Reference<Brush?> SystemNeutralBackground   => ThemeResources.Get<Brush>("SystemFillColorNeutralBackgroundBrush");
    public static Reference<Brush?> SystemSolidAttention       => ThemeResources.Get<Brush>("SystemFillColorSolidAttentionBackgroundBrush");
}