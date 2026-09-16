using Godot;

namespace SudokuEndless;

/// <summary>Audio preferences applied immediately and retained between launches.</summary>
public partial class Settings : Control
{
	public override void _Ready()
	{
		Bind("MusicToggle", "Music", MusicSettings.Enabled, MusicSettings.SetEnabled);
		Bind("SoundToggle", "Sound", MusicSettings.SoundEnabled, MusicSettings.SetSoundEnabled);
		Bind("HighlightToggle", "Highlight", MusicSettings.HighlightEnabled, MusicSettings.SetHighlightEnabled);
		Bind("RemainingToggle", "Show remaining numbers", MusicSettings.ShowRemaining, MusicSettings.SetShowRemaining);
		Button back = GetNodeOrNull<Button>("%BackButton");
		if (back != null)
		{
			back.Pressed += OnBack;
		}
	}

	private void Bind(string name, string caption, bool initial, System.Action<bool> apply)
	{
		var button = GetNode<PaperButton>("%" + name);
		void Refresh(bool enabled)
		{
			button.Caption = $"{caption}: {(enabled ? "ON" : "OFF")}";
			button.Accent = enabled;
			button.AccessibilityName = button.Caption;
			button.QueueRedraw();
		}
		button.SetPressedNoSignal(initial);
		Refresh(initial);
		button.Toggled += enabled => { apply(enabled); Refresh(enabled); };
	}

	private void OnBack() => SceneTransition.GoTo(SceneTransition.StartScreenPath);
}
