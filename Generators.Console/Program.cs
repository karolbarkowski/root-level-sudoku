
using Generators.Sudoku;
using System.Diagnostics;

Stopwatch sw = new();
sw.Start();

Board board = SudokuGenerator.Generate(SudokuGenerator.Difficulty.Easy);
sw.Stop();

Console.WriteLine($"Generated in {sw.ElapsedMilliseconds}ms");
Console.WriteLine(board.ToString());
Console.WriteLine("Keys: [any] apply next move  |  [u] undo  |  [r] redo  |  [q] quit");
Console.WriteLine();

while (true)
{
    char key = Console.ReadKey(intercept: true).KeyChar;
    if (key is 'q' or 'Q')
        break;

    switch (key)
    {
        case 'u' or 'U':
            Console.WriteLine(board.Undo() ? "Undid last move." : "Nothing to undo.");
            break;

        case 'r' or 'R':
            Console.WriteLine(board.Redo() ? "Redid move." : "Nothing to redo.");
            break;

        default:
            if (board.SuggestNextMove() is not { } move)
            {
                Console.WriteLine("No forced move available.");
                continue;
            }
            board.PlaceMove(move.Row, move.Col, move.Value);
            Console.WriteLine($"Placed {move.Value} at column {move.Col}, row {move.Row} ({move.Technique.ToDisplayName()} technique)");
            break;
    }

    Console.WriteLine(board.ToString());
    Console.WriteLine();
}
