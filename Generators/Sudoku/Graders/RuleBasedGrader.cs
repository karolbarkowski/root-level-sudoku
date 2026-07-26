using System.Numerics;
using static Generators.Sudoku.SudokuGenerator;

namespace Generators.Sudoku.Graders;


/// <summary>
/// A rule-based Sudoku solver that mimics the techniques a human would use and reports the
/// hardest technique it needed. It never guesses: if the implemented techniques can't finish
/// the puzzle it reports <see cref="Difficulty.Beyond"/>. Every elimination it makes is a
/// forced deduction, so a full solve also proves the puzzle has a single solution.
/// </summary>
internal sealed class RuleBasedGrader
{
    // All nine digits 1..9 packed as a candidate bitmask (bit d set => digit d is possible).
    // Bit 0 is left unused so each digit maps directly onto its own bit.
    private const int AllDigits = 0b11_1111_1110;

    // Flat 9x9 grids indexed as r * 9 + c. Flat arrays let the JIT eliminate bounds checks and
    // address with a single index, which 2D arrays (int[,]) cannot; on ARM the difference is
    // significant and this is the hottest code in Easy..Expert generation.
    private readonly byte[] _cells = new byte[81]; // working copy of the puzzle (0 = empty)
    private readonly int[] _cand = new int[81];    // per empty-cell candidate bitmask

    // Precomputed row/column of every (unit, position) pair, so the per-cell scans that dominate
    // every technique index a table instead of recomputing div/mod on each access.
    private static readonly byte[] UnitRow = new byte[27 * 9];
    private static readonly byte[] UnitCol = new byte[27 * 9];

    static RuleBasedGrader()
    {
        for (int unit = 0; unit < 27; unit++)
        {
            for (int idx = 0; idx < 9; idx++)
            {
                int r, c;
                if (unit < 9) { r = unit; c = idx; }                 // rows
                else if (unit < 18) { r = idx; c = unit - 9; }       // columns
                else                                                 // boxes
                {
                    int b = unit - 18;
                    r = b / 3 * 3 + idx / 3;
                    c = b % 3 * 3 + idx % 3;
                }
                UnitRow[unit * 9 + idx] = (byte)r;
                UnitCol[unit * 9 + idx] = (byte)c;
            }
        }
    }

    /// <summary>
    /// Solves a copy of <paramref name="puzzle"/> with human techniques and returns the
    /// hardest one required, or <see cref="Difficulty.Beyond"/> if it can't be finished.
    /// </summary>
    public Difficulty Grade(ReadOnlySpan<byte> puzzle) => Grade(puzzle, Difficulty.Beyond);

    /// <summary>
    /// Same as <see cref="Grade(ReadOnlySpan{byte})"/>, but bails out as soon as the solve is
    /// known to exceed <paramref name="maxDifficulty"/>. Because the hardest technique used only
    /// ever rises, once it passes the ceiling the exact final grade no longer matters to a caller
    /// that only asks "is this &gt; target?" — so callers that gate on that (clue removal) get the
    /// identical accept/reject decision while skipping the rest of an already-too-hard solve.
    /// The returned value is then simply <em>some</em> difficulty above the ceiling.
    /// </summary>
    public Difficulty Grade(ReadOnlySpan<byte> puzzle, Difficulty maxDifficulty)
    {
        // Work on a private copy so grading never disturbs the caller's board.
        puzzle.CopyTo(_cells);

        InitCandidates();

        Difficulty hardest = Difficulty.Easy;
        while (!Solved())
        {
            // Always reach for the cheapest technique first and only escalate when the easy
            // ones stop making progress. `hardest` tracks the toughest technique we leaned on.
            if (ApplyNakedSingles()) continue;                                          // Easy
            if (ApplyHiddenSingles()) continue;                                         // Easy
            if (ApplyLockedCandidates()) { Raise(ref hardest, Difficulty.Medium); if (hardest > maxDifficulty) return hardest; continue; }
            if (ApplyNakedSubsets()) { Raise(ref hardest, Difficulty.Hard); if (hardest > maxDifficulty) return hardest; continue; }
            if (ApplyHiddenSubsets()) { Raise(ref hardest, Difficulty.Hard); if (hardest > maxDifficulty) return hardest; continue; }
            if (ApplyXWing()) { Raise(ref hardest, Difficulty.Expert); if (hardest > maxDifficulty) return hardest; continue; }
            if (ApplyXYWing()) { Raise(ref hardest, Difficulty.Expert); if (hardest > maxDifficulty) return hardest; continue; }

            // Nothing advanced the grid: this puzzle is beyond our solver.
            return Difficulty.Beyond;
        }

        return hardest;
    }

