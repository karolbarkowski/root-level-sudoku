using Godot;

namespace Sudoku;

/// <summary>
/// Plays a one-shot entrance: the Control scales up from <see cref="UiAnimationSettings.AppearFromScale"/>
/// and optionally fades in. Add as a child of any Control.
///
/// Set an increasing <see cref="StaggerIndex"/> on a row or column of widgets to make them cascade
/// in rather than all landing at once.
///
/// This writes both <c>scale</c> and <c>modulate</c>, but only until the entrance finishes — it ends
/// on exactly the resting values a <see cref="ScaleTween"/> or <see cref="ModulateTween"/> expects,
/// so the two compose. Keep it last among the behaviour children of a widget.
/// </summary>
[Tool]
[GlobalClass]
public partial class AppearTween : UiTweenBehaviour
{
	/// <summary>Play automatically when the widget enters the tree. Turn off to trigger it from code.</summary>
	[Export] public bool PlayOnReady { get; set; } = true;

	/// <summary>Position in a cascade. Each step adds <see cref="UiAnimationSettings.AppearStagger"/> of delay.</summary>
	[Export(PropertyHint.Range, "0,32,1")] public int StaggerIndex { get; set; }

	/// <summary>Extra delay for this widget alone, on top of the stagger.</summary>
	[Export(PropertyHint.Range, "0,2,0.01")] public float ExtraDelay { get; set; }

	public AppearTween()
	{
		// An entrance is not an interaction — leave hover and press to the other behaviours.
		ReactToHover = false;
		ReactToPress = false;
	}

	protected override void OnHostReady()
	{
		KeepPivotCentered();

		if (PlayOnReady)
		{
			Play();
		}
	}

	/// <summary>Runs the entrance from the top. Safe to call again to replay it.</summary>
	public void Play()
	{
		if (Host == null)
		{
			return;
		}

		// The widget's authored colour is the destination, so an already-tinted Control stays tinted.
		Color resting = Host.Modulate;
		float duration = Settings.Duration(Settings.AppearDuration);
		float delay = Settings.Duration((Settings.AppearStagger * StaggerIndex) + ExtraDelay);

		if (!Settings.Enabled || duration <= 0f)
		{
			Host.Scale = Vector2.One;
			Host.Modulate = resting;
			return;
		}

		Host.Scale = Vector2.One * Settings.AppearFromScale;
		if (Settings.AppearFade)
		{
			Host.Modulate = new Color(resting.R, resting.G, resting.B, 0f);
		}

		Tween tween = StartTween();
		tween.SetParallel(true).SetTrans(Settings.AppearTransition).SetEase(Settings.AppearEase);
		tween.TweenProperty(Host, ScaleProperty, Vector2.One, duration).SetDelay(delay);

		if (Settings.AppearFade)
		{
			// Linear alpha: an overshooting curve on a colour would blow past full opacity.
			tween.TweenProperty(Host, ModulateProperty, resting, duration)
				.SetDelay(delay)
				.SetTrans(Tween.TransitionType.Sine);
		}
	}
}
