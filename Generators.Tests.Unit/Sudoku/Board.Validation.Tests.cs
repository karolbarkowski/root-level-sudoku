using Generators.Sudoku;
using Shouldly;

namespace Generators.Tests.Unit.Sudoku;

public class BoardValidationTests
{
    private static readonly int[,] Solved =
    {
        { 5, 3, 4, 6, 7, 8, 9, 1, 2 },
        { 6, 7, 2, 1, 9, 5, 3, 4, 8 },
        { 1, 9, 8, 3, 4, 2, 5, 6, 7 },
        { 8, 5, 9, 7, 6, 1, 4, 2, 3 },
        { 4, 2, 6, 8, 5, 3, 7, 9, 1 },
        { 7, 1, 3, 9, 2, 4, 8, 5, 6 },
        { 9, 6, 1, 5, 3, 7, 2, 8, 4 },
        { 2, 8, 7, 4, 1, 9, 6, 3, 5 },
        { 3, 4, 5, 2, 8, 6, 1, 7, 9 },
    };

    [Fact]
    public void IsSolved_True_ForCompleteValidGrid()
    {
        Board board = new(Solved);
        board.IsSolved().ShouldBe(true);
    }

    [Fact]
    public void IsSolved_False_WhenACellIsEmpty()
    {
        int[,] state = (int[,])Solved.Clone();
        state[4, 4] = 0;
        Board board = new(state);

        board.IsSolved().ShouldBe(false);
    }

    [Fact]
    public void IsSolved_False_WhenAUnitHasADuplicate()
    {
        int[,] state = (int[,])Solved.Clone();
        state[0, 0] = state[0, 1]; // duplicate digit in the first row (and its box/column)
        Board board = new(state);

        board.IsSolved().ShouldBe(false);
    }

    [Fact]
    public void IsSolved_False_ForAFreshlyGeneratedPuzzle()
    {
        // A generated puzzle has blanks, so it must not read as solved.
        Board board = SudokuGenerator.Generate(SudokuGenerator.Difficulty.Easy);
        board.IsSolved().ShouldBe(false);
    }
}
