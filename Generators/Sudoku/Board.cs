using Generators.Sudoku.Graders;
using System.Text;

namespace Generators.Sudoku;

public class Board
{
    internal const int Size = 9;
    internal readonly byte[] state;

    // Created on first use: a freshly generated level (the common on-device case) never touches
    // undo/redo, so it should not pay for two Stack allocations it will not use.
    private Stack<MoveRecord>? _undo;
    private Stack<MoveRecord>? _redo;
    private readonly RuleBasedGrader _hintGrader = new();
    private readonly UniquenessGrader _uniquenessGrader = new();

    public Board()
    {
        state = new byte[Size * Size];
        Randomize();
    }

    internal Board(int[,] initialState)
    {
        if (initialState.GetLength(0) != Size || initialState.GetLength(1) != Size)
            throw new ArgumentException("State must be a 9x9 matrix");

        state = new byte[Size * Size];
        for (int row = 0; row < Size; row++)
            for (int col = 0; col < Size; col++)
                state[Index(row, col)] = checked((byte)initialState[row, col]);
    }

    internal static int Index(int row, int col) => row * Size + col;

    public int this[int row, int col]
    {
        get => state[Index(row, col)];
        internal set => state[Index(row, col)] = checked((byte)value);
    }

    internal void Swap(int first, int second) => (state[second], state[first]) = (state[first], state[second]);
    internal void Swap(int row1, int col1, int row2, int col2) => Swap(Index(row1, col1), Index(row2, col2));

    public void Randomize()
    {
        FastRandom random = FastRandom.CreateSeeded();
        Randomize(ref random);
    }

    internal void Randomize(ref FastRandom random)
    {
        Span<byte> blockNumbers = stackalloc byte[Size];

        for (int block = 0; block < Size; block++)
        {
            Utils.FillShuffled1To9(blockNumbers, ref random);
            int startRow = block / 3 * 3;
            int startCol = block % 3 * 3;

            for (int cell = 0; cell < Size; cell++)
                state[Index(startRow + cell / 3, startCol + cell % 3)] = blockNumbers[cell];
        }
    }

    public Move? SuggestNextMove()
        => _hintGrader.TryFindNextMove(state, out int row, out int column, out int value, out Technique technique)
            ? new Move(row, column, value, technique)
            : null;

    internal RuleBasedGrader RuleGrader => _hintGrader;
    internal UniquenessGrader UniquenessGrader => _uniquenessGrader;

    /// <summary>The 81 cell values, row by row (0 = empty).</summary>
    public int[] GetCells()
    {
        int[] cells = new int[state.Length];
        for (int i = 0; i < cells.Length; i++) cells[i] = state[i];
        return cells;
    }

    /// <summary>Moves that <see cref="Undo"/> can take back, oldest first.</summary>
    public MoveRecord[] GetUndoHistory() => OldestFirst(_undo);

    /// <summary>Moves that <see cref="Redo"/> can re-apply, oldest first (the next redo is last).</summary>
    public MoveRecord[] GetRedoHistory() => OldestFirst(_redo);

    private static MoveRecord[] OldestFirst(Stack<MoveRecord>? stack)
    {
        if (stack is not { Count: > 0 }) return [];
        MoveRecord[] records = stack.ToArray(); // top of the stack first
        Array.Reverse(records);
        return records;
    }

    /// <summary>
    /// Rebuilds a board saved with <see cref="GetCells"/>, <see cref="GetUndoHistory"/> and
    /// <see cref="GetRedoHistory"/>. Throws <see cref="ArgumentException"/> when the data is not a
    /// valid board, so a damaged save can be rejected rather than loaded.
    /// </summary>
    public static Board Restore(ReadOnlySpan<int> cells, IEnumerable<MoveRecord> undoHistory, IEnumerable<MoveRecord> redoHistory)
    {
        if (cells.Length != Size * Size)
            throw new ArgumentException("A board has 81 cells", nameof(cells));

        var board = new Board(new int[Size, Size]);
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] is < 0 or > 9) throw new ArgumentException($"Cell {i} holds {cells[i]}", nameof(cells));
            board.state[i] = (byte)cells[i];
        }
        foreach (MoveRecord record in undoHistory) (board._undo ??= new()).Push(Validated(record));
        foreach (MoveRecord record in redoHistory) (board._redo ??= new()).Push(Validated(record));
        return board;
    }

    private static MoveRecord Validated(MoveRecord record) =>
        record is { Row: >= 0 and < Size, Col: >= 0 and < Size, PreviousValue: >= 0 and <= 9, NewValue: >= 0 and <= 9 }
            ? record
            : throw new ArgumentException($"Invalid history entry {record}");

    public bool CanUndo => _undo is { Count: > 0 };
    public bool CanRedo => _redo is { Count: > 0 };

    /// <summary>Writes <paramref name="value"/> (0 clears) at the cell and records it for undo.</summary>
    public void PlaceMove(int row, int col, int value)
    {
        int index = Index(row, col);
        byte previous = state[index];
        state[index] = checked((byte)value);
        (_undo ??= new()).Push(new MoveRecord(row, col, previous, value));
        _redo?.Clear();
    }

    public bool Undo()
    {
        if (!CanUndo) return false;
        MoveRecord rec = _undo!.Pop();
        state[Index(rec.Row, rec.Col)] = checked((byte)rec.PreviousValue);
        (_redo ??= new()).Push(rec);
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;
        MoveRecord rec = _redo!.Pop();
        state[Index(rec.Row, rec.Col)] = checked((byte)rec.NewValue);
        (_undo ??= new()).Push(rec);
        return true;
    }

    /// <summary>Clears every cell that is not a clue and forgets the undo/redo history.</summary>
    /// <param name="isGiven">Which cells are clues, indexed <c>row * 9 + col</c>.</param>
    public void Restart(ReadOnlySpan<bool> isGiven)
    {
        if (isGiven.Length != state.Length)
            throw new ArgumentException("Clue mask must cover all 81 cells", nameof(isGiven));

        for (int i = 0; i < state.Length; i++)
            if (!isGiven[i]) state[i] = 0;
        _undo?.Clear();
        _redo?.Clear();
    }

    /// <summary>
    /// True only when the board is a complete, valid solution: every cell filled and every row,
    /// column, and 3x3 box contains the digits 1-9 exactly once.
    /// </summary>
    public bool IsSolved()
    {
        // Bits 1..9 set; an empty cell (0) sets bit 0, so it can never reach this exactly.
        const int Complete = 0x3FE;

        for (int unit = 0; unit < Size; unit++)
        {
            int rowMask = 0, colMask = 0, boxMask = 0;
            int boxRow = unit / 3 * 3;
            int boxCol = unit % 3 * 3;

            for (int k = 0; k < Size; k++)
            {
                rowMask |= 1 << state[Index(unit, k)];
                colMask |= 1 << state[Index(k, unit)];
                boxMask |= 1 << state[Index(boxRow + k / 3, boxCol + k % 3)];
            }

            if (rowMask != Complete || colMask != Complete || boxMask != Complete)
                return false;
        }

        return true;
    }

    public int Cost()
    {
        int total = 0;
        for (int i = 0; i < Size; i++)
        {
            total += Utils.CountDuplicatesInRow(state, i);
            total += Utils.CountDuplicatesInColumn(state, i);
        }
        return total;
    }

    public override string ToString()
    {
        StringBuilder sb = new(200);
        for (int row = 0; row < Size; row++)
        {
            for (int col = 0; col < Size; col++)
            {
                int value = state[Index(row, col)];
                sb.Append(value == 0 ? '-' : (char)('0' + value));
                sb.Append(' ');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
