using Godot;

namespace Sudoku;

/// <summary>
/// Tints the Control as it is hovered, pressed and disabled. Add as a child of any Control.
///
/// Owns the Control's <c>modulate</c> (or <c>self_modulate</c>), so it will overwrite any tint the
/// widget's own script sets on that property — a Control that already manages its own modulate
/// should either use <see cref="UseSelfModulate"/> to stay out of the way, or skip this behaviour.
/// </summary>
[Tool]
[GlobalClass]
public partial class ModulateTween : UiTweenBehaviour
{
	/// <summary>
	/// Tint only this Control and not its children. Useful when a button's label should keep its own
	/// colour while the background reacts.
	/// </summary>
	[Export] public bool UseSelfModulate { get; set; }

	protected override void ApplyState(InteractionState state, bool instant)
	{
		Color target = state switch
		{
			InteractionState.Hovered => Settings.HoverModulate,
			InteractionState.Pressed => Settings.PressModulate,
			InteractionState.Disabled => Settings.DisabledModulate,
			_ => Settings.NormalModulate,
		};

		float duration = Settings.Duration(DurationFor(state));

		if (instant || duration <= 0f)
		{
			Set(target);
			return;
		}

		bool press = state == InteractionState.Pressed || PreviousState == InteractionState.Pressed;

		StartTween()
			.SetTrans(press ? Settings.PressTransition : Settings.HoverTransition)
			.SetEase(press ? Settings.PressEase : Settings.HoverEase)
			.TweenProperty(Host, UseSelfModulate ? SelfModulateProperty : ModulateProperty, target, duration);
	}

	private void Set(Color color)
	{
		if (UseSelfModulate)
		{
			Host.SelfModulate = color;
		}
		else
		{
			Host.Modulate = color;
		}
	}

	private float DurationFor(InteractionState state) => state switch
	{
		InteractionState.Disabled => Settings.DisabledDuration,
		_ when PreviousState == InteractionState.Disabled => Settings.DisabledDuration,
		InteractionState.Pressed => Settings.PressDownDuration,
		_ when PreviousState == InteractionState.Pressed => Settings.PressUpDuration,
		InteractionState.Hovered => Settings.HoverInDuration,
		_ => Settings.HoverOutDuration,
	};
}
