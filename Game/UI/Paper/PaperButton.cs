using Godot;
using Sudoku;

namespace SudokuEndless;

/// <summary>Reusable native button: mouse, touch, keyboard, focus and disabled semantics are inherited.</summary>
[Tool]
public partial class PaperButton : Button
{
    public enum PaperIcon { None, Settings, Exit }
    [Export] public string Caption { get; set; } = "Easy";
    [Export] public string Index { get; set; } = "01";
    [Export] public bool Secondary { get; set; }
    [Export] public PaperIcon Symbol { get; set; }
    private float _highlight;
    private float _depression;
    private bool _hovered;
    private Tween _feedback;
    private Tween _entry;
    private float _entryOffset;
    private float _entryScale = 1;
    private bool _touchInput;

    public override void _Input(InputEvent input)
    {
        bool touch = _touchInput;
        if (input is InputEventScreenTouch) touch = true;
        else if (input is InputEventKey || input is InputEventMouseMotion && input.Device >= 0) touch = false;
        if (touch == _touchInput) return;
        _touchInput = touch;
        Animate();
    }

    /// <summary>How far below its resting place the button starts. Small on purpose — a nudge, not a fly-in.</summary>
    private const float EntryRise = 26;

    public void PrepareEntrance()
    {
        _entry?.Kill();
        _entryOffset = EntryRise;
        _entryScale = .96f;
        QueueRedraw();
    }

    /// <summary>Rises into place. The caller owns the stagger; this only knows its own slot.</summary>
    public void PlayEntrance(double delay, double duration)
    {
        if (!UiAnimationSettings.Default.Enabled)
        {
            // Still land at rest: a prepared button would otherwise stay parked below the line.
            _entryOffset = 0;
            _entryScale = 1;
            QueueRedraw();
            return;
        }
        PrepareEntrance();
        _entry = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _entry.TweenMethod(Callable.From<float>(v => { _entryOffset = v; QueueRedraw(); }), EntryRise, 0f, duration).SetDelay(delay);
        _entry.TweenMethod(Callable.From<float>(v => { _entryScale = v; QueueRedraw(); }), .96f, 1f, duration).SetDelay(delay);
    }

    public override void _Ready()
    {
        MouseDefaultCursorShape = CursorShape.PointingHand;
        AccessibilityName = Secondary ? Caption : $"Start {Caption} puzzle";
        CustomMinimumSize = new Vector2(100, Secondary ? 66 : 68);
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        MouseEntered += () => { _hovered = true; Animate(); };
        MouseExited += () => { _hovered = false; Animate(); };
        FocusEntered += Animate;
        FocusExited += Animate;
        ButtonDown += Animate;
        ButtonUp += Animate;
        Resized += QueueRedraw;
        SetProcess(Engine.IsEditorHint());
    }

    private void Animate()
    {
        _feedback?.Kill();
        float highlight = !Disabled && ((!_touchInput && (_hovered || HasFocus())) || IsPressed()) ? 1 : 0;
        float depression = !Disabled && IsPressed() ? 1 : 0;
        if (!UiAnimationSettings.Default.Enabled)
        {
            _highlight = highlight;
            _depression = depression;
            QueueRedraw();
            return;
        }
        _feedback = CreateTween().SetParallel().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        _feedback.TweenMethod(Callable.From<float>(v => { _highlight = v; QueueRedraw(); }), _highlight, highlight, .10);
        _feedback.TweenMethod(Callable.From<float>(v => { _depression = v; QueueRedraw(); }), _depression, depression, depression > 0 ? .06 : .10);
    }

    public override void _Process(double delta) { if (Engine.IsEditorHint()) QueueRedraw(); }
    public override void _ExitTree() { _feedback?.Kill(); _entry?.Kill(); }

