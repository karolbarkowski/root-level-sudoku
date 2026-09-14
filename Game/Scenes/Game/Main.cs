using Godot;
using System;
using System.Threading.Tasks;
using Generators.Sudoku;
using Sudoku;
namespace SudokuEndless;

public enum InputMode { Value, Hints }

/// <summary>Coordinates the reusable board, paper actions and digit keys on a portrait sheet.</summary>
[Tool]
public partial class Main : Control, ITransitionScreen
{
    [Export] public PackedScene NumberButtonScene { get; set; }
    [Export] public ButtonGroup NumberSelection { get; set; }
    private BoardView _board;
    private Control _sheet;
    private PaperIconButton _notes;
    private PaperIconButton _undo;
    private PaperIconButton _redo;
    private int _activeDigit;
    private int _lastSelectedIndex = -1;
    private int _lastSelectedValue;
    private Tween _entry;
    private bool _leaving;
    private bool _previousBack;
    private readonly NumberButton[] _keys = new NumberButton[9];

    // The sheet's sections, top to bottom, and where each one enters from (design units).
    private static readonly string[] SectionNames = { "Navigation", "BoardArea", "Actions", "NumberBar" };
    private static readonly float[] EntryShift = { -10, 14, 20, 20 };
    private const int BoardSection = 1;
    private const float EntryDuration = .35f;
    private const float EntryStagger = .06f;
    private const float ExitDuration = .2f;
    private const float ExitStagger = .03f;
    private const float ExitSink = 10;
    private readonly Control[] _sections = new Control[SectionNames.Length];
    private readonly float[] _sectionY = new float[SectionNames.Length];
    private readonly float[] _sectionShift = new float[SectionNames.Length];

    public override void _EnterTree()
    {
        // SceneTransition generates the puzzle before this scene exists. Launching the scene on its
        // own (F6) has no transition, so generate here, before the board's _Ready reads the session.
        if (!Engine.IsEditorHint() && !GameSession.HasPuzzle)
            GameSession.Start(SudokuGenerator.Generate(GameSession.Difficulty), GameSession.Difficulty);
    }

    public override void _Ready()
    {
        _sheet = GetNode<Control>("%Sheet");
        for (int i = 0; i < SectionNames.Length; i++)
            _sections[i] = _sheet.GetNode<Control>(SectionNames[i]);
        _board = GetNode<BoardView>("%Board");
        _notes = GetNode<PaperIconButton>("%ModeToggle");
        _undo = GetNode<PaperIconButton>("%UndoButton");
        _redo = GetNode<PaperIconButton>("%RedoButton");
        GetNode<Label>("%Difficulty").Text = GameSession.Difficulty.ToString().ToUpperInvariant();
        GetNode<PaperIconButton>("%BackButton").Pressed += BackToMenu;
        _undo.Pressed += () => _board.Undo();
        _redo.Pressed += () => _board.Redo();
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
        _board.SelectionChanged += OnSelectionChanged;
        _board.Solved += OnSolved;
        Resized += Layout;
        Layout();
        SetNotes(GameSession.NotesMode);
        if (Engine.IsEditorHint()) return;
        _previousBack = GetTree().QuitOnGoBack;
        GetTree().QuitOnGoBack = false;
        PrepareEntry();
        if (!SceneTransition.IsTransitioning) RevealWithoutTransition();
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
        void Place(int section, float y, float h)
        {
            var control = _sections[section];
            _sectionY[section] = y;
            control.Position = new Vector2(0, y + _sectionShift[section]);
            control.Size = new Vector2(476, h);
        }
        Place(0, 0, 64);
        Place(1, (64 + height - 328 - 476) / 2, 476);
        Place(2, height - 328, 88);
        Place(3, height - 220, 220);
    }

    /// <summary>Animated offset on top of the laid-out position, so a resize mid-animation keeps both.</summary>
    private void SetSectionShift(int section, float shift)
    {
        _sectionShift[section] = shift;
        _sections[section].Position = new Vector2(0, _sectionY[section] + shift);
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
        _notes.AccessibilityName = _notes.Caption;
        _notes.TooltipText = _notes.Caption;
        _notes.Accent = enabled;
        _notes.QueueRedraw();
        Refresh();
    }

    private void EnterNumber(int number)
    {
        if (!_board.CanEdit) return;
        _activeDigit = number;
        if (_notes.ButtonPressed)
            _board.ToggleSelectedHint(number);
        else if (_board.SelectedUserValue == number)
            _board.EraseSelected();
        else
            _board.SetSelectedValue(number);
        Refresh();
    }

    private void OnSelectionChanged(int value)
    {
        _activeDigit = value;
        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        Vector2 point;
        bool pressed;
        if (@event is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Left)
        {
            point = mouse.Position;
            pressed = mouse.Pressed;
        }
        else if (@event is InputEventScreenTouch touch)
        {
            point = touch.Position;
            pressed = touch.Pressed;
        }
        else return;

        if (pressed && !_board.GetGlobalRect().HasPoint(point))
            _board.ClearSelection();
    }

