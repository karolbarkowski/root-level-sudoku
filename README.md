# Sudoku Endless

An endless Sudoku game for mobile, built with Godot 4.7 (C#). Every puzzle is generated on the
device at the chosen difficulty, and a generative ambient soundtrack plays underneath.

## Project structure

```
Generators/               .NET library: board model, puzzle generator, difficulty graders
Generators.Tests.Unit/    Unit tests for the board, history, hints and validation
Generators.Benchmarks/    BenchmarkDotNet benchmarks for generation speed
Generators.Console/       Console app that generates a puzzle and steps through its solution
Game/                     Godot project (references Generators via ProjectReference)
  Scenes/StartScreen/     Main scene: difficulty choice, continue / new game
  Scenes/Game/            Gameplay: Board, Tile, NumberButton, SolvedPanel, Main
  UI/Paper/               Shared UI kit (buttons, menus, dialogs, theme, palette)
  UI/Transition/          SceneTransition autoload: animated scene changes, runs generation
  UtilsScripts/           GameSession: live puzzle, undo history, notes, save/load
  Music/                  Generative soundtrack (MusicDirector, Composer, MusicLibrary, settings)
  Audio/                  GameSounds autoload: UI and gameplay sound effects
  Resources/              Audio, fonts, icons, tile textures, animation settings
  Tests/                  Headless GDScript tests (game flow, persistence, music, ...)
```

Autoloads (see `Game/project.godot`): `GameSounds`, `SceneTransition`, `Music`.

`GameSession` holds the current puzzle in memory across scenes and writes it to
`user://session.json` after every change, so a killed mobile app resumes where it left off.
Preferences live in `user://settings.cfg` (`MusicSettings`).

## Level generation

Generation lives in `Generators/Sudoku/SudokuGenerator.cs` and has two stages. The game calls it
on a background thread during the "Generating puzzle" transition (`SceneTransition.StartNewGame`).

### 1. A full solution by simulated annealing

1. Each 3x3 box is filled with a random permutation of 1–9, so box constraints hold from the start.
2. The cost is the number of duplicate digits across all rows and columns.
3. Each step swaps two cells inside one random box. Improving swaps are always accepted; worse ones
   are accepted with probability `exp(-delta / T)`.
4. The temperature starts at 0.5 and is multiplied by 0.99 every 1000 steps. When the cost reaches
   0 the grid is a valid solution; if it cools to the floor first, the board is re-randomized and
   the search restarts.

The hot loop is allocation-free: per-row/column digit counts are updated incrementally for each
proposed swap, and acceptance probabilities are precomputed per temperature.

### 2. Removing clues down to a difficulty

Cells are visited in random order. Each is emptied, the puzzle is graded, and the digit is put
back if the grade is now harder than the target. The result is the sparsest puzzle this pass
finds that still grades at or below the target difficulty.

| Difficulty | Techniques required |
|---|---|
| Easy | Naked / hidden singles |
| Medium | + locked candidates |
| Hard | + naked / hidden pairs and triples |
| Expert | + X-Wing, XY-Wing |

`RuleBasedGrader` solves the puzzle like a human would, using the cheapest technique that makes
progress, and reports the hardest one it needed. A full solve means every placement was forced,
which also proves the solution is unique. A puzzle it can't finish grades as `Invalid` and is
never produced. The same grader powers in-game hints (`TryFindNextMove`).

More detail: `Generators/Sudoku/Graders/*.md`.

## Music system

The soundtrack is generated live rather than played from a track. It lives in `Game/Music/` and
runs as the `Music` autoload, so it continues across scene changes.

- **`MusicLibrary`** lists every sound: single-note instruments (pad, keys, mallet, chime), a
  sampled instrument folder (`Sounds/HollowTree/`, files named after notes such as `F#3.wav`), a C
  drone, and *tempo sets* (Noire at 67 BPM, Daylight at 82 BPM) with atmosphere loops. Each entry
  has a loudness trim so swapping files doesn't change the mix.
- **`Composer`** is pure C# with no Godot types. It writes one bar at a time in C Lydian and
  changes chord every 8 bars (Cmaj9, D/C, Em7, Cmaj7#11). In each bar it places pad chords,
  scale runs on the sampled instrument, short pentatonic keys phrases (a weighted random walk),
  sparse mallet chord tones, and occasional high chimes. Timing is slightly humanized, and there
  is lots of rest.
- **`MusicDirector`** plays it:
  - Loads all streams on a background thread, then picks a random tempo set and seed.
  - Starts all loops in the same frame and never stops them. Sections come and go by fading
    volume (atmospheres cross-fade every 32 bars; optional brush loops come and go in 8/12/16-bar
    sections), so timed loops stay in sync.
  - Uses the **drone loop's playback position as the clock**. Each bar is composed half a beat
    ahead, and notes are triggered when the clock reaches them, so notes can't drift from the
    audio. Notes more than a quarter-beat late (after a stall) are dropped instead of bunching up.
  - Plays each note from the nearest recorded sample, pitch-shifted, through polyphonic players
    on left, centre and right buses.
  - Pauses when the app is backgrounded.

Audio buses (`Game/default_bus_layout.tres`):

```
Master
├── Music          hard limiter; mute and volume come from settings
│   ├── MusicNotes     dotted-eighth delay (synced to BPM) + reverb
│   │   ├── MusicNotesL / MusicNotesR   panned note players
│   └── MusicBeds      low-pass; drone, atmospheres, brushes
└── GameSounds     UI/gameplay effects, controlled separately
```

Missing audio files are logged and skipped, so the music keeps playing while sounds are being
swapped. Loop files must be imported with Loop Mode = Forward.

## Running tests

```bash
dotnet test Generators.Tests.Unit
```

```bash
Godot --path Game --headless --script res://Tests/music.gd
```

Each script in `Game/Tests/` lists how to run it in its header comment.
