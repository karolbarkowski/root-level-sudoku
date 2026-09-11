using System;
using Godot;

namespace Sudoku;

/// <summary>
/// Base class for the drop-in UI animation behaviours in this folder.
///
/// A behaviour is a plain <see cref="Node"/> that you add as a child of the <see cref="Control"/> it
/// animates — that is what makes them stackable: a script attached directly to the Control would
/// have to replace the Control's own script, and only one of those can exist at a time. As children
/// you can combine as many as you like (scale + tint + entrance) on the same widget.
///
/// It does the boring half of the job: finding the Control, translating mouse/touch/button signals
/// into one of four <see cref="InteractionState"/>s, and keeping a single tween per behaviour so
/// interrupted animations never fight each other. Subclasses only implement
/// <see cref="ApplyState"/> — "given this state, what should my property become".
///
/// Each subclass owns exactly one property (scale, modulate, ...) so two behaviours on the same
/// Control never write the same value.
///
/// All timings and magnitudes come from <see cref="UiAnimationSettings"/>, not from here.
/// </summary>
[Tool]
public partial class UiTweenBehaviour : Node
{
	/// <summary>What the widget is currently doing, as far as the animations are concerned.</summary>
	public enum InteractionState
	{
		Normal,
		Hovered,
		Pressed,
		Disabled,
	}

	protected const string ScaleProperty = "scale";
	protected const string ModulateProperty = "modulate";
	protected const string SelfModulateProperty = "self_modulate";

	/// <summary>
	/// The Control to animate. Leave empty to use the nearest Control ancestor, which is the normal
	/// case — set it only when the behaviour lives somewhere else in the tree.
	/// </summary>
	[Export] public Control Target { get; set; }

	/// <summary>
	/// Per-widget escape hatch from the project-wide <see cref="UiAnimationSettings.Default"/>.
	/// Leave empty unless this one widget genuinely needs to move differently from everything else.
	/// </summary>
	[Export] public UiAnimationSettings SettingsOverride { get; set; }

	[Export] public bool ReactToHover { get; set; } = true;
	[Export] public bool ReactToPress { get; set; } = true;

	/// <summary>The resolved Control being animated. Null if none was found (a warning is logged).</summary>
	protected Control Host { get; private set; }

	protected InteractionState State { get; private set; } = InteractionState.Normal;

	/// <summary>The state we came from. Lets subclasses pick, say, a release duration over a hover-out one.</summary>
	protected InteractionState PreviousState { get; private set; } = InteractionState.Normal;

	protected UiAnimationSettings Settings => SettingsOverride ?? UiAnimationSettings.Default;

	private Tween _tween;
	private bool _hovered;
	private bool _pressed;
	private bool _pivotTracked;
	private bool _wasDisabled;

	public override void _Ready()
	{
		SetProcess(false);
		SetProcessInput(false);
		Host = ResolveHost();

		if (Engine.IsEditorHint())
		{
			UpdateConfigurationWarnings();
			return;
		}

		if (Host == null)
		{
			GD.PushWarning($"{GetType().Name} at '{GetPath()}' found no Control to animate. " +
				"Make it a child of a Control, or assign its Target.");
			return;
		}

		Host.MouseEntered += OnMouseEntered;
		Host.MouseExited += OnMouseExited;

		if (Host is BaseButton button)
		{
			// BaseButton already tracks the press for us, including releases that land outside it.
			button.ButtonDown += OnPressStarted;
			button.ButtonUp += OnPressEnded;

			// Disabled has no change signal, so watch it. One bool compare per frame is cheaper than
			// making every caller remember to poke us after flipping it.
			_wasDisabled = button.Disabled;
			SetProcess(true);
		}
		else
		{
			Host.GuiInput += OnHostGuiInput;
		}

		OnHostReady();

		// Only touch the widget up front if it does not already look the way it should — otherwise a
		// behaviour that runs first would clobber the starting values another one (AppearTween) set.
		if (ComputeState() != InteractionState.Normal)
		{
			Refresh(instant: true);
		}
	}

	public override void _ExitTree()
	{
		if (Engine.IsEditorHint() || Host == null)
		{
			return;
		}

		Host.MouseEntered -= OnMouseEntered;
		Host.MouseExited -= OnMouseExited;

		if (Host is BaseButton button)
		{
			button.ButtonDown -= OnPressStarted;
			button.ButtonUp -= OnPressEnded;
		}
		else
		{
			Host.GuiInput -= OnHostGuiInput;
		}

		if (_pivotTracked)
		{
			Host.Resized -= CenterPivot;
			_pivotTracked = false;
		}

		_tween?.Kill();
		_tween = null;
	}

