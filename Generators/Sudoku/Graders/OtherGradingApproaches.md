# Other Sudoku grading approaches

This project currently uses a human-style rule grader for every difficulty, `Easy` through `Expert`. The following approaches are potential additions or replacements, depending on what “difficulty” should mean.

| Approach | What it measures | Advantages | Disadvantages | Best fit |
|---|---|---|---|---|
| Weighted human-technique solver | Techniques used, with a score per technique | Explainable; maps well to player expectations; can distinguish puzzles within a band | Requires maintained technique weights and more implemented techniques | Fine-grained displayed difficulty |
| Human solve-path metrics | Number of deductions, branching, candidate reductions, and technique transitions | More nuanced than just the hardest technique | Calibration is subjective; more bookkeeping | Ranking puzzles within each difficulty band |
| Backtracking-node count | Search nodes, guesses, or recursion depth | Very fast to implement; deterministic with fixed ordering | Poor proxy for human difficulty; solver ordering strongly affects score | Internal complexity filter or generation budget |
| Exact-cover / DLX node count | Search effort using Algorithm X / Dancing Links | Extremely fast uniqueness checking; efficient on large batches | Still not human difficulty; more complex implementation | High-throughput uniqueness validation |
| SAT/constraint-programming metrics | Conflicts, propagation, decisions, or learned clauses | Powerful and flexible; can validate variants of Sudoku | External dependency or substantial implementation; scores are solver-specific | Sudoku variants or research tooling |
| Monte Carlo / sampling estimate | Estimated solution-space size or clue criticality | Can identify fragile clues and ambiguity trends | Non-deterministic unless seeded; expensive; not a direct difficulty rating | Puzzle-quality analysis |
| Machine-learned difficulty model | Predicted human solve time/error rate | Can match observed player behaviour | Needs quality training data and ongoing validation; less explainable | Adaptive consumer difficulty labels |

## Recommended path

Keep `RuleBasedGrader` as the correctness baseline: it gives a human-explainable band, powers hints, and a full solve proves uniqueness.

If more resolution is needed, add a weighted score to `RuleBasedGrader` before introducing a separate solver. It reuses existing candidate state, retains explainability, and can remain allocation-free. For much faster high-volume uniqueness checks, consider an exact-cover/DLX validator; use it as a validator alongside—not as a replacement for—the human-oriented grader.
