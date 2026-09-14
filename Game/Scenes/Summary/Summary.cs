using Godot;
using Sudoku;

namespace SudokuEndless;

/// <summary>
/// Shown after a puzzle is solved. Displays a congratulations message and offers a fresh game at the
/// same difficulty.
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

	private void OnPlayAgain() => SceneTransition.StartNewGame(GameSession.Difficulty);
}
