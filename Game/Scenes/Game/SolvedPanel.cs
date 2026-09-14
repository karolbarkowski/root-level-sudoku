using Godot;
using Generators.Sudoku;
using Sudoku;

namespace SudokuEndless;

/// <summary>
/// Shown once the puzzle is solved: a sheet that slides up from the bottom edge over the controls
/// below the board, with a congratulation and the next steps. While open it takes every tap, so the
/// finished board can be admired but not edited.
/// </summary>
[Tool]
public partial class SolvedPanel : Control
{
    [Signal] public delegate void NewPuzzleRequestedEventHandler();
    [Signal] public delegate void MenuRequestedEventHandler();

    // Content, in design units; scaled with the game sheet.
    private const float ColumnWidth = 476;
    private const float ContentHeight = 194;
    private const float ButtonTop = 128;
    private const float ButtonHeight = 66;
    private const float CornerRadius = 28;

    /// <summary>Lets the last number land before the sheet arrives.</summary>
    private const double OpenDelay = .35;
    private const double OpenDuration = .5;

    private Control _content;
    private PaperButton _newPuzzle;
    private PaperButton _menu;
    private Tween _tween;
    private float _progress;
    private float _top;
    private float _columnX;
    private float _scale = 1;
    private string _message = "";
    private readonly StyleBoxFlat _sheet = new()
    {
        BgColor = PaperStyle.Paper.Lightened(.05f),
        BorderColor = PaperStyle.Surface,
        BorderWidthTop = 1,
    };

    public bool IsOpen { get; private set; }

    public override void _Ready()
    {
        // [Tool] only so the game scene's own [Tool] script can find it; nothing to show in the editor.
        if (Engine.IsEditorHint()) return;
        SetAnchorsPreset(LayoutPreset.TopLeft);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _content = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(ColumnWidth, ContentHeight) };
        _content.Draw += DrawContent;
        AddChild(_content);

        var scene = GD.Load<PackedScene>("res://UI/Paper/PaperButton.tscn");
        _menu = scene.Instantiate<PaperButton>();
        _menu.Caption = "Menu";
        _menu.Secondary = true;
        _newPuzzle = scene.Instantiate<PaperButton>();
        _newPuzzle.Caption = "New puzzle";
        _newPuzzle.Secondary = true;
        _newPuzzle.Accent = true;
        float buttonWidth = (ColumnWidth - 12) / 2;
        foreach (var (button, x) in new[] { (_menu, 0f), (_newPuzzle, buttonWidth + 12) })
        {
            _content.AddChild(button);
            button.Position = new Vector2(x, ButtonTop);
            button.Size = new Vector2(buttonWidth, ButtonHeight);
        }
        _menu.Pressed += () => EmitSignal(SignalName.MenuRequested);
        _newPuzzle.Pressed += () => EmitSignal(SignalName.NewPuzzleRequested);
    }

    /// <summary>
    /// Where the sheet may reach: its top edge (just below the board) and the game column, in this
    /// control's coordinates. The owner calls this from its layout pass.
    /// </summary>
    public void Place(float top, float columnX, float scale)
    {
        _top = top;
        _columnX = columnX;
        _scale = scale;
        Apply();
    }

    public void Open(SudokuGenerator.Difficulty difficulty)
    {
        if (IsOpen) return;
        IsOpen = true;
        _message = $"You finished this {difficulty.ToString().ToLowerInvariant()} puzzle.";
        Visible = true;
        _tween?.Kill();
        if (!UiAnimationSettings.Default.Enabled)
        {
            SetProgress(1);
            return;
        }
        SetProgress(0);
        _tween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _tween.TweenInterval(OpenDelay);
        _tween.TweenMethod(Callable.From<float>(SetProgress), 0f, 1f, OpenDuration);
    }

    private void SetProgress(float progress)
    {
        _progress = progress;
        Apply();
    }

    /// <summary>How far the sheet still has to travel, in pixels.</summary>
    private float Offset => (1 - _progress) * (Size.Y - _top);

    private void Apply()
    {
        if (_content == null) return;
        float available = (Size.Y - _top) / _scale;
        float contentTop = Mathf.Max(24, (available - ContentHeight) / 2);
        _content.Scale = Vector2.One * _scale;
        _content.Position = new Vector2(_columnX, _top + Offset + contentTop * _scale);
        _content.QueueRedraw();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!IsOpen) return;
        float radius = CornerRadius * _scale;
        _sheet.CornerRadiusTopLeft = _sheet.CornerRadiusTopRight = Mathf.RoundToInt(radius);
        // Runs past the bottom edge so the rounded corners never show there.
        DrawStyleBox(_sheet, new Rect2(0, _top + Offset, Size.X, Size.Y - _top + radius));
    }

    private void DrawContent()
    {
        _content.DrawString(PaperStyle.Body, new Vector2(0, 14), "PUZZLE SOLVED", fontSize: 14, modulate: PaperStyle.Burgundy);
        _content.DrawString(PaperStyle.Display, new Vector2(-2, 70), "Congratulations!", fontSize: 52, modulate: PaperStyle.Ink);
        _content.DrawString(PaperStyle.Body, new Vector2(0, 104), _message, fontSize: 18, modulate: PaperStyle.Muted);
    }

    public override void _ExitTree() => _tween?.Kill();
}
