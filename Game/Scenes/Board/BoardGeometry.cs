namespace SudokuEndless;

/// <summary>
/// Pure board geometry: dimensions and the row/column/box relationships the UI needs (peer
/// highlighting, index math). Cell values and move history now live in the domain board
/// (<c>Generators.Sudoku.Board</c>), so this holds only layout math — no state.
/// </summary>
public static class BoardGeometry
{
	public const int Size = 9;
	public const int BoxSize = 3;
	public const int CellCount = Size * Size;

	public static int Index(int row, int col) => (row * Size) + col;
	public static int RowOf(int index) => index / Size;
	public static int ColOf(int index) => index % Size;

	public static bool SameRow(int a, int b) => RowOf(a) == RowOf(b);
	public static bool SameColumn(int a, int b) => ColOf(a) == ColOf(b);

	public static bool SameBox(int a, int b) =>
		(RowOf(a) / BoxSize == RowOf(b) / BoxSize) &&
		(ColOf(a) / BoxSize == ColOf(b) / BoxSize);

	/// <summary>True when two cells share a row, column, or 3x3 box (a cell is its own peer).</summary>
	public static bool ArePeers(int a, int b) => SameRow(a, b) || SameColumn(a, b) || SameBox(a, b);
}
