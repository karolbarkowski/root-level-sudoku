using Godot;
using Sudoku;

namespace SudokuEndless;

/// <summary>
/// Modal yes/no question: a dimmed screen with a card and two buttons. Add it as the last child of
/// a full-screen Control, which sizes it (anchors alone do not resolve against every root). It stays
/// hidden until <see cref="Open"/>; tapping the dimmed area cancels.
/// </summary>
public partial class PaperConfirm : Control
{
    [Signal] public delegate void ConfirmedEventHandler();

    [Export] public string Title { get; set; } = "Are you sure?";
    [Export(PropertyHint.MultilineText)] public string Body { get; set; } = "";
    [Export] public string ConfirmCaption { get; set; } = "OK";
    [Export] public string CancelCaption { get; set; } = "Cancel";

    // The card, in design units; scaled like the game sheet.
    private const float CardWidth = 476;
    private const float CardHeight = 250;
    private const float Padding = 28;
    private const float ButtonHeight = 66;
    private const float Rise = 16;

    private Control _card;
    private PaperButton _confirm;
    private PaperButton _cancel;
    private Tween _tween;
    private float _progress;
    private readonly StyleBoxFlat _cardStyle = new()
    {
        BgColor = PaperStyle.Paper.Lightened(.04f),
        CornerRadiusTopLeft = 18, CornerRadiusTopRight = 18, CornerRadiusBottomLeft = 18, CornerRadiusBottomRight = 18,
        BorderColor = PaperStyle.Surface,
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
    };

    public bool IsOpen { get; private set; }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.TopLeft);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _card = new Control { MouseFilter = MouseFilterEnum.Stop, Size = new Vector2(CardWidth, CardHeight) };
        _card.Draw += DrawCard;
        AddChild(_card);

        var scene = GD.Load<PackedScene>("res://UI/Paper/PaperButton.tscn");
        _cancel = scene.Instantiate<PaperButton>();
        _cancel.Caption = CancelCaption;
        _cancel.Secondary = true;
        _confirm = scene.Instantiate<PaperButton>();
        _confirm.Caption = ConfirmCaption;
        _confirm.Secondary = true;
        _confirm.Accent = true;
        float buttonWidth = (CardWidth - Padding * 2 - 12) / 2;
        float buttonY = CardHeight - Padding - ButtonHeight;
        foreach (var (button, x) in new[] { (_cancel, Padding), (_confirm, Padding + buttonWidth + 12) })
        {
            _card.AddChild(button);
            button.Position = new Vector2(x, buttonY);
            button.Size = new Vector2(buttonWidth, ButtonHeight);
        }
        _cancel.Pressed += () => Close(false);
        _confirm.Pressed += () => Close(true);

        Resized += Layout;
        Layout();
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        Visible = true;
        Animate(1, .22, Tween.EaseType.Out);
    }

    /// <summary>Dismisses the dialog; <see cref="Confirmed"/> fires straight away when accepted.</summary>
    public void Close(bool confirmed)
    {
        if (!IsOpen) return;
        IsOpen = false;
        Animate(0, .16, Tween.EaseType.In);
        if (confirmed) EmitSignal(SignalName.Confirmed);
    }

    public override void _GuiInput(InputEvent @event)
    {
        // Only presses reaching the dimmed area arrive here; the card and its buttons stop their own.
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Close(false);
            AcceptEvent();
        }
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
        Modulate = new Color(1, 1, 1, progress);
        Visible = progress > 0 || IsOpen;
        Layout();
        QueueRedraw();
    }

    private void Layout()
    {
        if (_card == null) return;
        // Same fit as the game sheet, so the card matches the board's width.
        float scale = Mathf.Min(Size.X / 540, Size.Y / 980);
        _card.Scale = Vector2.One * scale;
        _card.Position = new Vector2((Size.X - CardWidth * scale) / 2, (Size.Y - CardHeight * scale) / 2 + Rise * (1 - _progress) * scale);
    }

    public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 0, 0, .55f));

    private void DrawCard()
    {
        _card.DrawStyleBox(_cardStyle, new Rect2(Vector2.Zero, _card.Size));
        _card.DrawString(PaperStyle.Body, new Vector2(Padding, Padding + 26), Title, fontSize: 28, modulate: PaperStyle.Ink);
        _card.DrawMultilineString(PaperStyle.Body, new Vector2(Padding, Padding + 68), Body, HorizontalAlignment.Left, CardWidth - Padding * 2, 18, -1, PaperStyle.Muted);
    }

    public override void _ExitTree() => _tween?.Kill();
}
