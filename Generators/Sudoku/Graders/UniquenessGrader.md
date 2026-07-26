# Uniqueness grader

`UniquenessGrader` answers one question: does this puzzle have exactly one solution? It returns `Difficulty.Beyond` for exactly one solution and `Difficulty.Invalid` for zero or multiple solutions.

## How it works

The grader copies the puzzle into a reusable `byte[81]` workspace and builds row, column, and box digit masks. It then uses depth-first backtracking, stopping as soon as it finds a second solution.

At every recursion level it selects the empty cell with the fewest valid candidates (the minimum-remaining-values heuristic). Candidate masks are computed from the three occupancy masks, and row/column/box masks are updated and restored in place.

## Why the generator uses it

Puzzles in the `Beyond` band may need guessing or techniques that `RuleBasedGrader` does not implement. Rule-based completion therefore cannot prove their uniqueness. `SudokuGenerator` uses this grader while removing clues for `Difficulty.Beyond`.

## Performance and allocation behaviour

The fixed-size grid and occupancy arrays are allocated once when the `Board` is created, then reused. A generation into an existing board has no managed allocations. The solver does no recursive heap allocation; recursion consumes only stack frames.

## Limitations

This grader intentionally does not estimate human difficulty. A very easy and a very difficult uniquely solvable puzzle both receive `Beyond` from it. Its work is data-dependent: sparse or highly ambiguous puzzles can still require substantial search, although the MRV heuristic and two-solution cutoff reduce that cost.
