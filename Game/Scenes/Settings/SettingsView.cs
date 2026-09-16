using Godot;

namespace SudokuEndless;

/// <summary>
/// The list of settings, without a title or a way out: the start screen shows it in place of its
/// menu, and the game shows it in a sheet. Every change is saved at once.
/// </summary>
public partial class SettingsView : VBoxContainer
{
    /// <summary>Emitted after any setting changes, so a screen can redraw what depends on it.</summary>
    [Signal] public delegate void ChangedEventHandler();

    /// <summary>Gap between rows. Set before the view enters the tree.</summary>
    public int Separation { get; set; } = 24;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", Separation);
        void Heading(string text)
        {
            var label = new Label { Text = text };
            label.AddThemeFontSizeOverride("font_size", 16);
            label.AddThemeColorOverride("font_color", PaperStyle.Muted);
            AddChild(label);
        }
        void Row(string title, string description, bool enabled, System.Action<bool> toggle,
            int volume = 0, System.Action<int> changeVolume = null)
        {
            var row = new SettingRow();
            AddChild(row);
            row.Configure(title, description, enabled, value => { toggle(value); EmitSignal(SignalName.Changed); },
                volume, changeVolume == null ? null : value => { changeVolume(value); EmitSignal(SignalName.Changed); });
        }
        Heading("AUDIO");
        Row("Music", "Background soundtrack.", MusicSettings.Enabled, MusicSettings.SetEnabled,
            MusicSettings.MusicVolume, MusicSettings.SetMusicVolume);
        Row("Sound", "Buttons and game events.", MusicSettings.SoundEnabled, MusicSettings.SetSoundEnabled,
            MusicSettings.SoundVolume, MusicSettings.SetSoundVolume);
        AddChild(new Control { CustomMinimumSize = new Vector2(0, 12), MouseFilter = MouseFilterEnum.Ignore });
        Heading("BOARD");
        Row("Highlight", "Shade the selected row, column and 3 × 3 box.", MusicSettings.HighlightEnabled, MusicSettings.SetHighlightEnabled);
        Row("Show remaining numbers", "Show boxes for each digit still to place.", MusicSettings.ShowRemaining, MusicSettings.SetShowRemaining);
    }
}
