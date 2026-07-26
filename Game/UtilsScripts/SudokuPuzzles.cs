namespace SudokuEndless;

/// <summary>
/// Hardcoded puzzle data and a parser. This is the only data source for the current slice;
/// later this is replaced by a generator / persistence layer.
/// </summary>
public static class SudokuPuzzles
{
    /// <summary>
    /// An 81-character puzzle laid out row by row. '0' or '.' means an empty cell; '1'-'9' are
    /// givens (clues). This is the well-known "easy" seed puzzle.
    /// </summary>
    public const string Default =
        "53..7...." +
        "6..195..." +
        ".98....6." +
        "8...6...3" +
        "4..8.3..1" +
        "7...2...6" +
        ".6....28." +
        "...419..5" +
        "....8..79";

    public static BoardState CreateHardcoded() => Parse(Default);

    /// <summary>Parses an 81-char puzzle string into a <see cref="BoardState"/>, flagging givens.</summary>
    public static BoardState Parse(string puzzle)
    {
        var state = new BoardState();
        int count = puzzle.Length < BoardState.CellCount ? puzzle.Length : BoardState.CellCount;

        for (int i = 0; i < count; i++)
        {
            char c = puzzle[i];
            int value = (c >= '1' && c <= '9') ? c - '0' : 0;

            CellData cell = state.GetCell(i);
            cell.Value = value;
            cell.IsGiven = value != 0;
        }

        return state;
    }
}
