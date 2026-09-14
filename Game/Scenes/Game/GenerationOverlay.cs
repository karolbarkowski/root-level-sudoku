using Godot;
using Sudoku;

namespace SudokuEndless;

/// <summary>Animated, input-blocking loading state shown while a puzzle is generated.</summary>
[Tool]
public partial class GenerationOverlay : Control
{
    private Tween _exit;
    private string _title = "GENERATING PUZZLE";

    /// <summary>Message shown above the spinner while this overlay is active.</summary>
    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? string.Empty;
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        QueueRedraw();
    }

    public void HideAnimated()
    {
        _exit?.Kill();
        if (!UiAnimationSettings.Default.Enabled)
        {
            Visible = false;
            return;
        }
        _exit = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        _exit.TweenProperty(this, "modulate:a", 0f, .22);
        _exit.TweenCallback(Callable.From(() => Visible = false));
    }

    public override void _ExitTree() => _exit?.Kill();

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), PaperStyle.Paper);
        Vector2 center = new(Size.X / 2, Size.Y / 2 - 22);
        string title = _title;
        float width = PaperStyle.Body.GetStringSize(title, fontSize: 18).X;
        DrawString(PaperStyle.Body, new Vector2((Size.X - width) / 2, center.Y + 66), title, fontSize: 18, modulate: PaperStyle.Ink);
        string detail = "Preparing your board...";
        float detailWidth = PaperStyle.Body.GetStringSize(detail, fontSize: 14).X;
        DrawString(PaperStyle.Body, new Vector2((Size.X - detailWidth) / 2, center.Y + 94), detail, fontSize: 14, modulate: PaperStyle.Muted);
    }
}