    private void Refresh()
    {
        // A cell tap changes the selected index/value; a number tap keeps its active key while
        // editing an empty cell.
        int selectedValue = _board.SelectedUserValue;
        if (_board.SelectedIndex != _lastSelectedIndex || selectedValue != _lastSelectedValue)
            _activeDigit = selectedValue;
        _lastSelectedIndex = _board.SelectedIndex;
        _lastSelectedValue = selectedValue;
        int[] counts = _board.GetValueCounts();
        _undo.Disabled = !_board.CanUndo;
        _redo.Disabled = !_board.CanRedo;
        _undo.RefreshFeedback();
        _redo.RefreshFeedback();
        // Keep keys available: nine occurrences do not guarantee nine correct placements.
        foreach (var key in _keys)
        {
            if (key == null) continue;
            key.Remaining = Mathf.Max(0, 9 - counts[key.Number]);
            key.Selected = _notes.ButtonPressed
                ? _board.SelectedHasHint(key.Number)
                : _board.SelectedUserValue == key.Number;
            key.Disabled = !_board.CanEdit;
            key.RefreshAvailability();
        }
    }

    public void BackToMenu()
    {
        if (_leaving || Engine.IsEditorHint()) return;
        _leaving = SceneTransition.GoTo(SceneTransition.StartScreenPath);
    }

    private void OnSolved()
    {
        if (_leaving) return;
        GameSession.Clear();
        _leaving = SceneTransition.GoTo(SceneTransition.SummaryPath);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMGoBackRequest && IsNodeReady()) BackToMenu();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (key.Keycode == Key.Escape) BackToMenu();
        else if (key.Keycode >= Key.Key1 && key.Keycode <= Key.Key9) EnterNumber((int)key.Keycode - (int)Key.Key0);
        else if (key.Keycode == Key.Backspace || key.Keycode == Key.Delete) _board.EraseSelected();
        else return;
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Parks every section just off its resting place, drawn but invisible.</summary>
    private void PrepareEntry()
    {
        if (!UiAnimationSettings.Default.Enabled) return;
        _entry?.Kill();
        for (int i = 0; i < _sections.Length; i++)
        {
            // Near-zero alpha keeps drawing active to warm fonts/textures without a visible flash.
            _sections[i].Modulate = new Color(1, 1, 1, .001f);
            SetSectionShift(i, EntryShift[i]);
        }
        // Fading the board fades its overlapping layers (grid colour, boxes, tiles) one by one, so they
        // bleed through each other and the board looks muddy. Behind a transition it stays opaque and
        // the lifting cover provides the fade; it only moves. (It does not scale either: mid-scale, the
        // one-pixel cell rules fall between pixels and flicker.)
        if (SceneTransition.IsTransitioning) _sections[BoardSection].Modulate = Colors.White;
    }

    public async Task PrepareRevealAsync()
    {
        await _board.WhenBuilt;
        if (IsInstanceValid(this) && IsInsideTree()) Refresh();
    }

    public void PlayEntry()
    {
        if (!UiAnimationSettings.Default.Enabled || _leaving) return;
        _entry?.Kill();
        _entry = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        for (int i = 0; i < _sections.Length; i++)
        {
            int section = i;
            double delay = i * EntryStagger;
            _entry.TweenProperty(_sections[i], "modulate:a", 1f, EntryDuration).SetDelay(delay);
            _entry.TweenMethod(Callable.From<float>(v => SetSectionShift(section, v)), _sectionShift[i], 0f, EntryDuration).SetDelay(delay);
        }
    }

    public async Task PlayExitAsync()
    {
        if (!UiAnimationSettings.Default.Enabled) return;
        _entry?.Kill();
        _entry = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        for (int i = 0; i < _sections.Length; i++)
        {
            int section = i;
            double delay = i * ExitStagger;
            // The board stays opaque (see PrepareEntry); the cover fades in over it.
            if (i != BoardSection) _entry.TweenProperty(_sections[i], "modulate:a", 0f, ExitDuration).SetDelay(delay);
            _entry.TweenMethod(Callable.From<float>(v => SetSectionShift(section, v)), _sectionShift[i], ExitSink, ExitDuration).SetDelay(delay);
        }
        await ToSignal(GetTree().CreateTimer(ExitDuration + (_sections.Length - 1) * ExitStagger), SceneTreeTimer.SignalName.Timeout);
    }

    /// <summary>Launched directly (e.g. F6), there is no cover to wait behind.</summary>
    private async void RevealWithoutTransition()
    {
        await PrepareRevealAsync();
        if (!IsInstanceValid(this) || !IsInsideTree()) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        if (IsInstanceValid(this) && IsInsideTree()) PlayEntry();
    }

    public override void _ExitTree()
    {
        Resized -= Layout;
        _entry?.Kill();
        if (!Engine.IsEditorHint()) GetTree().QuitOnGoBack = _previousBack;
    }
}
