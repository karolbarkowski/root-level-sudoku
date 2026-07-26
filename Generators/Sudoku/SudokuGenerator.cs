using Generators.Sudoku.Graders;

namespace Generators.Sudoku;

public partial class SudokuGenerator
{
    private const double T0 = 0.5;
    private const double Tmin = 1e-4;
    private const double Alpha = 0.99;
    private const int StepsPerTemperature = 1000;
    private const int MaxCostDelta = 32;

    // Precomputed board index of every (block, cell-within-block) pair. The annealing inner loop
    // resolves swap targets ~1.7M times per pass, so this replaces per-step div/mod with a lookup.
    private static readonly byte[] BlockCell = BuildBlockCellTable();

    private static byte[] BuildBlockCellTable()
    {
        byte[] table = new byte[81];
        for (int block = 0; block < 9; block++)
            for (int cell = 0; cell < 9; cell++)
                table[block * 9 + cell] = (byte)((block / 3 * 3 + cell / 3) * Board.Size + block % 3 * 3 + cell % 3);
        return table;
    }

    public static Board Generate(Difficulty difficulty, Board? board = null)
    {
        board ??= new Board();
        FastRandom random = FastRandom.CreateSeeded();
        Anneal(board, ref random);
        RemoveCues(board, difficulty, ref random);
        return board;
    }

    private static void Anneal(Board state, ref FastRandom random)
    {
        Span<byte> rowCounts = stackalloc byte[Board.Size * 10];
        Span<byte> columnCounts = stackalloc byte[Board.Size * 10];
        Span<double> acceptance = stackalloc double[MaxCostDelta + 1];

        while (true)
        {
            state.Randomize(ref random);
            int cost = InitializeCounts(state.state, rowCounts, columnCounts);
            double temperature = T0;

            while (temperature > Tmin && cost > 0)
            {
                // acceptance[delta] = exp(-delta/T) = exp(-1/T)^delta, so one exp plus a running
                // product fills the table instead of MaxCostDelta separate (ARM-costly) exp calls.
                acceptance[0] = 1;
                double factor = Math.Exp(-1.0 / temperature);
                double running = 1.0;
                for (int delta = 1; delta <= MaxCostDelta; delta++)
                {
                    running *= factor;
                    acceptance[delta] = running;
                }

                for (int step = 0; step < StepsPerTemperature; step++)
                {
                    int block = random.Next(9);
                    int firstCell = random.Next(9);
                    int secondCell = random.Next(8);
                    if (secondCell >= firstCell) secondCell++;

                    int first = BlockCell[block * 9 + firstCell];
                    int second = BlockCell[block * 9 + secondCell];
                    int costDelta = ApplySwapToCounts(state.state, first, second, rowCounts, columnCounts, forward: true);

                    if (costDelta <= 0 || random.NextDouble() < acceptance[costDelta])
                    {
                        state.Swap(first, second);
                        cost += costDelta;
                        if (cost == 0) return;
                    }
                    else
                    {
                        ApplySwapToCounts(state.state, first, second, rowCounts, columnCounts, forward: false);
                    }
                }
                temperature *= Alpha;
            }
        }
    }

    private static int InitializeCounts(ReadOnlySpan<byte> state, Span<byte> rowCounts, Span<byte> columnCounts)
    {
        rowCounts.Clear();
        columnCounts.Clear();
        int cost = 0;
        for (int row = 0; row < Board.Size; row++)
        {
            for (int col = 0; col < Board.Size; col++)
            {
                int digit = state[row * Board.Size + col];
                cost += ChangeCount(rowCounts, row, digit, 1);
                cost += ChangeCount(columnCounts, col, digit, 1);
            }
        }
        return cost;
    }

    // Updates the count tables for a proposed swap, but deliberately does not mutate the board.
    private static int ApplySwapToCounts(ReadOnlySpan<byte> state, int first, int second, Span<byte> rowCounts, Span<byte> columnCounts, bool forward)
    {
        int firstRow = first / Board.Size, firstCol = first % Board.Size;
        int secondRow = second / Board.Size, secondCol = second % Board.Size;
        int firstDigit = state[first], secondDigit = state[second];
        int delta = 0;

        int firstChange = forward ? -1 : 1;
        int secondChange = -firstChange;
        if (firstRow != secondRow)
        {
            delta += ChangeCount(rowCounts, firstRow, firstDigit, firstChange);
            delta += ChangeCount(rowCounts, firstRow, secondDigit, secondChange);
            delta += ChangeCount(rowCounts, secondRow, secondDigit, firstChange);
            delta += ChangeCount(rowCounts, secondRow, firstDigit, secondChange);
        }
        if (firstCol != secondCol)
        {
            delta += ChangeCount(columnCounts, firstCol, firstDigit, firstChange);
            delta += ChangeCount(columnCounts, firstCol, secondDigit, secondChange);
            delta += ChangeCount(columnCounts, secondCol, secondDigit, firstChange);
            delta += ChangeCount(columnCounts, secondCol, firstDigit, secondChange);
        }
        return delta;
    }

    private static int ChangeCount(Span<byte> counts, int unit, int digit, int change)
    {
        int index = unit * 10 + digit;
        int before = counts[index];
        int after = before + change;
        counts[index] = (byte)after;
        return Math.Max(after - 1, 0) - Math.Max(before - 1, 0);
    }

    private static void RemoveCues(Board board, Difficulty target, ref FastRandom random)
    {
        Span<byte> order = stackalloc byte[81];
        for (int i = 0; i < order.Length; i++) order[i] = (byte)i;
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        foreach (int index in order)
        {
            byte saved = board.state[index];
            board.state[index] = 0;
            Difficulty grade = target == Difficulty.Beyond
                ? board.UniquenessGrader.Grade(board.state)
                : board.RuleGrader.Grade(board.state, target); // stop early once it's provably too hard
            if (grade > target) board.state[index] = saved;
        }
    }
}
