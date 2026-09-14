using Godot;

namespace SudokuEndless;

/// <summary>Shared charcoal/orange palette. Legacy names preserve existing scene references.</summary>
public static class PaperStyle
{
    public static readonly Color Ink = new("F4F5F5");
    public static readonly Color Paper = new("272F33");
    public static readonly Color Burgundy = new("F58220");
    public static readonly Color Muted = new("A4ADB1");
    public static readonly Color Surface = new("41494D");

    private static Font _display;
    private static Font _body;

    // Cached: these are read from _Draw, which runs per frame while a button's hover tween plays.
    public static Font Display => _display ??= GD.Load<Font>("res://Resources/fonts/Anton/Anton-Regular.ttf");
    public static Font Body => _body ??= GD.Load<Font>("res://Resources/fonts/Alata-Regular.ttf");

    private static ShaderMaterial _iconTint;

    /// <summary>
    /// Material for a control that draws tinted icons: every texture is painted in its draw colour,
    /// so black SVGs take any colour. Shared, since it has no per-control state.
    /// </summary>
    public static ShaderMaterial IconTint => _iconTint ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://UI/Paper/IconTint.gdshader") };

}
