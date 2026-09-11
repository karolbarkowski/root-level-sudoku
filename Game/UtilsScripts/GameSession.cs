using Generators.Sudoku;

namespace Sudoku;

/// <summary>
/// Tiny cross-scene game state. Static so it survives a scene change (start screen → game) without
/// an autoload; the start screen writes the chosen difficulty here and the board reads it when it
/// generates a new puzzle.
/// </summary>
public static class GameSession
{
	public static SudokuGenerator.Difficulty Difficulty { get; set; } = SudokuGenerator.Difficulty.Easy;
}
