using static Generators.Sudoku.SudokuGenerator;

namespace Generators.Sudoku.Graders;

internal sealed class UniquenessGrader
{
    private const int AllDigits = 0b11_1111_1110;
    private readonly byte[] _grid = new byte[81];
    private readonly ushort[] _rows = new ushort[9];
    private readonly ushort[] _columns = new ushort[9];
    private readonly ushort[] _boxes = new ushort[9];

    public Difficulty Grade(ReadOnlySpan<byte> puzzle)
    {
        puzzle.CopyTo(_grid);
        Array.Clear(_rows);
        Array.Clear(_columns);
        Array.Clear(_boxes);

        for (int index = 0; index < 81; index++)
        {
            int digit = _grid[index];
            if (digit == 0) continue;
            int row = index / 9, col = index % 9, box = row / 3 * 3 + col / 3;
            int bit = 1 << digit;
            if (((_rows[row] | _columns[col] | _boxes[box]) & bit) != 0)
                return Difficulty.Invalid;
            _rows[row] |= (ushort)bit;
            _columns[col] |= (ushort)bit;
            _boxes[box] |= (ushort)bit;
        }

        return CountSolutions(2) == 1 ? Difficulty.Beyond : Difficulty.Invalid;
    }

    private int CountSolutions(int cap)
    {
        int bestIndex = -1, bestMask = 0, bestCount = 10;
        for (int index = 0; index < 81; index++)
        {
            if (_grid[index] != 0) continue;
            int row = index / 9, col = index % 9;
            int mask = AllDigits & ~(_rows[row] | _columns[col] | _boxes[row / 3 * 3 + col / 3]);
            int count = System.Numerics.BitOperations.PopCount((uint)mask);
            if (count == 0) return 0;
            if (count < bestCount)
            {
                bestIndex = index;
                bestMask = mask;
                bestCount = count;
                if (count == 1) break;
            }
        }
        if (bestIndex < 0) return 1;

        int bestRow = bestIndex / 9, bestCol = bestIndex % 9, bestBox = bestRow / 3 * 3 + bestCol / 3;
        int solutions = 0;
        while (bestMask != 0)
        {
            int bit = bestMask & -bestMask;
            bestMask &= bestMask - 1;
            _grid[bestIndex] = (byte)System.Numerics.BitOperations.TrailingZeroCount((uint)bit);
            _rows[bestRow] |= (ushort)bit;
            _columns[bestCol] |= (ushort)bit;
            _boxes[bestBox] |= (ushort)bit;
            solutions += CountSolutions(cap - solutions);
            _grid[bestIndex] = 0;
            _rows[bestRow] &= (ushort)~bit;
            _columns[bestCol] &= (ushort)~bit;
            _boxes[bestBox] &= (ushort)~bit;
            if (solutions >= cap) return solutions;
        }
        return solutions;
    }
}
