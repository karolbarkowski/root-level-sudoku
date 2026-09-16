using Godot;
using Sudoku;

namespace SudokuEndless;

/// <summary>Reusable native button: mouse, touch, keyboard, focus and disabled semantics are inherited.</summary>
[Tool]
public partial class PaperButton : Button
{
    [Export] public string Caption { get; set; } = "Easy";
    [Export] public string Index { get; set; } = "01";
    [Export] public bool Secondary { get; set; }
    [Export] public bool Accent { get; set; }

    /// <summary>Low-emphasis secondary action: no surface until hovered or pressed, muted caption.</summary>
    [Export] public bool Ghost { get; set; }
    [Export] public int CaptionSize { get; set; } = 24;
    /// <summary>Optional icon drawn before a secondary button's caption, in the accent colour.</summary>
    [Export] public Texture2D LeadingIcon { get; set; }

    private const float IconSize = 26;
    private const float ArrowSize = 36;
    private static Texture2D _arrow;
    // The Back button's chevron, mirrored, so every arrow in the UI shares one icon.
    private static Texture2D Arrow => _arrow ??= GD.Load<Texture2D>("res://Resources/icons/arrow-left.svg");
    private const float IconGap = 10;
    private float _highlight;
    private float _depression;
    private bool _hovered;
    private Tween _feedback;
    private Tween _entry;
    private float _entryOffset;
    private float _entryScale = 1;
    private bool _touchInput;
    private bool _held;
    private readonly StyleBoxFlat _surface = new() { CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14 };

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

    /// <summary>How far the button sinks as it leaves.</summary>
    private const float ExitSink = 12;

    /// <summary>Sinks and shrinks slightly; the caller fades the row. The caller owns the stagger.</summary>
    public void PlayExit(double delay, double duration)
    {
        _entry?.Kill();
        if (!UiAnimationSettings.Default.Enabled) return;
        _entry = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        _entry.TweenMethod(Callable.From<float>(v => { _entryOffset = v; QueueRedraw(); }), _entryOffset, ExitSink, duration).SetDelay(delay);
        _entry.TweenMethod(Callable.From<float>(v => { _entryScale = v; QueueRedraw(); }), _entryScale, .98f, duration).SetDelay(delay);
    }

    /// <summary>Keeps the highlight on after release, marking this as the choice that was made.</summary>
    public void HoldHighlight()
    {
        _held = true;
        Animate();
    }

    public void ReleaseHighlight()
    {
        _held = false;
        Animate();
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
        if (LeadingIcon != null || !Secondary) Material = PaperStyle.IconTint;
        // Icons are rasterized larger than drawn; mipmaps keep the downscale crisp.
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        AccessibilityName = Secondary ? Caption : $"Start {Caption} puzzle";
        CustomMinimumSize = new Vector2(100, Ghost ? 52 : Secondary ? 66 : 68);
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
        float highlight = !Disabled && ((!_touchInput && (_hovered || HasFocus())) || IsPressed() || _held) ? 1 : 0;
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
        Color foreground = PaperStyle.Ink;
        if (Disabled) foreground = foreground with { A = .35f };
        float inset = _depression * 2;
        var rect = new Rect2(new Vector2(inset, inset + _depression), Size - Vector2.One * inset * 2);
        if (Ghost)
        {
            foreground = PaperStyle.Muted.Lerp(PaperStyle.Ink, h);
            _surface.BgColor = PaperStyle.Surface with { A = h };
        }
        else _surface.BgColor = Accent ? PaperStyle.Burgundy.Lightened(h * .12f) : PaperStyle.Surface.Lerp(PaperStyle.Burgundy, h);
        DrawStyleBox(_surface, rect);
        float baseline = Size.Y / 2 + (Secondary ? 9 : 12) + _depression;
        if (Secondary)
        {
            float width = PaperStyle.Body.GetStringSize(Caption, fontSize: CaptionSize).X;
            float extra = LeadingIcon == null ? 0 : IconSize + IconGap;
            float left = (Size.X - width - extra) / 2;
            DrawString(PaperStyle.Body, new Vector2(left + extra, baseline), Caption, fontSize: CaptionSize, modulate: foreground);
            if (LeadingIcon != null)
            {
                // Accent where it contrasts; on an orange (accent or highlighted) surface, follow the caption.
                Color icon = Ghost ? PaperStyle.Burgundy : Accent ? foreground : PaperStyle.Burgundy.Lerp(foreground, h);
                if (Disabled) icon.A = .35f;
                DrawTextureRect(LeadingIcon, new Rect2(left, (Size.Y - IconSize) / 2 + _depression, IconSize, IconSize), false, icon);
            }
        }
        else
        {
            DrawString(PaperStyle.Body, new Vector2(20, baseline - 3), Index, fontSize: 20, modulate: PaperStyle.Burgundy.Lerp(PaperStyle.Ink, h));
            DrawString(PaperStyle.Body, new Vector2(78, baseline), Caption, fontSize: 31, modulate: foreground);
            // x is the chevron's tip; in the mirrored icon the tip sits two thirds across.
            float x = Size.X - 31 + h * 3 - inset;
            float y = Size.Y / 2 + _depression;
            float left = x - ArrowSize * 2 / 3;
            // A negative width mirrors the left-pointing icon.
            DrawTextureRect(Arrow, new Rect2(left + ArrowSize, y - ArrowSize / 2, -ArrowSize, ArrowSize), false, foreground);
        }
    }
}
