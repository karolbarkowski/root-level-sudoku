using Generators.Sudoku;
using Shouldly;


namespace Generators.Tests.Unit.Sudoku;

public class BoardTests
{
    private readonly int[,] testState = new int[,]
    {
            { 1, 1, 2, 3, 4, 5, 6, 7, 8 },
            { 1, 2, 3, 4, 5, 6, 7, 8, 9 },
            { 2, 3, 4, 5, 6, 7, 8, 9, 1 },
            { 3, 4, 5, 6, 7, 8, 9, 1, 2 },
            { 4, 5, 6, 7, 8, 9, 1, 2, 3 },
            { 5, 6, 7, 8, 9, 1, 2, 3, 4 },
            { 6, 7, 8, 9, 1, 2, 3, 4, 5 },
            { 7, 8, 9, 1, 2, 3, 4, 5, 6 },
            { 8, 9, 1, 2, 3, 4, 5, 6, 7 }
    };

    [Fact]
    public void InitialState_Should_HaveValidBlocks()
    {
        Board sut = new();

        AssertAllBlocksValid(sut);
    }

    [Fact]
    public void Randomize_Should_RefillBoardInPlaceWithValidBlocks()
    {
        Board sut = new();

        sut.Randomize();

        AssertAllBlocksValid(sut);
    }

    [Fact]
    public void Generate_Should_ReturnPuzzleWithNoRuleViolations()
    {
        Board board = SudokuGenerator.Generate(SudokuGenerator.Difficulty.Easy);

        for (int unit = 0; unit < 27; unit++)
        {
            int seen = 0;
            for (int offset = 0; offset < 9; offset++)
            {
                int row, col;
                if (unit < 9) { row = unit; col = offset; }
                else if (unit < 18) { row = offset; col = unit - 9; }
                else
                {
                    int box = unit - 18;
                    row = box / 3 * 3 + offset / 3;
                    col = box % 3 * 3 + offset % 3;
                }

                int value = board[row, col];
                if (value == 0) continue;
                (seen & (1 << value)).ShouldBe(0);
                seen |= 1 << value;
            }
        }
    }

    [Fact]
    public void Constructor_Should_RejectNon9x9State()
    {
        int[,] nonSquare = new int[9, 8];

        Should.Throw<ArgumentException>(() => new Board(nonSquare));
    }

    [Fact]
    public void Cost_Should_CountDuplicatesInRowsAndColumns()
    {
        Board sut = new(testState);

        Assert.Equal(2, sut.Cost());
    }

    [Fact]
    public void Swap_Shoul_SwapTwoElements()
    {
        int[,] testState = new int[,]
        {
            { 1, 1, 2, 3, 4, 5, 6, 7, 8 },
            { 1, 22, 3, 4, 5, 6, 7, 8, 9 },
            { 2, 3, 4, 5, 6, 7, 8, 9, 1 },
            { 3, 4, 5, 6, 7, 8, 9, 1, 2 },
            { 4, 5, 6, 7, 8, 9, 1, 2, 3 },
            { 5, 6, 7, 8, 9, 1, 2, 3, 4 },
            { 6, 7, 8, 9, 1, 2, 3, 4, 5 },
            { 7, 8, 9, 1, 2, 3, 4, 55, 6 },
            { 8, 9, 1, 2, 3, 4, 5, 6, 7 }
        };

        Board sut = new Board(testState);
        sut.Swap(1, 1, 7, 7);

        sut[1, 1].ShouldBe(55);
        sut[7, 7].ShouldBe(22);
    }

    private static void AssertAllBlocksValid(Board board)
    {
        for (int blockIndex = 0; blockIndex < 9; blockIndex++)
        {
            int startRow = blockIndex / 3 * 3;
            int startCol = blockIndex % 3 * 3;
            bool[] seen = new bool[10];

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int n = board[startRow + r, startCol + c];
                    Assert.InRange(n, 1, 9);
                    Assert.False(seen[n], $"Number {n} appears more than once in block {blockIndex}");
                    seen[n] = true;
                }
            }

            for (int number = 1; number <= 9; number++)
            {
                Assert.True(seen[number], $"Number {number} is missing from block {blockIndex}");
            }
        }
    }
}
