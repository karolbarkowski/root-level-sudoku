using Generators.Sudoku;
using SudokuEndless;
namespace Sudoku;

/// <summary>The live puzzle survives scene changes, including its undo history.</summary>
public static class GameSession
{
	public static SudokuGenerator.Difficulty Difficulty { get; set; } = SudokuGenerator.Difficulty.Easy;
	public static Board ActiveBoard { get; set; }
	public static CellData[] Cells { get; set; }
	public static int SelectedIndex { get; set; } = -1;
	public static bool NotesMode { get; set; }
	public static bool HasPuzzle => ActiveBoard != null;
	public static void Clear()
	{
		ActiveBoard = null;
		Cells = null;
		SelectedIndex = -1;
		NotesMode = false;
	}
}
