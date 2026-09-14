using Godot;
using Sudoku;
namespace SudokuEndless;

public enum InputMode { Value, Hints }

/// <summary>Coordinates the reusable board, paper actions and digit keys on a portrait sheet.</summary>
[Tool]
public partial class Main : Control
{
    [Export] public PackedScene NumberButtonScene { get; set; }
    [Export] public ButtonGroup NumberSelection { get; set; }
    private BoardView _board;
    private Control _sheet;
    private PaperIconButton _notes;
    private PaperIconButton _undo;
    private PaperIconButton _redo;
    private PaperIconButton _erase;
    private bool _paused;
    private int _activeDigit;
    private PaperIconButton _pause;
    private Tween _entry;
    private bool _leaving;
    private bool _previousBack;
    private readonly NumberButton[] _keys = new NumberButton[9];

    public override void _Ready()
    {
        _sheet = GetNode<Control>("%Sheet");
        _board = GetNode<BoardView>("%Board");
        _notes = GetNode<PaperIconButton>("%ModeToggle");
        _undo = GetNode<PaperIconButton>("%UndoButton");
        _redo = GetNode<PaperIconButton>("%RedoButton");
        _erase = GetNode<PaperIconButton>("%EraseButton");
        _pause = GetNode<PaperIconButton>("%PauseButton");
        _pause.Pressed += () => SetPaused(!_paused);
        GetNode<Control>("%PausePanel").GetNode<PaperButton>("Resume").Pressed += () => SetPaused(false);
        GetNode<Label>("%Difficulty").Text = GameSession.Difficulty.ToString().ToUpperInvariant();
        GetNode<PaperIconButton>("%BackButton").Pressed += BackToMenu;
        _undo.Pressed += () => _board.Undo();
        _redo.Pressed += () => _board.Redo();
        _erase.Pressed += () => _board.EraseSelected();
        _notes.SetPressedNoSignal(GameSession.NotesMode);
        _notes.Toggled += SetNotes;
        var grid = GetNode<GridContainer>("%NumberBar");
        for (int n = 1; n <= 9; n++)
        {
            var key = NumberButtonScene.Instantiate<NumberButton>();
            key.Number = n;
            key.Name = "Digit" + n;
            key.NumberPressed += EnterNumber;
            grid.AddChild(key);
            _keys[n - 1] = key;
        }
        _board.BoardChanged += Refresh;
        _board.Solved += OnSolved;
        Resized += Layout;
        Layout();
        SetNotes(GameSession.NotesMode);
        if (!Engine.IsEditorHint())
        {
            _previousBack = GetTree().QuitOnGoBack;
            GetTree().QuitOnGoBack = false;
            PlayEntry();
        }
    }

    private void Layout()
    {
        var background = GetNode<Control>("PaperBackground");
        background.SetAnchorsPreset(LayoutPreset.TopLeft);
        background.Position = Vector2.Zero;
        background.Size = Size;
        // Pin the header and controls to the edges; center the square in the space between.
        float scale = Mathf.Min(Size.X / 540, Size.Y / 980);
        float height = Size.Y / scale - 32;
        _sheet.SetAnchorsPreset(LayoutPreset.TopLeft);
        _sheet.Size = new Vector2(476, height);
        _sheet.Scale = Vector2.One * scale;
        _sheet.Position = new Vector2((Size.X - 476 * scale) / 2, 32 * scale);
        void Place(string path, float y, float h)
        {
            var control = _sheet.GetNode<Control>(path);
            control.Position = new Vector2(0, y);
            control.Size = new Vector2(476, h);
        }
        Place("Navigation", 0, 64);
        Place("BoardArea", (64 + height - 328 - 476) / 2, 476);
        Place("Actions", height - 328, 88);
        Place("NumberBar", height - 220, 220);
    }

