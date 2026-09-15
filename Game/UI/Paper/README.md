# Dark vector UI

The Paper scene and class names are kept from an earlier look. Screens use flat vector
surfaces and SVG icons.

## Palette
- Background: #272F33
- Raised controls: #41494D
- Accent: #F58220
- Text: #F4F5F5
- Muted text: #A4ADB1

PaperStyle.cs owns the shared palette; PaperTheme.tres supplies the bundled Alata font.
Anton remains the start-screen title font. Board colors are editable in
Resources/default_tile_textures.tres.

## Components
- PaperBackground.tscn: full-screen flat ColorRect.
- PaperHero.tscn: animated live title and subtitle.
- PaperMenu.tscn / PaperFooter.tscn: difficulty and navigation composition.
- PaperButton.tscn: reusable rounded text button, including the continue action.
- PaperIconButton.tscn: reusable animated back, undo, redo and notes icons.
- NumberButton.tscn: bottom-aligned digit bar with remaining count; selection animates
  height and orange fill over 220 ms. Disabled animation settings apply immediately.

The portrait game places navigation at the top and the number strip at the bottom,
with the board centered in between. Entry animations finish within one second.
GameSession retains the live puzzle, pencil marks and undo history across scene changes,
and writes it to user://session.json after every change so it survives the app being killed.

Every scene change goes through the SceneTransition autoload (UI/Transition). The
outgoing scene plays its exit while a cover fades in; the next scene loads on a worker
thread while a fresh puzzle is generated on a worker task, with the TransitionOverlay's (UI/Transition)
message and spinner on the cover. The new scene is swapped in under the cover, the board
builds its tiles over several frames, and the cover lifts as the game controls enter in a
short stagger. The previous scene is freed piecemeal afterwards. Scenes take part through
ITransitionScreen; Resume skips generation because it already has a Board.

## Verification
Build Game/sudoku.csproj, then run Godot with --path Game and one of:
- --script res://Tests/game_flow.gd
- --script res://Tests/paper_preview.gd
- --script res://Tests/paper_entry.gd
- --script res://Tests/persistence.gd -- --phase=play, then the same with --phase=resume
  (two runs: the first kills its own process, the second checks the game comes back)

Captures are written under output/. Checks cover portrait sizes, selection animation,
bottom alignment, remaining counts, generation/loading, menu/continue, notes, undo/redo, and
new-game/completion behavior. Physical Android touch and safe areas require device testing.
