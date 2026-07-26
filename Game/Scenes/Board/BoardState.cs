using System;

namespace SudokuEndless;

/// <summary>
/// The authoritative game state: a 9x9 grid of <see cref="CellData"/>. Owned by the board,
/// which is the single source of truth. This is a plain C# class (no node in the tree) so the
/// game logic stays decoupled from the view and is straightforward to test.
///
/// All mutation goes through this type; tiles never write here. After a mutation the board
/// re-pushes the affected cells down to the tiles (see <c>Board.RenderAll</c>).
/// </summary>
public class BoardState
{
    public const int Size = 9;
    public const int BoxSize = 3;
    public const int CellCount = Size * Size;

    private readonly CellData[] _cells = new CellData[CellCount];

    public BoardState()
    {
        for (int i = 0; i < CellCount; i++)
        {
            _cells[i] = new CellData();
        }
    }

    public static int Index(int row, int col) => (row * Size) + col;
    public static int RowOf(int index) => index / Size;
    public static int ColOf(int index) => index % Size;

    // --- Cell geometry (used for peer highlighting now, and validation later) ---

    public static bool SameRow(int a, int b) => RowOf(a) == RowOf(b);
    public static bool SameColumn(int a, int b) => ColOf(a) == ColOf(b);

    public static bool SameBox(int a, int b) =>
        (RowOf(a) / BoxSize == RowOf(b) / BoxSize) &&
        (ColOf(a) / BoxSize == ColOf(b) / BoxSize);

    /// <summary>
    /// True when two cells share a row, column, or 3x3 box. A cell is trivially a peer of itself;
    /// callers that care about the distinction (e.g. selection vs. peer highlight) handle it first.
    /// </summary>
    public static bool ArePeers(int a, int b) => SameRow(a, b) || SameColumn(a, b) || SameBox(a, b);

    public CellData GetCell(int index) => _cells[index];
    public CellData GetCell(int row, int col) => _cells[Index(row, col)];

    /// <summary>
    /// Counts how many times each digit (1-9) currently appears on the board, givens included.
    /// The returned array is indexed by value (index 0 unused), so a digit is fully placed when
    /// <c>counts[value] == Size</c>.
    /// </summary>
    public int[] GetValueCounts()
    {
        var counts = new int[Size + 1];
        foreach (CellData cell in _cells)
        {
            int value = cell.Value;
            if (value >= 1 && value <= Size)
            {
                counts[value]++;
            }
        }

        return counts;
    }

    /// <summary>Sets a player value and clears the cell's hints. No-op on given (clue) cells.</summary>
    public void SetValue(int index, int value)
    {
        CellData cell = _cells[index];
        if (cell.IsGiven)
        {
            return;
        }

        cell.Value = value;
        cell.Hints = Array.Empty<int>();
    }

    /// <summary>Toggles a pencil mark. Only allowed on empty, non-given cells.</summary>
    public void ToggleHint(int index, int n)
    {
        CellData cell = _cells[index];
        if (cell.IsGiven || !cell.IsEmpty)
        {
            return;
        }

        cell.ToggleHint(n);
    }

    /// <summary>Clears a player cell back to empty. No-op on given (clue) cells.</summary>
    public void ClearCell(int index)
    {
        CellData cell = _cells[index];
        if (cell.IsGiven)
        {
            return;
        }

        cell.Clear();
    }
}