    // Assembly reloads do not necessarily rerun _Ready or emit Resized in the 2D editor.
    // Reapply the layout there so the preview cannot retain the old bottom margin.
    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint() && IsNodeReady() && IsInstanceValid(_sheet) && Size.X > 0 && Size.Y > 0)
            Layout();
    }

    private void SetNotes(bool enabled)
    {
        if (!Engine.IsEditorHint()) GameSession.NotesMode = enabled;
        _notes.Caption = enabled ? "Notes ON" : "Notes OFF";
        _notes.Accent = enabled;
        _notes.QueueRedraw();
        Refresh();
    }

    private void EnterNumber(int number)
    {
        if (_paused || !_board.CanEdit) return;
        _activeDigit = number;
        if (_notes.ButtonPressed) _board.ToggleSelectedHint(number);
        else _board.SetSelectedValue(number);
        Refresh();
    }

    private void Refresh()
    {
        int[] counts = _board.GetValueCounts();
        _undo.Disabled = _paused || !_board.CanUndo;
        _redo.Disabled = _paused || !_board.CanRedo;
        _erase.Disabled = _paused || !_board.CanEdit;
        _notes.Disabled = _paused;
        _undo.RefreshFeedback();
        _redo.RefreshFeedback();
        _erase.RefreshFeedback();
        // Keep keys available: nine occurrences do not guarantee nine correct placements.
        foreach (var key in _keys)
        {
            if (key == null) continue;
            key.Remaining = Mathf.Max(0, 9 - counts[key.Number]);
            key.Selected = key.Number == _activeDigit;
            key.Disabled = _paused || !_board.CanEdit;
            key.RefreshAvailability();
        }
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        _board.Visible = !paused;
        GetNode<Control>("%PausePanel").Visible = paused;
        _pause.Symbol = paused ? PaperIconButton.Glyph.Play : PaperIconButton.Glyph.Pause;
        _pause.AccessibilityName = paused ? "Resume puzzle" : "Pause puzzle";
        _pause.TooltipText = _pause.AccessibilityName;
        _pause.QueueRedraw();
        Refresh();
        if (paused) GetNode<Control>("%PausePanel").GetNode<PaperButton>("Resume").GrabFocus();
        else _pause.GrabFocus();
    }

    public void BackToMenu()
    {
        if (_leaving || Engine.IsEditorHint()) return;
        _leaving = true;
        var error = GetTree().ChangeSceneToFile("res://Scenes/StartScreen/StartScreen.tscn");
        if (error != Error.Ok) { _leaving = false; GD.PushError($"Cannot open menu: {error}"); }
    }

    private void OnSolved()
    {
        GameSession.Clear();
        GetTree().ChangeSceneToFile("res://Scenes/Summary/Summary.tscn");
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMGoBackRequest && IsNodeReady()) BackToMenu();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (_paused) { if (key.Keycode == Key.Escape) SetPaused(false); return; }
        if (key.Keycode == Key.Escape) BackToMenu();
        else if (key.Keycode >= Key.Key1 && key.Keycode <= Key.Key9) EnterNumber((int)key.Keycode - (int)Key.Key0);
        else if (key.Keycode == Key.Backspace || key.Keycode == Key.Delete) _board.EraseSelected();
        else return;
        GetViewport().SetInputAsHandled();
    }

    private async void PlayEntry()
    {
        if (!UiAnimationSettings.Default.Enabled) return;
        var column = _sheet;
        foreach (Control child in column.GetChildren()) child.Modulate = new Color(1,1,1,.001f);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        if (!IsInsideTree()) return;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInsideTree() || _leaving) return;
        _entry = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        int i = 0;
        foreach (Control child in column.GetChildren())
            _entry.TweenProperty(child, "modulate:a", 1f, .3).SetDelay(i++ * .055);
    }

    public override void _ExitTree()
    {
        Resized -= Layout;
        _entry?.Kill();
        if (!Engine.IsEditorHint()) GetTree().QuitOnGoBack = _previousBack;
    }
}
