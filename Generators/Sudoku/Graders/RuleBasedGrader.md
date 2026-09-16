# Rule-based grader

`RuleBasedGrader` evaluates a puzzle by applying human-style, non-guessing solving techniques. It returns the hardest technique required to solve the entire puzzle.

## What it reports

| Result | Meaning |
|---|---|
| `Easy` | Naked and hidden singles were sufficient. |
| `Medium` | Locked candidates were required. |
| `Hard` | Naked or hidden pairs/triples were required. |
| `Expert` | X-Wing or XY-Wing was required. |
| `Invalid` | The implemented techniques could not finish the puzzle. |

It also provides `TryFindNextMove`, which returns the next forced placement and the technique that exposed it. This powers the in-game hint feature.

## How it works

The grader copies the input to reusable private work arrays, creates a candidate bit mask for each empty cell, and repeatedly applies the cheapest available technique. A placement updates candidate masks for its peers. Candidate-elimination techniques are retried until a placement becomes possible or no progress remains.

The supported order is: naked single, hidden single, locked candidates, naked subsets, hidden subsets, X-Wing, then XY-Wing.

## Uniqueness guarantee

When the grader completely solves a valid puzzle, every placement was forced. Therefore a completed solve also proves uniqueness. This is why `SudokuGenerator` uses this grader for clue removal at every difficulty.

## Performance and allocation behaviour

The grader owns its two scratch 9x9 arrays and is retained by `Board`. Calls to `Grade` and `TryFindNextMove` reuse that memory and allocate nothing. It is stateful while grading, so a single `Board` must not use it concurrently from multiple threads.

## Limitations

`Invalid` means only that this implementation could not solve the puzzle using its current technique set. The puzzle may still have a unique solution that needs chains or guessing; the generator simply never produces such puzzles.
