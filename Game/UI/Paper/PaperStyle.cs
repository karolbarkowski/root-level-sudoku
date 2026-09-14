using Godot;

namespace SudokuEndless;

/// <summary>Shared print palette and bundled, redistributable typography.</summary>
public static class PaperStyle
{
    public static readonly Color Ink = new("292a28");
    public static readonly Color Paper = new("eeeae0");
    public static readonly Color Burgundy = new("80363E");
    public static readonly Color Muted = new("777770");

    private static Font _display;
    private static Font _body;

    // Cached: these are read from _Draw, which runs per frame while a button's hover tween plays.
    public static Font Display => _display ??= GD.Load<Font>("res://Resources/fonts/Anton/Anton-Regular.ttf");
    public static Font Body => _body ??= GD.Load<Font>("res://Resources/fonts/Alata-Regular.ttf");

    /// <summary>Draws a hairline rule around <paramref name="rect"/>, inset rather than centred on it.</summary>
    public static void DrawBorder(CanvasItem target, Rect2 rect, Color color, float thickness = 1.5f)
    {
        // A rule must cover at least one physical pixel when the portrait sheet is scaled down.
        thickness = Mathf.Max(2f, Mathf.Max(thickness, 1f / Mathf.Max(.01f, target.GetGlobalTransformWithCanvas().Scale.X)));
        target.DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X, thickness)), color);
        target.DrawRect(new Rect2(rect.Position + new Vector2(0, rect.Size.Y - thickness), new Vector2(rect.Size.X, thickness)), color);
        target.DrawRect(new Rect2(rect.Position, new Vector2(thickness, rect.Size.Y)), color);
        target.DrawRect(new Rect2(rect.Position + new Vector2(rect.Size.X - thickness, 0), new Vector2(thickness, rect.Size.Y)), color);
    }
}
