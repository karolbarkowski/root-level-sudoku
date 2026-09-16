using Godot;

namespace SudokuEndless;

/// <summary>Persistent sound effects, independent of the soundtrack's mute setting.</summary>
public partial class GameSounds : Node
{
    private static GameSounds _instance;
    private AudioStreamPlayer _click, _toggle, _start, _success;

    public override void _Ready()
    {
        _instance = this;
        ProcessMode = ProcessModeEnum.Always;
        _click = MakePlayer("button_plop");
        _toggle = MakePlayer("toggle");
        _start = MakePlayer("level_start");
        _success = MakePlayer("success");
        GetTree().NodeAdded += WireButton;
        WireExisting(GetTree().Root);
    }

    private AudioStreamPlayer MakePlayer(string name)
    {
        var player = new AudioStreamPlayer
        {
            Stream = GD.Load<AudioStream>($"res://Resources/Audio/GameSounds/{name}.mp3"),
            Bus = "GameSounds",
            MaxPolyphony = 4
        };
        AddChild(player);
        return player;
    }

    private void WireExisting(Node node)
    {
        WireButton(node);
        foreach (Node child in node.GetChildren()) WireExisting(child);
    }

    private void WireButton(Node node)
    {
        if (node is not BaseButton button) return;
        button.Pressed += () =>
        {
            // Gameplay input stays quiet; whole UI sections can opt out of click feedback.
            for (Node current = button; current != null; current = current.GetParent())
                if (current.IsInGroup("silent_button_sounds")) return;
            // Settings toggles get their own cue instead of two overlapping sounds.
            bool setting = false;
            for (Node parent = button.GetParent(); parent != null; parent = parent.GetParent())
                if (parent is SettingsView) { setting = true; break; }
            if (setting && button.ToggleMode) _toggle.Play();
            else _click.Play();
        };
    }

    public static void BoardShown() => _instance?._start.Play();
    public static void BoardCompleted() => _instance?._success.Play();

    public override void _ExitTree()
    {
        GetTree().NodeAdded -= WireButton;
        if (_instance == this) _instance = null;
    }
}