    /// <summary>
    /// Finds the next move a human solver could make from the current position. It runs the same
    /// cheapest-first technique cascade as <see cref="Grade"/>, but stops at the first cell it can
    /// fill instead of solving the whole puzzle. Advanced techniques only *eliminate* candidates,
    /// so the move returned is always a naked or hidden single — though one that may only have
    /// become available after some locked-candidate / subset / fish eliminations.
    /// Returns false when no forced move exists (the puzzle needs guessing, is already complete,
    /// or is in a contradictory state).
    ///
    /// <paramref name="technique"/> reports *why* the move is available: for a directly-playable
    /// single it is the single itself; when the position needed eliminations first, it is the
    /// technique that unlocked the move (that elimination is applied immediately before the single
    /// appears, so it is the change that made the placement possible).
    /// </summary>
    public bool TryFindNextMove(ReadOnlySpan<byte> puzzle, out int row, out int col, out int value, out Technique technique)
    {
        puzzle.CopyTo(_cells);

        InitCandidates();

        // Null until an elimination is needed; once set, it's the technique that unlocked the move.
        Technique? unlockedBy = null;
        while (true)
        {
            // A single is a move we can play right now, so always prefer it.
            if (TryFindNakedSingle(out row, out col, out value))
            {
                technique = unlockedBy ?? Technique.NakedSingle;
                return true;
            }
            if (TryFindHiddenSingle(out row, out col, out value))
            {
                technique = unlockedBy ?? Technique.HiddenSingle;
                return true;
            }

            // No single yet — try to expose one with a candidate elimination, cheapest first.
            if (ApplyLockedCandidates()) { unlockedBy = Technique.LockedCandidates; continue; }
            if (ApplyNakedSubsets()) { unlockedBy = Technique.NakedSubset; continue; }
            if (ApplyHiddenSubsets()) { unlockedBy = Technique.HiddenSubset; continue; }
            if (ApplyXWing()) { unlockedBy = Technique.XWing; continue; }
            if (ApplyXYWing()) { unlockedBy = Technique.XYWing; continue; }

            // Nothing more can be deduced logically.
            row = col = value = 0;
            technique = default;
            return false;
        }
    }

    /// <summary>Reports (without placing) the first cell that has exactly one candidate.</summary>
    private bool TryFindNakedSingle(out int row, out int col, out int value)
    {
        for (int r = 0; r < 9; r++)
        {
            int rowBase = r * 9;
            for (int c = 0; c < 9; c++)
            {
                int cell = rowBase + c;
                if (_cells[cell] == 0 && PopCount(_cand[cell]) == 1)
                {
                    row = r;
                    col = c;
                    value = TrailingDigit(_cand[cell]);
                    return true;
                }
            }
        }
        row = col = value = 0;
        return false;
    }

    /// <summary>Reports (without placing) the first digit that fits only one cell of a unit.</summary>
    private bool TryFindHiddenSingle(out int row, out int col, out int value)
    {
        for (int unit = 0; unit < 27; unit++) // 9 rows, 9 columns, 9 boxes
        {
            for (int d = 1; d <= 9; d++)
            {
                int bit = 1 << d, count = 0, hitR = -1, hitC = -1;
                for (int idx = 0; idx < 9; idx++)
                {
                    UnitCell(unit, idx, out int r, out int c);
                    int cell = r * 9 + c;
                    if (_cells[cell] == 0 && (_cand[cell] & bit) != 0)
                    {
                        count++;
                        hitR = r;
                        hitC = c;
                    }
                }
                if (count == 1)
                {
                    row = hitR;
                    col = hitC;
                    value = d;
                    return true;
                }
            }
        }
        row = col = value = 0;
        return false;
    }

    // ---- setup -----------------------------------------------------------------------

