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

	/// <summary>
	/// Installs a freshly generated board; its non-empty cells become the clues. Call on the Godot
	/// thread — <see cref="CellData"/> is a Resource.
	/// </summary>
	public static void Start(Board board, SudokuGenerator.Difficulty difficulty)
	{
		var cells = new CellData[BoardGeometry.CellCount];
		for (int i = 0; i < cells.Length; i++)
		{
			int value = board[BoardGeometry.RowOf(i), BoardGeometry.ColOf(i)];
			cells[i] = new CellData { Value = value, IsGiven = value != 0 };
		}
		Clear();
		Difficulty = difficulty;
		ActiveBoard = board;
		Cells = cells;
	}
}
