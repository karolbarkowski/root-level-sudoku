using Generators.Sudoku;
using Shouldly;

namespace Generators.Tests.Unit.Sudoku;

public class BoardHintsTests
{
    // A complete, valid Sudoku solution used as ground truth for the tests below.
    private static readonly int[,] Solution =
    {
        { 5, 3, 4, 6, 7, 8, 9, 1, 2 },
        { 6, 7, 2, 1, 9, 5, 3, 4, 8 },
        { 1, 9, 8, 3, 4, 2, 5, 6, 7 },
        { 8, 5, 9, 7, 6, 1, 4, 2, 3 },
        { 4, 2, 6, 8, 5, 3, 7, 9, 1 },
        { 7, 1, 3, 9, 2, 4, 8, 5, 6 },
        { 9, 6, 1, 5, 3, 7, 2, 8, 4 },
        { 2, 8, 7, 4, 1, 9, 6, 3, 5 },
        { 3, 4, 5, 2, 8, 6, 1, 7, 9 }
    };

    [Fact]
    public void SuggestNextMove_Should_ReturnNull_WhenBoardIsComplete()
    {
        Board board = new((int[,])Solution.Clone());

        board.SuggestNextMove().HasValue.ShouldBe(false);
    }

    [Fact]
    public void SuggestNextMove_Should_FillTheOnlyEmptyCellWithItsSolutionValue()
    {
        int[,] state = (int[,])Solution.Clone();
        state[4, 4] = 0; // blank a single cell -> a naked single

        Board board = new(state);

        Move move = board.SuggestNextMove()!.Value;

        move.Row.ShouldBe(4);
        move.Col.ShouldBe(4);
        move.Value.ShouldBe(Solution[4, 4]);
        move.Technique.ShouldBe(Technique.NakedSingle); // the lone empty cell has one candidate
    }

    [Fact]
    public void SuggestNextMove_Should_OnlyEverSuggestSolutionConsistentMoves()
    {
        int[,] state = (int[,])Solution.Clone();

        // Blank a scattered set of cells; the remainder stays solvable by singles.
        (int, int)[] blanks =
        {
            (0, 0), (0, 4), (1, 2), (2, 7), (3, 3),
            (4, 4), (5, 8), (6, 1), (7, 5), (8, 0)
        };
        foreach ((int r, int c) in blanks)
        {
            state[r, c] = 0;
        }

        Board board = new(state);

        // Repeatedly apply the suggested move; every one must target an empty cell and match the
        // true solution. Following the hints to exhaustion should reconstruct the full solution.
        while (board.SuggestNextMove() is { } move)
        {
            board[move.Row, move.Col].ShouldBe(0);
            move.Value.ShouldBe(Solution[move.Row, move.Col]);
            Enum.IsDefined(move.Technique).ShouldBe(true); // reason is always a real technique
            board[move.Row, move.Col] = move.Value;
        }

        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                board[r, c].ShouldBe(Solution[r, c]);
            }
        }
    }
}
