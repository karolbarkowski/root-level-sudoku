using Godot;

namespace SudokuEndless;

/// <summary>Persistent soundtrack preference shared by startup and Settings.</summary>
public static class MusicSettings
{
    private const string Path = "user://settings.cfg";
    private static readonly ConfigFile Config = new();
    public static bool Enabled { get; private set; } = true;

    public static void Load()
    {
        var error = Config.Load(Path);
        if (error != Error.Ok && error != Error.FileNotFound)
            GD.PushWarning($"Cannot load settings: {error}");
        Enabled = Config.GetValue("audio", "music_enabled", true).AsBool();
        Apply();
    }

    public static void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        Apply();
        Config.SetValue("audio", "music_enabled", enabled);
        var error = Config.Save(Path);
        if (error != Error.Ok) GD.PushWarning($"Cannot save settings: {error}");
    }

    private static void Apply()
    {
        int bus = AudioServer.GetBusIndex("Music");
        if (bus >= 0) AudioServer.SetBusMute(bus, !Enabled);
        else GD.PushWarning("Music bus is missing; cannot apply music preference.");
    }
}
