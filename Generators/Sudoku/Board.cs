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

    public bool CanUndo => _undo is { Count: > 0 };
    public bool CanRedo => _redo is { Count: > 0 };

    public void PlaceMove(Move move)
    {
        int index = Index(move.Row, move.Col);
        byte previous = state[index];
        state[index] = checked((byte)move.Value);
        (_undo ??= new()).Push(new MoveRecord(move.Row, move.Col, previous, move.Value, move.Technique));
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
