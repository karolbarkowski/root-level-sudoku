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

        board.PlaceMove(0, 0, 5);
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
        board.PlaceMove(3, 4, 7);
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
        board.PlaceMove(0, 0, 5);
        board.Undo();
        board.CanRedo.ShouldBe(true);

        board.PlaceMove(1, 1, 3);

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

        board.PlaceMove(2, 2, 9); // overwrite 4 -> 9
        board[2, 2].ShouldBe(9);

        board.Undo();
        board[2, 2].ShouldBe(4); // original digit comes back, not 0
    }

    [Fact]
    public void UndoAll_Then_RedoAll_ReproducesEachState()
    {
        Board board = EmptyBoard();
        (int Row, int Col, int Value)[] moves =
        {
            (0, 0, 1),
            (1, 1, 2),
            (2, 2, 3),
        };

        foreach (var m in moves)
            board.PlaceMove(m.Row, m.Col, m.Value);

        // Undo everything -> back to the empty starting state.
        while (board.Undo()) { }
        for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
                board[r, c].ShouldBe(0);

        // Redo everything -> back to the fully-placed state.
        while (board.Redo()) { }
        foreach (var m in moves)
            board[m.Row, m.Col].ShouldBe(m.Value);
    }

    [Fact]
    public void Restore_RebuildsValues_AndBothHistories()
    {
        Board original = EmptyBoard();
        original.PlaceMove(0, 0, 5);
        original.PlaceMove(4, 4, 7);
        original.PlaceMove(8, 8, 2);
        original.Undo(); // (8,8) moves to the redo history

        Board restored = Board.Restore(original.GetCells(), original.GetUndoHistory(), original.GetRedoHistory());

        restored.GetCells().ShouldBe(original.GetCells());
        restored.Undo().ShouldBe(true);
        restored[4, 4].ShouldBe(0);
        restored.Redo().ShouldBe(true);
        restored.Redo().ShouldBe(true);
        restored[8, 8].ShouldBe(2);
        restored.CanRedo.ShouldBe(false);
    }

    [Fact]
    public void Restore_WithInvalidData_Throws()
    {
        Should.Throw<ArgumentException>(() => Board.Restore(new int[80], [], []));
        Should.Throw<ArgumentException>(() => Board.Restore(new int[81], [new MoveRecord(9, 0, 0, 1)], []));
        int[] cells = new int[81];
        cells[3] = 10;
        Should.Throw<ArgumentException>(() => Board.Restore(cells, [], []));
    }
}
