using Godot;

namespace Sudoku;

/// <summary>
/// Grows the Control on hover and dips it on press. Add as a child of any Control.
///
/// Owns the Control's <c>scale</c> — do not put two of these on the same widget.
/// </summary>
[Tool]
[GlobalClass]
public partial class ScaleTween : UiTweenBehaviour
{
	/// <summary>
	/// Multiplies how far this widget travels from its resting size, without changing the shared
	/// timings. 0 makes it inert, 2 makes a hero button pop twice as much as everything else.
	/// </summary>
	[Export(PropertyHint.Range, "0,3,0.05")] public float Strength { get; set; } = 1f;

	/// <summary>
	/// Scale from the middle. Turn off only if the widget already has a deliberate pivot set.
	/// </summary>
	[Export] public bool CenterPivot { get; set; } = true;

	protected override void OnHostReady()
	{
		if (CenterPivot)
		{
			KeepPivotCentered();
		}
	}

	protected override void ApplyState(InteractionState state, bool instant)
	{
		float scale = state switch
		{
			InteractionState.Hovered => Settings.HoverScale,
			InteractionState.Pressed => Settings.PressScale,
			_ => 1f,
		};

		// Interpolate away from 1 rather than multiplying, so Strength scales the *effect*.
		var target = Vector2.One * (1f + ((scale - 1f) * Strength));
		float duration = Settings.Duration(DurationFor(state));

		if (instant || duration <= 0f)
		{
			Host.Scale = target;
			return;
		}

		bool press = state == InteractionState.Pressed || PreviousState == InteractionState.Pressed;

		StartTween()
			.SetTrans(press ? Settings.PressTransition : Settings.HoverTransition)
			.SetEase(press ? Settings.PressEase : Settings.HoverEase)
			.TweenProperty(Host, ScaleProperty, target, duration);
	}

	private float DurationFor(InteractionState state) => state switch
	{
		InteractionState.Pressed => Settings.PressDownDuration,
		_ when PreviousState == InteractionState.Pressed => Settings.PressUpDuration,
		InteractionState.Hovered => Settings.HoverInDuration,
		_ => Settings.HoverOutDuration,
	};
}
