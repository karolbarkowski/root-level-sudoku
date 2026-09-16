namespace Generators.Sudoku;

public partial class SudokuGenerator
{
    /// <summary>Human-solving difficulty bands, ordered from easiest (1) to hardest (5).</summary>
    public enum Difficulty
    {
        Easy = 1,   // solvable with naked/hidden singles alone
        Medium = 2, // also needs locked candidates (pointing/claiming)
        Hard = 3,   // also needs naked/hidden pairs and triples
        Expert = 4, // also needs X-Wing / XY-Wing
        Invalid = 5 // not solvable with the techniques above (or not uniquely) — always rejected
    }
}
