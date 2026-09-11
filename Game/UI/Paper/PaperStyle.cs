using Godot;

namespace SudokuEndless;

/// <summary>Shared print palette and bundled, redistributable typography.</summary>
public static class PaperStyle
{
    public static readonly Color Ink = new("292a28");
    public static readonly Color Paper = new("eeeae0");
    public static readonly Color Burgundy = new("87464b");
    public static readonly Color Muted = new("777770");
    public static Font Display => GD.Load<Font>("res://Resources/fonts/Anton/Anton-Regular.ttf");
    public static Font Body => GD.Load<Font>("res://Resources/fonts/Alata-Regular.ttf");
}
