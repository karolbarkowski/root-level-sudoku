using Godot;
using Sudoku;

namespace SudokuEndless;

/// <summary>
/// The settings during a game: a sheet that slides up from the bottom edge over the dimmed board.
/// Done, a tap on the dimmed area, or Back closes it. While open it takes every tap, so nothing
/// underneath changes by accident.
/// </summary>
public partial class SettingsSheet : Control
{
    /// <summary>Emitted after any setting changes, so the game can redraw what depends on it.</summary>
    [Signal] public delegate void ChangedEventHandler();

    // Content, in design units; scaled with the game sheet.
    private const float ColumnWidth = 476;
    private const float PaddingTop = 28;
    private const float PaddingBottom = 28;
    private const float CornerRadius = 28;
    private const double OpenDuration = .35;
    private const double CloseDuration = .25;
    private const float DimAlpha = .55f;

    private VBoxContainer _content;
    private Tween _tween;
    private float _progress;
    private float _scale = 1;
    private readonly StyleBoxFlat _sheet = new()
    {
        BgColor = PaperStyle.Paper.Lightened(.05f),
        BorderColor = PaperStyle.Surface,
        BorderWidthTop = 1,
    };

    public bool IsOpen { get; private set; }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.TopLeft);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _content = new VBoxContainer { Name = "Content" };
        _content.AddThemeConstantOverride("separation", 16);
        AddChild(_content);
        var title = new Label { Text = "Settings" };
        title.AddThemeFontOverride("font", PaperStyle.Display);
        title.AddThemeFontSizeOverride("font_size", 42);
        _content.AddChild(title);
        var view = new SettingsView { Name = "View", Separation = 18 };
        view.Changed += () => EmitSignal(SignalName.Changed);
        _content.AddChild(view);
        _content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8), MouseFilter = MouseFilterEnum.Ignore });
        var done = GD.Load<PackedScene>("res://UI/Paper/PaperButton.tscn").Instantiate<PaperButton>();
        done.Name = "Done";
        done.Caption = "Done";
        done.Secondary = true;
        done.Pressed += Close;
        _content.AddChild(done);

        Resized += Apply;
        Apply();
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        Visible = true;
        Animate(1, OpenDuration, Tween.EaseType.Out);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Animate(0, CloseDuration, Tween.EaseType.In);
    }

    public override void _GuiInput(InputEvent @event)
    {
        // Every press lands here unless a control on the sheet took it; only the dimmed area closes.
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } press) return;
        if (press.Position.Y < SheetTop) Close();
        AcceptEvent();
    }

    private void Animate(float target, double duration, Tween.EaseType ease)
    {
        _tween?.Kill();
        if (!UiAnimationSettings.Default.Enabled)
        {
            SetProgress(target);
            return;
        }
        _tween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(ease);
        _tween.TweenMethod(Callable.From<float>(SetProgress), _progress, target, duration);
    }

    private void SetProgress(float progress)
    {
        _progress = progress;
        Visible = progress > 0 || IsOpen;
        Apply();
    }

    /// <summary>The sheet's full height on screen, in pixels.</summary>
    private float SheetHeight => (PaddingTop + _content.Size.Y + PaddingBottom) * _scale;

    /// <summary>Where the sheet's top edge is right now; below the screen when closed.</summary>
    private float SheetTop => Size.Y - SheetHeight * _progress;

    private void Apply()
    {
        if (_content == null || Size.X <= 0 || Size.Y <= 0) return;
        // Same fit as the game sheet, so the rows line up with the board's width.
        _scale = Mathf.Min(Size.X / 540, Size.Y / 980);
        _content.Size = new Vector2(ColumnWidth, _content.GetCombinedMinimumSize().Y);
        _content.Scale = Vector2.One * _scale;
        _content.Position = new Vector2((Size.X - ColumnWidth * _scale) / 2, SheetTop + PaddingTop * _scale);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_progress <= 0) return;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 0, 0, DimAlpha * _progress));
        float radius = CornerRadius * _scale;
        _sheet.CornerRadiusTopLeft = _sheet.CornerRadiusTopRight = Mathf.RoundToInt(radius);
        // Runs past the bottom edge so the rounded corners never show there.
        DrawStyleBox(_sheet, new Rect2(0, SheetTop, Size.X, SheetHeight + radius));
    }

    public override void _ExitTree() => _tween?.Kill();
}
