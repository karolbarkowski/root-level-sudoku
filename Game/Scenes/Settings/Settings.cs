using Godot;

namespace SudokuEndless;

/// <summary>Audio preferences applied immediately and retained between launches.</summary>
public partial class Settings : Control
{
	public override void _Ready()
	{
		var music = GetNode<PaperButton>("%MusicToggle");
		music.SetPressedNoSignal(MusicSettings.Enabled);
		void RefreshMusic()
		{
			music.Caption = MusicSettings.Enabled ? "Music: ON" : "Music: OFF";
			music.Accent = MusicSettings.Enabled;
			music.AccessibilityName = $"Background music, {(MusicSettings.Enabled ? "on" : "off")}";
			music.QueueRedraw();
		}
		RefreshMusic();
		music.Toggled += enabled => { MusicSettings.SetEnabled(enabled); RefreshMusic(); };
		Button back = GetNodeOrNull<Button>("%BackButton");
		if (back != null)
		{
			back.Pressed += OnBack;
		}
	}

	private void OnBack() => SceneTransition.GoTo(SceneTransition.StartScreenPath);
}
