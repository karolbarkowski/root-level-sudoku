using Godot;
using Sudoku;

namespace SudokuEndless;

/// <summary>
/// Tiny handoff scene. It gets a frame on screen before the heavier game scene is instantiated,
/// so scene construction itself cannot hide the loading feedback.
/// </summary>
public partial class Loading : Control
{
    private bool _leaving;
    private GenerationOverlay _overlay;
    private Main _game;
    private bool _handoff;

    public override async void _Ready()
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        if (!IsInsideTree()) return;
        await ToSignal(GetTree().CreateTimer(.08), SceneTreeTimer.SignalName.Timeout);
        if (!IsInsideTree() || _leaving) return;
        _overlay = GetNode<GenerationOverlay>("GenerationOverlay");
        if (GameSession.HasPuzzle)
            _overlay.Title = "LOADING BOARD";
        PackedScene packed = GD.Load<PackedScene>("res://Scenes/Game/Main.tscn");
        if (packed == null) return;
        _game = packed.Instantiate<Main>();
        GetTree().Root.AddChild(_game);
        GetTree().Root.MoveChild(this, GetTree().Root.GetChildCount() - 1);
        _game.TreeExiting += OnGameExiting;
        GetTree().CurrentScene = _game;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_handoff || _game == null || !_game.IsInsideTree()) return;
        BoardView board = _game.GetNodeOrNull<BoardView>("%Board");
        if (board == null || !board.Visible) return;
        _handoff = true;
        _overlay?.HideAnimated();
        RemoveLoaderAfterFade();
    }

    private async void RemoveLoaderAfterFade()
    {
        await ToSignal(GetTree().CreateTimer(.24), SceneTreeTimer.SignalName.Timeout);
        if (!IsInsideTree()) return;
        if (_game != null && IsInstanceValid(_game))
            _game.TreeExiting -= OnGameExiting;
        QueueFree();
    }

    private void OnGameExiting()
    {
        _leaving = true;
        if (IsInsideTree()) QueueFree();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMGoBackRequest)
        {
            _leaving = true;
            if (_game == null && IsInsideTree())
                GetTree().ChangeSceneToFile("res://Scenes/StartScreen/StartScreen.tscn");
        }
    }
}