    public override void _Draw()
    {
        DrawSetTransform(Size * (1 - _entryScale) / 2 + new Vector2(0, _entryOffset), 0, Vector2.One * _entryScale);
        float h = Disabled ? 0 : _highlight;
        Color foreground = PaperStyle.Ink.Lerp(PaperStyle.Paper, h);
        if (Disabled) foreground = foreground with { A = .35f };
        float inset = _depression * 2;
        var rect = new Rect2(new Vector2(inset, inset + _depression), Size - Vector2.One * inset * 2);
        DrawRect(rect, PaperStyle.Ink with { A = h });
        Border(rect, PaperStyle.Ink with { A = Disabled ? .3f : 1 });
        float baseline = Size.Y / 2 + (Secondary ? 9 : 12) + _depression;
        if (Secondary)
        {
            float width = PaperStyle.Display.GetStringSize(Caption, fontSize: 24).X;
            float extra = Symbol == PaperIcon.None ? 0 : 34;
            float left = (Size.X - width - extra) / 2;
            DrawString(PaperStyle.Display, new Vector2(left + extra, baseline), Caption, fontSize: 24, modulate: foreground);
            if (Symbol != PaperIcon.None) DrawIcon(new Vector2(left + 11, Size.Y / 2 + _depression), foreground);
        }
        else
        {
            DrawRect(new Rect2(rect.Position, new Vector2(60, rect.Size.Y)), PaperStyle.Burgundy with { A = h });
            DrawLine(new Vector2(60, inset), new Vector2(60, Size.Y - inset), PaperStyle.Ink with { A = 1 - h }, 1);
            DrawString(PaperStyle.Display, new Vector2(20, baseline - 3), Index, fontSize: 22, modulate: foreground);
            DrawString(PaperStyle.Display, new Vector2(78, baseline), Caption, fontSize: 31, modulate: foreground);
            float x = Size.X - 31 + h * 3 - inset;
            float y = Size.Y / 2 + _depression;
            DrawLine(new Vector2(x - 23, y), new Vector2(x, y), foreground, 1.6f, true);
            DrawPolyline(new[] { new Vector2(x - 10, y - 10), new Vector2(x, y), new Vector2(x - 10, y + 10) }, foreground, 1.6f, true);
        }
        if (HasFocus() && !_touchInput) DrawRect(rect.Grow(-5), PaperStyle.Paper.Lerp(PaperStyle.Burgundy, 1 - h), false, 1);
    }

    private void Border(Rect2 rect, Color color)
    {
        const float thickness = 1.5f;
        DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X, thickness)), color);
        DrawRect(new Rect2(rect.Position + new Vector2(0, rect.Size.Y - thickness), new Vector2(rect.Size.X, thickness)), color);
        DrawRect(new Rect2(rect.Position, new Vector2(thickness, rect.Size.Y)), color);
        DrawRect(new Rect2(rect.Position + new Vector2(rect.Size.X - thickness, 0), new Vector2(thickness, rect.Size.Y)), color);
    }

    private void DrawIcon(Vector2 center, Color color)
    {
        if (Symbol == PaperIcon.Settings)
        {
            DrawArc(center, 8, 0, Mathf.Tau, 24, color, 1.5f, true);
            DrawArc(center, 3, 0, Mathf.Tau, 16, color, 1.5f, true);
            for (int i = 0; i < 8; i++)
            {
                Vector2 direction = Vector2.FromAngle(i * Mathf.Tau / 8);
                DrawLine(center + direction * 8, center + direction * 12, color, 3, true);
            }
        }
        else
        {
            DrawPolyline(new[] { center + new Vector2(1,-11), center + new Vector2(-10,-11), center + new Vector2(-10,11), center + new Vector2(1,11) }, color, 1.5f, true);
            DrawLine(center + new Vector2(-4,0), center + new Vector2(12,0), color, 1.5f, true);
            DrawPolyline(new[] { center + new Vector2(6,-6), center + new Vector2(12,0), center + new Vector2(6,6) }, color, 1.5f, true);
        }
    }
}
