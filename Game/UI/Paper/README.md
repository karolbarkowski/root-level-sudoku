# Paper start screen

Open Scenes/StartScreen/StartScreen.tscn and run the scene (F6).

The composition follows one 540 x 1220 vertical poster at supported portrait sizes.
The hero occupies approximately the upper half; hero and menu scale together,
so resizing cannot shrink only the title. The project is portrait-only, including
the Android orientation setting. A standalone window starts at 420 x 950 and
permits sizes down to 280 x 480. Godot editor embedded-game dock chrome has its
own minimum size; use a standalone window to test very narrow portrait widths.

## Reusable components

- PaperBackground.tscn: Resources/Paper/paper-background.png, full-window image.
- PaperHero.tscn: live header, title and subtitle over an independent Nine.tscn.
- Nine.tscn: Resources/Paper/nine-distressed.png. The image contains the entire
  numeral with top padding. Standard CanvasItemMaterial multiply blending lets
  the paper show through the white printing plate. There is no custom shader.
- PaperMenu.tscn: difficulty list, heading, hint and footer containers.
- PaperFooter.tscn: two reusable PaperButton instances.
- PaperButton.tscn: native Button; exported Caption, Index, Secondary and Symbol.
  Connect its normal Pressed signal. All buttons start neutral; keyboard focus,
  pointer hover and touch press retain their animated feedback.
- PaperStyle.cs / PaperTheme.tres: shared palette and typography. Anton is bundled
  with its OFL license and approximates the generated concept's lettering.
- StartScreen.cs: composition, enum-driven buttons and navigation only.

Entry is prepared before drawing, then starts after the first rendered frame so startup loading cannot consume the animation. The title and numeral settle from opposite directions over 280 ms. Buttons fade, rise 22 logical pixels, and scale from 96% over 210 ms with up to 68 ms stagger. The complete sequence finishes in 280 ms.
Hover/focus takes 100 ms; press takes 60 ms; release takes 100 ms.
UiAnimationSettings.Default.Enabled disables these animations. Entry and
interaction tweens do not write the same properties.

## Asset provenance

PNG assets were generated with the built-in image generation tool using the
approved concept as a reference. The full prompts are in Resources/Paper/prompts.md.
The numeral uses white-background multiply compositing because the generator's
transparent-background attempt produced an opaque checkerboard. No checkerboard
asset is used in the game. The former procedural shaders are removed.

## Verification

Build the C# project, then run:

    Godot_v4.7.1.exe --path Game --script res://Tests/paper_preview.gd

The graphical smoke check creates output/start-screen and saves previews at five
portrait sizes, including 280 x 640 and 746 x 1311. It checks neutral Easy focus, vertical
ordering, shared scaling, visible top of the numeral, image-based assets, layout
bounds, completed entry opacity, Settings and Back keyboard navigation, and a
mouse click on Easy producing an unfinished puzzle. Physical device touch and
notch/safe-area behavior still require testing on an exported mobile build.

To inspect the entry itself, run with --fixed-fps 60 --script res://Tests/paper_entry.gd. This checks the initial prepared frame, visible intermediate motion and stagger, final resting state, replay on re-entry, and disabled-motion behavior. Captured frames are saved under output/start-screen/entry.

