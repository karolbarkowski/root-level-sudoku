using Godot;

namespace SudokuEndless;

public partial class Settings : Control
{
    public override void _Ready()
    {
        var content = GetNode<VBoxContainer>("%Content");
        void Heading(string text)
        {
            var label = new Label { Text = text };
            label.AddThemeFontSizeOverride("font_size", 16);
            label.AddThemeColorOverride("font_color", PaperStyle.Muted);
            content.AddChild(label);
        }
        void Row(string title, string description, bool enabled, System.Action<bool> toggle,
            int volume = 0, System.Action<int> changeVolume = null)
        {
            var row = new SettingRow();
            content.AddChild(row);
            row.Configure(title, description, enabled, toggle, volume, changeVolume);
        }
        Heading("AUDIO");
        Row("Music", "Background soundtrack.", MusicSettings.Enabled, MusicSettings.SetEnabled,
            MusicSettings.MusicVolume, MusicSettings.SetMusicVolume);
        Row("Sound", "Buttons and game events.", MusicSettings.SoundEnabled, MusicSettings.SetSoundEnabled,
            MusicSettings.SoundVolume, MusicSettings.SetSoundVolume);
        content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        Heading("BOARD");
        Row("Highlight", "Shade the selected row, column and 3 × 3 box.", MusicSettings.HighlightEnabled, MusicSettings.SetHighlightEnabled);
        Row("Show remaining numbers", "Show boxes for each digit still to place.", MusicSettings.ShowRemaining, MusicSettings.SetShowRemaining);
        var back = GD.Load<PackedScene>("res://UI/Paper/PaperButton.tscn").Instantiate<PaperButton>();
        back.Caption = "Back";
        back.Secondary = true;
        back.LeadingIcon = GD.Load<Texture2D>("res://Resources/icons/arrow-left.svg");
        back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        back.Pressed += () => SceneTransition.GoTo(SceneTransition.StartScreenPath);
        content.AddChild(back);
        // After AddChild: PaperButton._Ready resets the minimum size. Caption plus icon plus side padding.
        back.CustomMinimumSize = new Vector2(PaperStyle.Body.GetStringSize("Back", fontSize: back.CaptionSize).X + 140, 56);
    }
}