    /// <summary>Seeds each empty cell with the digits not already used in its row/column/box.</summary>
    private void InitCandidates()
    {
        for (int r = 0; r < 9; r++)
        {
            int rowBase = r * 9;
            for (int c = 0; c < 9; c++)
            {
                int cell = rowBase + c;
                if (_cells[cell] != 0)
                {
                    _cand[cell] = 0;
                    continue;
                }

                int used = 0;
                for (int k = 0; k < 9; k++)
                {
                    used |= 1 << _cells[rowBase + k]; // same row
                    used |= 1 << _cells[k * 9 + c];   // same column
                }
                int br = r / 3 * 3, bc = c / 3 * 3;
                for (int dr = 0; dr < 3; dr++)
                {
                    for (int dc = 0; dc < 3; dc++)
                    {
                        used |= 1 << _cells[(br + dr) * 9 + bc + dc]; // same box
                    }
                }

                // Empty peers contribute bit 0, which AllDigits masks away.
                _cand[cell] = AllDigits & ~used;
            }
        }
    }

    /// <summary>Places <paramref name="digit"/> and strips it from every peer's candidates.</summary>
    private void Place(int r, int c, int digit)
    {
        _cells[r * 9 + c] = (byte)digit;
        _cand[r * 9 + c] = 0;

        int clear = ~(1 << digit);
        for (int k = 0; k < 9; k++)
        {
            _cand[r * 9 + k] &= clear; // row peers
            _cand[k * 9 + c] &= clear; // column peers
        }
        int br = r / 3 * 3, bc = c / 3 * 3;
        for (int dr = 0; dr < 3; dr++)
        {
            for (int dc = 0; dc < 3; dc++)
            {
                _cand[(br + dr) * 9 + bc + dc] &= clear; // box peers
            }
        }
    }

    private bool Solved()
    {
        for (int cell = 0; cell < 81; cell++)
        {
            if (_cells[cell] == 0) return false;
        }
        return true;
    }

    // ---- techniques ------------------------------------------------------------------

    /// <summary>Naked single: a cell with exactly one remaining candidate must be that digit.</summary>
    private bool ApplyNakedSingles()
    {
        bool progress = false;
        for (int r = 0; r < 9; r++)
        {
            int rowBase = r * 9;
            for (int c = 0; c < 9; c++)
            {
                int cell = rowBase + c;
                if (_cells[cell] == 0 && PopCount(_cand[cell]) == 1)
                {
                    Place(r, c, TrailingDigit(_cand[cell]));
                    progress = true;
                }
            }
        }
        return progress;
    }

    /// <summary>Hidden single: a digit that fits only one cell of a unit must go in that cell.</summary>
    private bool ApplyHiddenSingles()
    {
        bool progress = false;
        for (int unit = 0; unit < 27; unit++) // 9 rows, 9 columns, 9 boxes
        {
            for (int d = 1; d <= 9; d++)
            {
                int bit = 1 << d, count = 0, hitR = -1, hitC = -1;
                for (int idx = 0; idx < 9; idx++)
                {
                    UnitCell(unit, idx, out int r, out int c);
                    int cell = r * 9 + c;
                    if (_cells[cell] == 0 && (_cand[cell] & bit) != 0)
                    {
                        count++;
                        hitR = r;
                        hitC = c;
                    }
                }
                if (count == 1)
                {
                    Place(hitR, hitC, d);
                    progress = true;
                }
            }
        }
        return progress;
    }

