using Godot;

namespace SudokuEndless;

/// <summary>Shared audio and board preferences. Retains the existing music settings API and file.</summary>
public static class MusicSettings
{
    private const string Path = "user://settings.cfg";
    private static readonly ConfigFile Config = new();
    public static bool Enabled { get; private set; } = true;
    public static bool SoundEnabled { get; private set; } = true;
    public static bool HighlightEnabled { get; private set; } = true;
    public static bool ShowRemaining { get; private set; } = true;

    public static void Load()
    {
        var error = Config.Load(Path);
        if (error != Error.Ok && error != Error.FileNotFound)
            GD.PushWarning($"Cannot load settings: {error}");
        Enabled = Config.GetValue("audio", "music_enabled", true).AsBool();
        SoundEnabled = Config.GetValue("audio", "sound_enabled", true).AsBool();
        HighlightEnabled = Config.GetValue("board", "highlight_enabled", true).AsBool();
        ShowRemaining = Config.GetValue("board", "show_remaining", true).AsBool();
        Apply();
    }

    public static void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        Apply();
        Config.SetValue("audio", "music_enabled", enabled);
        Save();
    }

    public static void SetSoundEnabled(bool enabled)
    {
        SoundEnabled = enabled;
        Apply();
        Config.SetValue("audio", "sound_enabled", enabled);
        Save();
    }

    public static void SetHighlightEnabled(bool enabled)
    {
        HighlightEnabled = enabled;
        Config.SetValue("board", "highlight_enabled", enabled);
        Save();
    }

    public static void SetShowRemaining(bool enabled)
    {
        ShowRemaining = enabled;
        Config.SetValue("board", "show_remaining", enabled);
        Save();
    }

    private static void Save()
    {
        var error = Config.Save(Path);
        if (error != Error.Ok) GD.PushWarning($"Cannot save settings: {error}");
    }

    private static void Apply()
    {
        int bus = AudioServer.GetBusIndex("Music");
        if (bus >= 0) AudioServer.SetBusMute(bus, !Enabled);
        else GD.PushWarning("Music bus is missing; cannot apply music preference.");
        int sounds = AudioServer.GetBusIndex("GameSounds");
        if (sounds >= 0) AudioServer.SetBusMute(sounds, !SoundEnabled);
    }
}
