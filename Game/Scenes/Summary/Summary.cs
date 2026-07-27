using Godot;

namespace SudokuEndless;

/// <summary>
/// Shown after a puzzle is solved. Displays a congratulations message and offers a fresh game by
/// reloading the main scene.
/// </summary>
public partial class Summary : Control
{
	public override void _Ready()
	{
		Button playAgain = GetNodeOrNull<Button>("%PlayAgainButton");
		if (playAgain != null)
		{
			playAgain.Pressed += OnPlayAgain;
		}
	}

	private void OnPlayAgain()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
	}
}