    /// <summary>
    /// Locked candidates. Pointing: when a digit inside a box is confined to a single row or
    /// column, it can be removed from the rest of that row/column. Claiming: when a digit in a
    /// row or column is confined to a single box, it can be removed from the rest of that box.
    /// </summary>
    private bool ApplyLockedCandidates()
    {
        bool progress = false;

        // Pointing: box -> row / column.
        for (int box = 0; box < 9; box++)
        {
            int br = box / 3 * 3, bc = box % 3 * 3;
            for (int d = 1; d <= 9; d++)
            {
                int bit = 1 << d, rowsSeen = 0, colsSeen = 0, hits = 0;
                for (int dr = 0; dr < 3; dr++)
                {
                    for (int dc = 0; dc < 3; dc++)
                    {
                        int r = br + dr, c = bc + dc;
                        int cell = r * 9 + c;
                        if (_cells[cell] == 0 && (_cand[cell] & bit) != 0)
                        {
                            rowsSeen |= 1 << r;
                            colsSeen |= 1 << c;
                            hits++;
                        }
                    }
                }
                if (hits < 2) continue; // a single cell is a hidden single, handled elsewhere

                if (PopCount(rowsSeen) == 1) // all candidates share one row
                {
                    int r = TrailingDigit(rowsSeen);
                    for (int c = 0; c < 9; c++)
                    {
                        if (c / 3 != bc / 3 && EliminateCandidate(r, c, d)) progress = true;
                    }
                }
                if (PopCount(colsSeen) == 1) // all candidates share one column
                {
                    int c = TrailingDigit(colsSeen);
                    for (int r = 0; r < 9; r++)
                    {
                        if (r / 3 != br / 3 && EliminateCandidate(r, c, d)) progress = true;
                    }
                }
            }
        }

        // Claiming: row -> box.
        for (int r = 0; r < 9; r++)
        {
            int rowBase = r * 9;
            for (int d = 1; d <= 9; d++)
            {
                int bit = 1 << d, boxesSeen = 0, hits = 0;
                for (int c = 0; c < 9; c++)
                {
                    int cell = rowBase + c;
                    if (_cells[cell] == 0 && (_cand[cell] & bit) != 0)
                    {
                        boxesSeen |= 1 << (c / 3);
                        hits++;
                    }
                }
                if (hits >= 2 && PopCount(boxesSeen) == 1)
                {
                    int boxCol = TrailingDigit(boxesSeen) * 3;
                    int br = r / 3 * 3;
                    for (int dr = 0; dr < 3; dr++)
                    {
                        for (int dc = 0; dc < 3; dc++)
                        {
                            if (br + dr != r && EliminateCandidate(br + dr, boxCol + dc, d)) progress = true;
                        }
                    }
                }
            }
        }

        // Claiming: column -> box.
        for (int c = 0; c < 9; c++)
        {
            for (int d = 1; d <= 9; d++)
            {
                int bit = 1 << d, boxesSeen = 0, hits = 0;
                for (int r = 0; r < 9; r++)
                {
                    int cell = r * 9 + c;
                    if (_cells[cell] == 0 && (_cand[cell] & bit) != 0)
                    {
                        boxesSeen |= 1 << (r / 3);
                        hits++;
                    }
                }
                if (hits >= 2 && PopCount(boxesSeen) == 1)
                {
                    int boxRow = TrailingDigit(boxesSeen) * 3;
                    int bc = c / 3 * 3;
                    for (int dc = 0; dc < 3; dc++)
                    {
                        for (int dr = 0; dr < 3; dr++)
                        {
                            if (bc + dc != c && EliminateCandidate(boxRow + dr, bc + dc, d)) progress = true;
                        }
                    }
                }
            }
        }

        return progress;
    }

    /// <summary>
    /// Naked subsets (pairs/triples): if k cells in a unit together hold exactly k distinct
    /// candidates, those digits are locked to those cells and can be cleared from the rest of
    /// the unit. Returns as soon as one elimination is made so cached masks can't go stale.
    /// </summary>
    private bool ApplyNakedSubsets()
    {
        Span<int> idxOf = stackalloc int[9];  // positions (0..8) of the empty cells in the unit
        Span<int> maskOf = stackalloc int[9]; // their candidate masks

        for (int unit = 0; unit < 27; unit++)
        {
            int n = 0;
            for (int idx = 0; idx < 9; idx++)
            {
                UnitCell(unit, idx, out int r, out int c);
                int cell = r * 9 + c;
                if (_cells[cell] == 0)
                {
                    idxOf[n] = idx;
                    maskOf[n] = _cand[cell];
                    n++;
                }
            }

            // pairs
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    int combined = maskOf[i] | maskOf[j];
                    if (PopCount(combined) == 2 &&
                        EliminateDigitsFromUnit(unit, combined, idxOf[i], idxOf[j], -1))
                    {
                        return true;
                    }
                }
            }

