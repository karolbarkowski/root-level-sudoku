namespace Generators.Sudoku;

/// <summary>
/// The human solving technique that justifies a suggested <see cref="Move"/> — the reasoning a
/// player would use to know the move is correct. For a directly-available placement this is
/// <see cref="NakedSingle"/> or <see cref="HiddenSingle"/>; for a harder position it is the
/// technique whose candidate eliminations exposed the placement (locked candidates, a subset, or a
/// fish/wing). Ordered from easiest to hardest.
/// </summary>
public enum Technique
{
    // A cell has only one candidate left, so that digit must go there.
    NakedSingle,

    // A digit has only one possible cell within a row, column, or box, so it must go there.
    HiddenSingle,

    // A digit's candidates in a box all share one row/column (or vice versa), letting it be
    // eliminated from the rest of that line or box (a.k.a. pointing / claiming).
    LockedCandidates,

    // N cells in a unit share exactly N candidates between them, so those digits can be removed
    // from the unit's other cells (naked pair for N=2, naked triple for N=3).
    NakedSubset,

    // N digits in a unit can only go in the same N cells, so all other candidates can be removed
    // from those cells (hidden pair for N=2, hidden triple for N=3).
    HiddenSubset,

    // A digit is confined to the same two columns across two rows (or two rows across two columns),
    // letting it be eliminated from those columns/rows elsewhere.
    XWing,

    // A bi-value "pivot" {X,Y} with two peers {X,Z} and {Y,Z}: whichever value the pivot takes, one
    // peer becomes Z, so Z can be eliminated from any cell that sees both peers.
    XYWing
}

public static class TechniqueExtensions
{
    /// <summary>A short, human-readable label suitable for showing in a hint UI.</summary>
    public static string ToDisplayName(this Technique technique) => technique switch
    {
        Technique.NakedSingle => "Naked single",
        Technique.HiddenSingle => "Hidden single",
        Technique.LockedCandidates => "Locked candidates",
        Technique.NakedSubset => "Naked pair/triple",
        Technique.HiddenSubset => "Hidden pair/triple",
        Technique.XWing => "X-Wing",
        Technique.XYWing => "XY-Wing",
        _ => technique.ToString()
    };
}
