using Godot;

namespace SudokuEndless;

/// <summary>Settings view. Empty for now apart from a Back button returning to the start screen.</summary>
public partial class Settings : Control
{
	public override void _Ready()
	{
		Button back = GetNodeOrNull<Button>("%BackButton");
		if (back != null)
		{
			back.Pressed += OnBack;
		}
	}

	private void OnBack() => SceneTransition.GoTo(SceneTransition.StartScreenPath);
}