            // triples
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    for (int k = j + 1; k < n; k++)
                    {
                        int combined = maskOf[i] | maskOf[j] | maskOf[k];
                        if (PopCount(combined) == 3 &&
                            EliminateDigitsFromUnit(unit, combined, idxOf[i], idxOf[j], idxOf[k]))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Hidden subsets (pairs/triples): if k digits in a unit can only go in the same k cells,
    /// every other candidate can be cleared from those cells. Returns on the first elimination.
    /// </summary>
    private bool ApplyHiddenSubsets()
    {
        Span<int> posOf = stackalloc int[10]; // for each digit 1..9, bitmask of unit positions it can occupy

        for (int unit = 0; unit < 27; unit++)
        {
            for (int d = 1; d <= 9; d++)
            {
                posOf[d] = 0;
            }
            for (int idx = 0; idx < 9; idx++)
            {
                UnitCell(unit, idx, out int r, out int c);
                int cell = r * 9 + c;
                if (_cells[cell] == 0)
                {
                    int m = _cand[cell];
                    while (m != 0)
                    {
                        posOf[TrailingDigit(m)] |= 1 << idx;
                        m &= m - 1; // clear lowest set bit
                    }
                }
            }

            // hidden pairs
            for (int d1 = 1; d1 <= 9; d1++)
            {
                if (posOf[d1] == 0) continue;
                for (int d2 = d1 + 1; d2 <= 9; d2++)
                {
                    if (posOf[d2] == 0) continue;
                    int cells = posOf[d1] | posOf[d2];
                    if (PopCount(cells) == 2 &&
                        ConfineCellsToDigits(unit, cells, (1 << d1) | (1 << d2)))
                    {
                        return true;
                    }
                }
            }

            // hidden triples
            for (int d1 = 1; d1 <= 9; d1++)
            {
                if (posOf[d1] == 0) continue;
                for (int d2 = d1 + 1; d2 <= 9; d2++)
                {
                    if (posOf[d2] == 0) continue;
                    for (int d3 = d2 + 1; d3 <= 9; d3++)
                    {
                        if (posOf[d3] == 0) continue;
                        int cells = posOf[d1] | posOf[d2] | posOf[d3];
                        if (PopCount(cells) == 3 &&
                            ConfineCellsToDigits(unit, cells, (1 << d1) | (1 << d2) | (1 << d3)))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// X-Wing: if a digit is restricted to the same two columns across two rows (or the same
    /// two rows across two columns), it can be removed from those columns/rows everywhere else.
    /// </summary>
    private bool ApplyXWing()
    {
        Span<int> lineMask = stackalloc int[9];

        for (int d = 1; d <= 9; d++)
        {
            int bit = 1 << d;

            // Row-based: find two rows whose candidate columns for d are the same pair.
            for (int r = 0; r < 9; r++)
            {
                int rowBase = r * 9, m = 0;
                for (int c = 0; c < 9; c++)
                {
                    int cell = rowBase + c;
                    if (_cells[cell] == 0 && (_cand[cell] & bit) != 0) m |= 1 << c;
                }
                lineMask[r] = m;
            }
            for (int r1 = 0; r1 < 9; r1++)
            {
                if (PopCount(lineMask[r1]) != 2) continue;
                for (int r2 = r1 + 1; r2 < 9; r2++)
                {
                    if (lineMask[r2] != lineMask[r1]) continue;
                    bool changed = false;
                    for (int r = 0; r < 9; r++)
                    {
                        if (r == r1 || r == r2) continue;
                        int cols = lineMask[r1];
                        while (cols != 0)
                        {
                            int c = TrailingDigit(cols);
                            cols &= cols - 1;
                            if (EliminateCandidate(r, c, d)) changed = true;
                        }
                    }
                    if (changed) return true;
                }
            }

            // Column-based: find two columns whose candidate rows for d are the same pair.
            for (int c = 0; c < 9; c++)
            {
                int m = 0;
                for (int r = 0; r < 9; r++)
                {
                    int cell = r * 9 + c;
                    if (_cells[cell] == 0 && (_cand[cell] & bit) != 0) m |= 1 << r;
                }
                lineMask[c] = m;
            }
            for (int c1 = 0; c1 < 9; c1++)
            {
                if (PopCount(lineMask[c1]) != 2) continue;
                for (int c2 = c1 + 1; c2 < 9; c2++)
                {
                    if (lineMask[c2] != lineMask[c1]) continue;
                    bool changed = false;
                    for (int c = 0; c < 9; c++)
                    {
                        if (c == c1 || c == c2) continue;
                        int rows = lineMask[c1];
                        while (rows != 0)
                        {
                            int r = TrailingDigit(rows);
                            rows &= rows - 1;
                            if (EliminateCandidate(r, c, d)) changed = true;
                        }
                    }
                    if (changed) return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// XY-Wing: a bi-value pivot {X,Y} with two bi-value peers {X,Z} and {Y,Z}. Whatever the
    /// pivot turns out to be, one of the pincers becomes Z, so Z can be removed from every cell
    /// that sees both pincers.
    /// </summary>
    private bool ApplyXYWing()
    {
        Span<int> peerR = stackalloc int[20]; // a cell has at most 20 peers
        Span<int> peerC = stackalloc int[20];
        Span<int> peerM = stackalloc int[20];

        for (int pr = 0; pr < 9; pr++)
        {
            for (int pc = 0; pc < 9; pc++)
            {
                if (_cells[pr * 9 + pc] != 0) continue;
                int pivot = _cand[pr * 9 + pc];
                if (PopCount(pivot) != 2) continue;

                // Collect the pivot's bi-value peers.
                int n = 0;
                for (int r = 0; r < 9; r++)
                {
                    for (int c = 0; c < 9; c++)
                    {
                        int cell = r * 9 + c;
                        if ((r == pr && c == pc) || _cells[cell] != 0) continue;
                        if (PopCount(_cand[cell]) != 2 || !Sees(pr, pc, r, c)) continue;
                        peerR[n] = r;
                        peerC[n] = c;
                        peerM[n] = _cand[cell];
                        n++;
                    }
                }

                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        int x = peerM[i] & pivot; // digit pincer i shares with the pivot
                        int y = peerM[j] & pivot; // digit pincer j shares with the pivot
                        if (PopCount(x) != 1 || PopCount(y) != 1 || x == y) continue;

                        int z1 = peerM[i] & ~pivot; // the "other" digit of each pincer
                        int z2 = peerM[j] & ~pivot;
                        if (z1 != z2 || PopCount(z1) != 1) continue; // both must share digit Z

                        int zDigit = TrailingDigit(z1);
                        bool changed = false;
                        for (int r = 0; r < 9; r++)
                        {
                            for (int c = 0; c < 9; c++)
                            {
                                if (_cells[r * 9 + c] != 0) continue;
                                if (r == pr && c == pc) continue;
                                if ((r == peerR[i] && c == peerC[i]) || (r == peerR[j] && c == peerC[j])) continue;
                                if (Sees(peerR[i], peerC[i], r, c) && Sees(peerR[j], peerC[j], r, c) &&
                                    EliminateCandidate(r, c, zDigit))
                                {
                                    changed = true;
                                }
                            }
                        }
                        if (changed) return true;
                    }
                }
            }
        }
        return false;
    }

    // ---- small helpers ---------------------------------------------------------------

    /// <summary>Clears every digit in <paramref name="digits"/> from the unit's empty cells,
    /// except the (up to three) selected positions. Returns whether anything changed.</summary>
    private bool EliminateDigitsFromUnit(int unit, int digits, int keep0, int keep1, int keep2)
    {
        bool changed = false;
        for (int idx = 0; idx < 9; idx++)
        {
            if (idx == keep0 || idx == keep1 || idx == keep2) continue;
            UnitCell(unit, idx, out int r, out int c);
            int cell = r * 9 + c;
            if (_cells[cell] != 0) continue;
            int before = _cand[cell];
            _cand[cell] &= ~digits;
            if (_cand[cell] != before) changed = true;
        }
        return changed;
    }

    /// <summary>Reduces the given unit positions to only <paramref name="keepDigits"/>.
    /// Returns whether anything changed.</summary>
    private bool ConfineCellsToDigits(int unit, int positions, int keepDigits)
    {
        bool changed = false;
        while (positions != 0)
        {
            int idx = TrailingDigit(positions);
            positions &= positions - 1;
            UnitCell(unit, idx, out int r, out int c);
            int cell = r * 9 + c;
            int before = _cand[cell];
            _cand[cell] &= keepDigits;
            if (_cand[cell] != before) changed = true;
        }
        return changed;
    }

    private bool EliminateCandidate(int r, int c, int digit)
    {
        int cell = r * 9 + c, bit = 1 << digit;
        if ((_cand[cell] & bit) == 0) return false;
        _cand[cell] &= ~bit;
        return true;
    }

    /// <summary>Maps a unit index (0-8 rows, 9-17 columns, 18-26 boxes) and a position
    /// 0..8 within it to a board cell, using the precomputed lookup tables.</summary>
    private static void UnitCell(int unit, int idx, out int r, out int c)
    {
        int p = unit * 9 + idx;
        r = UnitRow[p];
        c = UnitCol[p];
    }

    /// <summary>True when the two cells share a row, column, or box (i.e. are peers).</summary>
    private static bool Sees(int r1, int c1, int r2, int c2)
        => r1 == r2 || c1 == c2 || (r1 / 3 == r2 / 3 && c1 / 3 == c2 / 3);

    private static void Raise(ref Difficulty current, Difficulty candidate)
    {
        if (candidate > current) current = candidate;
    }

    private static int PopCount(int mask) => BitOperations.PopCount((uint)mask);

    // For a single-bit mask this returns the digit; more generally, the lowest set bit's index.
    private static int TrailingDigit(int mask) => BitOperations.TrailingZeroCount((uint)mask);
}