	public override void _Notification(int what)
	{
		if (what is (int)NotificationParented or (int)NotificationUnparented)
		{
			UpdateConfigurationWarnings();
		}
	}

	public override string[] _GetConfigurationWarnings() =>
		ResolveHost() == null
			? new[] { "No Control to animate. Make this node a child of a Control, or set its Target." }
			: Array.Empty<string>();

	public override void _Process(double delta)
	{
		if (Host is not BaseButton button || button.Disabled == _wasDisabled)
		{
			return;
		}

		_wasDisabled = button.Disabled;
		Refresh();
	}

	/// <summary>
	/// Re-evaluates the state and animates towards it. <see cref="BaseButton"/> hosts are watched
	/// automatically; this is for Controls that carry their own notion of being unavailable.
	/// </summary>
	public void Refresh(bool instant = false)
	{
		if (Host == null)
		{
			return;
		}

		InteractionState next = ComputeState();
		if (next == State && !instant)
		{
			return;
		}

		PreviousState = State;
		State = next;
		ApplyState(next, instant || !Settings.Enabled);
	}

	/// <summary>Runs once the Host is resolved and wired, before the first state is applied.</summary>
	protected virtual void OnHostReady()
	{
	}

	/// <summary>
	/// Move this behaviour's property to whatever <paramref name="state"/> should look like.
	/// When <paramref name="instant"/> is true, assign it directly instead of tweening.
	/// </summary>
	protected virtual void ApplyState(InteractionState state, bool instant)
	{
	}

	/// <summary>
	/// Starts a fresh tween, cancelling this behaviour's previous one so a fast hover-in/hover-out
	/// never leaves two tweens writing the same property. Runs while the tree is paused, so menus
	/// stay animated over a paused game.
	/// </summary>
	protected Tween StartTween()
	{
		_tween?.Kill();
		_tween = Host.CreateTween();
		_tween.SetPauseMode(Tween.TweenPauseMode.Process);
		return _tween;
	}

	/// <summary>
	/// Pins the Control's pivot to its centre and keeps it there through resizes, so scaling and
	/// rotation grow from the middle instead of the top-left corner.
	/// </summary>
	protected void KeepPivotCentered()
	{
		if (Host == null || _pivotTracked)
		{
			return;
		}

		CenterPivot();
		Host.Resized += CenterPivot;
		_pivotTracked = true;
	}

	private void CenterPivot() => Host.PivotOffset = Host.Size * 0.5f;

	private InteractionState ComputeState()
	{
		if (Host is BaseButton { Disabled: true })
		{
			return InteractionState.Disabled;
		}

		if (_pressed && ReactToPress)
		{
			return InteractionState.Pressed;
		}

		return _hovered && ReactToHover ? InteractionState.Hovered : InteractionState.Normal;
	}

	private Control ResolveHost()
	{
		if (Target != null)
		{
			return Target;
		}

		// Walk up rather than checking only the parent, so behaviours can be grouped under a plain
		// Node (see ButtonFeedback.tscn) and still find the widget.
		for (Node node = GetParent(); node != null; node = node.GetParent())
		{
			if (node is Control control)
			{
				return control;
			}
		}

		return null;
	}

	private void OnMouseEntered()
	{
		_hovered = true;
		Refresh();
	}

	private void OnMouseExited()
	{
		_hovered = false;
		Refresh();
	}

	private void OnHostGuiInput(InputEvent @event)
	{
		bool down =
			@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } ||
			@event is InputEventScreenTouch { Pressed: true };

		if (down)
		{
			OnPressStarted();
		}
	}

	private void OnPressStarted()
	{
		_pressed = true;

		// A plain Control only sees events inside its own rect, so watch globally for the release —
		// otherwise dragging off the widget would leave it stuck in the pressed look.
		SetProcessInput(true);
		Refresh();
	}

	private void OnPressEnded()
	{
		_pressed = false;
		SetProcessInput(false);
		Refresh();
	}

	public override void _Input(InputEvent @event)
	{
		if (!_pressed)
		{
			return;
		}

		bool up =
			@event is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } ||
			@event is InputEventScreenTouch { Pressed: false };

		if (up)
		{
			OnPressEnded();
		}
	}
}
