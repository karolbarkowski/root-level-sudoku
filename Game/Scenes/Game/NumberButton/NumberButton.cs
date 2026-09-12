using Godot;
using Sudoku;

namespace SudokuEndless;

/// <summary>
/// A single digit (1-9) in the number bar, or the erase key (0, drawn as a cross).
///
/// The root is a real <see cref="Button"/>, so press semantics, focus and
/// <see cref="BaseButton.Disabled"/> come from the engine. Everything visible is drawn here in the
/// same printed idiom as <see cref="PaperButton"/> — a hairline rule that fills with ink as the key
/// is pressed — rather than assembled from Panel and Label children.
/// </summary>
[Tool]
public partial class NumberButton : Button
{
	/// <summary>
	/// Emitted when the button is activated. Carries the digit it represents; the board reads 0 as
	/// "clear the cell". Named apart from <see cref="BaseButton.Pressed"/>, which it rides on.
	/// </summary>
	[Signal]
	public delegate void NumberPressedEventHandler(int number);

	private int _number = 1;
	private float _highlight;
	private float _depression;
	private bool _hovered;
	private bool _touchInput;
	private Tween _feedback;

	/// <summary>The digit this button represents (1-9), or 0 for the erase key.</summary>
	[Export(PropertyHint.Range, "0,9")]
	public int Number
	{
		get => _number;
		set
		{
			_number = Mathf.Clamp(value, 0, 9);
			AccessibilityName = _number == 0 ? "Erase" : $"Enter {_number}";
			QueueRedraw();
		}
	}

	public override void _Ready()
	{
		MouseDefaultCursorShape = CursorShape.PointingHand;
		AccessibilityName = _number == 0 ? "Erase" : $"Enter {_number}";
		foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
		{
			AddThemeStyleboxOverride(state, new StyleBoxEmpty());
		}

		MouseEntered += () => { _hovered = true; Animate(); };
		MouseExited += () => { _hovered = false; Animate(); };
		FocusEntered += Animate;
		FocusExited += Animate;
		ButtonDown += Animate;
		ButtonUp += Animate;
		Resized += QueueRedraw;
		QueueRedraw();
	}

	// Touch leaves the pointer parked on the key it tapped, which would otherwise read as a
	// permanent hover. Same suppression PaperButton uses.
	public override void _Input(InputEvent input)
	{
		bool touch = _touchInput;
		if (input is InputEventScreenTouch) touch = true;
		else if (input is InputEventKey || input is InputEventMouseMotion && input.Device >= 0) touch = false;
		if (touch == _touchInput) return;
		_touchInput = touch;
		Animate();
	}

	public override void _Pressed() => EmitSignal(SignalName.NumberPressed, _number);

	public override void _ExitTree() => _feedback?.Kill();

	/// <summary>Disabled has no change signal, so the board calls this after flipping it.</summary>
	public void RefreshAvailability() => Animate();

	private void Animate()
	{
		_feedback?.Kill();
		float highlight = !Disabled && ((!_touchInput && (_hovered || HasFocus())) || IsPressed()) ? 1 : 0;
		float depression = !Disabled && IsPressed() ? 1 : 0;
		if (!UiAnimationSettings.Default.Enabled)
		{
			_highlight = highlight;
			_depression = depression;
			QueueRedraw();
			return;
		}

		_feedback = CreateTween().SetParallel().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		_feedback.TweenMethod(Callable.From<float>(v => { _highlight = v; QueueRedraw(); }), _highlight, highlight, .10);
		_feedback.TweenMethod(Callable.From<float>(v => { _depression = v; QueueRedraw(); }), _depression, depression, depression > 0 ? .06 : .10);
	}

	public override void _Draw()
	{
		float h = Disabled ? 0 : _highlight;
		Color foreground = PaperStyle.Ink.Lerp(PaperStyle.Paper, h);
		if (Disabled) foreground = foreground with { A = .3f };
		float inset = _depression * 2;
		var rect = new Rect2(new Vector2(inset, inset + _depression), Size - (Vector2.One * inset * 2));

		DrawRect(rect, PaperStyle.Ink with { A = h });
		PaperStyle.DrawBorder(this, rect, PaperStyle.Ink with { A = Disabled ? .25f : 1 });

		if (_number == 0)
		{
			DrawErase(rect.Position + (rect.Size / 2), foreground);
			return;
		}

		int fontSize = Mathf.RoundToInt(Size.Y * .52f);
		string text = _number.ToString();
		float width = PaperStyle.Display.GetStringSize(text, fontSize: fontSize).X;
		var baseline = new Vector2((Size.X - width) / 2, (Size.Y / 2) + (fontSize * .38f) + _depression);
		DrawString(PaperStyle.Display, baseline, text, fontSize: fontSize, modulate: foreground);
	}

	private void DrawErase(Vector2 center, Color color)
	{
		float arm = Mathf.Min(Size.X, Size.Y) * .18f;
		DrawLine(center + new Vector2(-arm, -arm), center + new Vector2(arm, arm), color, 1.8f, true);
		DrawLine(center + new Vector2(arm, -arm), center + new Vector2(-arm, arm), color, 1.8f, true);
	}
}
