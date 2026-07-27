namespace Generators.Sudoku;

/// <summary>
/// One entry in the board's move history: <see cref="NewValue"/> was written into the cell at
/// (<see cref="Row"/>, <see cref="Col"/>), replacing <see cref="PreviousValue"/>. Storing the
/// previous value lets Undo restore it exactly, even when a filled cell was overwritten.
/// </summary>
internal readonly record struct MoveRecord(int Row, int Col, int PreviousValue, int NewValue);
