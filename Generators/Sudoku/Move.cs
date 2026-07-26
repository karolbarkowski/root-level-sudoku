namespace Generators.Sudoku;

/// <summary>
/// A suggested move: place <see cref="Value"/> (1-9) at the cell given by the 0-based
/// <see cref="Row"/> and <see cref="Col"/>. <see cref="Technique"/> is the reasoning that makes
/// the move a good one — useful for explaining the hint in the UI.
/// </summary>
public readonly record struct Move(int Row, int Col, int Value, Technique Technique);
