using Generators.Sudoku;
using Shouldly;

namespace Generators.Tests.Unit.Sudoku;

public class BoardHistoryTests
{
    private static Board EmptyBoard() => new(new int[9, 9]);

    [Fact]
    public void PlaceMove_Then_Undo_RestoresPreviousValue()
    {
        Board board = EmptyBoard();
        board.CanUndo.ShouldBe(false);

        board.PlaceMove(new Move(0, 0, 5, Technique.NakedSingle));
        board[0, 0].ShouldBe(5);
        board.CanUndo.ShouldBe(true);
        board.CanRedo.ShouldBe(false);

        board.Undo().ShouldBe(true);
        board[0, 0].ShouldBe(0);
        board.CanUndo.ShouldBe(false);
        board.CanRedo.ShouldBe(true);
    }

    [Fact]
    public void Undo_Then_Redo_ReappliesTheMove()
    {
        Board board = EmptyBoard();
        board.PlaceMove(new Move(3, 4, 7, Technique.NakedSingle));
        board.Undo();

        board.Redo().ShouldBe(true);
        board[3, 4].ShouldBe(7);
        board.CanRedo.ShouldBe(false);
        board.CanUndo.ShouldBe(true);
    }

    [Fact]
    public void PlaceMove_AfterUndo_ClearsTheRedoBranch()
    {
        Board board = EmptyBoard();
        board.PlaceMove(new Move(0, 0, 5, Technique.NakedSingle));
        board.Undo();
        board.CanRedo.ShouldBe(true);

        board.PlaceMove(new Move(1, 1, 3, Technique.NakedSingle));

        board.CanRedo.ShouldBe(false);
        board.Redo().ShouldBe(false);
    }

    [Fact]
    public void Undo_And_Redo_OnEmptyHistory_ReturnFalse_AndDoNotThrow()
    {
        Board board = EmptyBoard();

        board.Undo().ShouldBe(false);
        board.Redo().ShouldBe(false);
    }

    [Fact]
    public void Undo_RestoresTheOverwrittenDigit_NotJustBlank()
    {
        int[,] state = new int[9, 9];
        state[2, 2] = 4; // cell already holds a digit
        Board board = new(state);

        board.PlaceMove(new Move(2, 2, 9, Technique.NakedSingle)); // overwrite 4 -> 9
        board[2, 2].ShouldBe(9);

        board.Undo();
        board[2, 2].ShouldBe(4); // original digit comes back, not 0
    }

    [Fact]
    public void UndoAll_Then_RedoAll_ReproducesEachState()
    {
        Board board = EmptyBoard();
        Move[] moves =
        {
            new(0, 0, 1, Technique.NakedSingle),
            new(1, 1, 2, Technique.NakedSingle),
            new(2, 2, 3, Technique.NakedSingle),
        };

        foreach (Move m in moves)
            board.PlaceMove(m);

        // Undo everything -> back to the empty starting state.
        while (board.Undo()) { }
        for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
                board[r, c].ShouldBe(0);

        // Redo everything -> back to the fully-placed state.
        while (board.Redo()) { }
        foreach (Move m in moves)
            board[m.Row, m.Col].ShouldBe(m.Value);
    }
}
