# UI

Reusable presentation helpers for the game's interface.

## Tweens

Drop-in animation behaviours. Each one is a plain `Node` that you add as a **child** of the `Control`
it animates:

1. Select the Control in the scene tree.
2. `Ctrl+A` → type `ScaleTween` (they are `[GlobalClass]`, so they show up by name).
3. Done — no wiring, no exported paths.

For the common "button that reacts to hover and press", drag **`ButtonFeedback.tscn`** from the
FileSystem dock onto the Control instead: it is a prepacked `ScaleTween` + `ModulateTween` pair.

They are children rather than scripts attached to the Control itself because a node can only carry
one script — a `Button` with `ScaleTween.cs` attached would stop being a `Button`. As children they
stack freely, and each behaviour owns exactly one property so they never fight:

| Behaviour       | Animates                    | Reacts to                       |
| --------------- | --------------------------- | ------------------------------- |
| `ScaleTween`    | `scale`                     | hover, press                    |
| `ModulateTween` | `modulate` / `self_modulate`| hover, press, disabled          |
| `AppearTween`   | `scale` + `modulate` (once) | entering the tree               |

Everything works on both mouse and touch, and on any `Control` — `BaseButton` presses come from its
`button_down`/`button_up` signals, anything else is read from `gui_input` with a global watch for the
release so dragging off a widget cannot leave it stuck down.

### Per-node knobs

Behaviours are meant to be used with their defaults. The exports that exist are deliberately about
*placement*, not *look*:

- `Target` — animate a Control other than the nearest ancestor.
- `ReactToHover` / `ReactToPress` — mute one channel.
- `ScaleTween.Strength` — multiplier on how far this widget travels (`2` = a hero button that pops
  twice as hard), while still following the shared timings.
- `AppearTween.StaggerIndex` — increase it across a row to make the entrance cascade.
- `SettingsOverride` — point one widget at a different settings resource entirely.

`BaseButton.Disabled` has no change signal, so behaviours poll it (one bool compare per frame) and
animate the disabled tint on their own. Controls with a *custom* notion of being unavailable should
call `Refresh()` on the behaviour after changing it.

A worked example of the whole pattern — `Button` root, visuals as `mouse_filter = Ignore` children,
behaviours baked in — is [`NumberButton.tscn`](../Scenes/Game/NumberButton/NumberButton.tscn).

## Where the numbers live

Every duration, scale, colour and easing curve is in
[`res://Resources/UiAnimationDefault.tres`](../Resources/UiAnimationDefault.tres)
(`UiAnimationSettings`). Edit that one file to retune the whole game's UI feel — including
`Enabled` (kills all tweening) and `SpeedScale` (a global multiplier, handy for demoing at 0.3).
