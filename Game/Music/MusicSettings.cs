using Godot;

namespace SudokuEndless;

/// <summary>Shared audio and board preferences. Retains the existing music settings API and file.</summary>
public static class MusicSettings
{
    private const string Path = "user://settings.cfg";
    private static readonly ConfigFile Config = new();
    public static bool Enabled { get; private set; }
    public static bool SoundEnabled { get; private set; } = true;
    public static bool HighlightEnabled { get; private set; } = true;
    public static bool ShowRemaining { get; private set; } = true;
    public static int MusicVolume { get; private set; } = 10;
    public static int SoundVolume { get; private set; } = 10;

    public static void Load()
    {
        var error = Config.Load(Path);
        if (error != Error.Ok && error != Error.FileNotFound)
            GD.PushWarning($"Cannot load settings: {error}");
        Enabled = Config.GetValue("audio", "music_enabled", false).AsBool(); // off until the player turns it on
        SoundEnabled = Config.GetValue("audio", "sound_enabled", true).AsBool();
        HighlightEnabled = Config.GetValue("board", "highlight_enabled", true).AsBool();
        ShowRemaining = Config.GetValue("board", "show_remaining", true).AsBool();
        MusicVolume = Mathf.Clamp(Config.GetValue("audio", "music_volume", 10).AsInt32(), 1, 10);
        SoundVolume = Mathf.Clamp(Config.GetValue("audio", "sound_volume", 10).AsInt32(), 1, 10);
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

    public static void SetMusicVolume(int value)
    {
        MusicVolume = Mathf.Clamp(value, 1, 10);
        Config.SetValue("audio", "music_volume", MusicVolume);
        Apply();
        Save();
    }

    public static void SetSoundVolume(int value)
    {
        SoundVolume = Mathf.Clamp(value, 1, 10);
        Config.SetValue("audio", "sound_volume", SoundVolume);
        Apply();
        Save();
    }

    private static void Apply()
    {
        int bus = AudioServer.GetBusIndex("Music");
        if (bus >= 0) AudioServer.SetBusMute(bus, !Enabled);
        else GD.PushWarning("Music bus is missing; cannot apply music preference.");
        int sounds = AudioServer.GetBusIndex("GameSounds");
        if (sounds >= 0) AudioServer.SetBusMute(sounds, !SoundEnabled);
        if (bus >= 0) AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(MusicVolume / 10f));
        if (sounds >= 0) AudioServer.SetBusVolumeDb(sounds, Mathf.LinearToDb(SoundVolume / 10f));
    }
}
