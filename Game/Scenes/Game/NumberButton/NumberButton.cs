using Godot;
using Sudoku;

namespace SudokuEndless;

/// <summary>
/// A single digit (1-9) in the number bar.
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
	private int _remaining = 9;
	private bool _countInitialized;
	private readonly float[] _boxes = { 1, 1, 1, 1, 1, 1, 1, 1, 1 };
	private Tween _stackTween;
	public int Remaining
	{
		get => _remaining;
		set
		{
			int next = Mathf.Clamp(value, 0, 9);
			if (_countInitialized && next == _remaining) return;
			_stackTween?.Kill();
			bool animate = _countInitialized && IsInsideTree() && !Engine.IsEditorHint() && UiAnimationSettings.Default.Enabled;
			_countInitialized = true;
			_remaining = next;
			AccessibilityName = $"Enter {_number}, {_remaining} remaining";
			if (animate)
				_stackTween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			for (int i = 0; i < _boxes.Length; i++)
			{
				int slot = i;
				float target = i < next ? 1 : 0;
				if (animate)
					_stackTween.TweenMethod(Callable.From<float>(v => { _boxes[slot] = v; QueueRedraw(); }), _boxes[i], target, .14);
				else _boxes[i] = target;
			}
			QueueRedraw();
		}
	}
	private bool _selected;
	private float _selection;
	private Tween _selectionTween;
	public float SelectionProgress => _selection;
	public bool Selected
	{
		get => _selected;
		set
		{
			if (_selected == value) return;
			_selected = value;
			_selectionTween?.Kill();
			float target = value ? 1 : 0;
			if (!IsInsideTree() || Engine.IsEditorHint() || !UiAnimationSettings.Default.Enabled)
			{
				_selection = target;
				QueueRedraw();
				return;
			}
			_selectionTween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			_selectionTween.TweenMethod(Callable.From<float>(v => { _selection = v; QueueRedraw(); }), _selection, target, .12);
		}
	}
	/// <summary>
	/// Visual space between neighbouring keys. The bar itself has no separation, so the keys' tap
	/// areas meet and a near-miss still lands on a key instead of the empty space behind them.
	/// </summary>
	private const float PlateGap = 4;

	private readonly StyleBoxFlat _plate = new() { CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 };
	private readonly StyleBoxFlat _box = new() { CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 };
	private float _highlight;
	private float _depression;
	private bool _hovered;
	private bool _touchInput;
	private Tween _feedback;

	/// <summary>The digit this button represents (1-9).</summary>
	[Export(PropertyHint.Range, "1,9")]
	public int Number
	{
		get => _number;
		set
		{
			_number = Mathf.Clamp(value, 1, 9);
			AccessibilityName = $"Enter {_number}";
			QueueRedraw();
		}
	}

	public override void _Ready()
	{
		MouseDefaultCursorShape = CursorShape.PointingHand;
		AccessibilityName = $"Enter {_number}";
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

	public override void _ExitTree() { _feedback?.Kill(); _selectionTween?.Kill(); _stackTween?.Kill(); }

	/// <summary>Disabled has no change signal, so the board calls this after flipping it.</summary>
	public void RefreshAvailability()
	{
		AccessibilityName = $"Enter {_number}, {Remaining} remaining";
		Animate();
	}

	private void Animate()
	{
		_feedback?.Kill();
		float highlight = !Disabled && ((!_touchInput && (_hovered || HasFocus())) || IsPressed()) ? 1 : 0;
		float depression = !Disabled && IsPressed() ? 1 : 0;
		if (Engine.IsEditorHint() || !UiAnimationSettings.Default.Enabled)
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
		float h = Disabled ? 0 : Mathf.Lerp(_selection, 1, _highlight * .12f);
		Color foreground = PaperStyle.Ink;
		if (Disabled) foreground = foreground with { A = .3f };
		float inset = _depression * 2;
		float top = Size.Y * .12f * (1 - _selection);
		DrawSetTransform(new Vector2(0, top));
		var rect = new Rect2(new Vector2(inset + PlateGap / 2, inset + _depression), Size - new Vector2(PlateGap, top) - (Vector2.One * inset * 2));

		_plate.BgColor = PaperStyle.Surface.Lerp(PaperStyle.Burgundy, h);
		DrawStyleBox(_plate, rect);

		int fontSize = Mathf.RoundToInt(Mathf.Min(32, Size.X * .65f));
		string text = _number.ToString();
		float width = PaperStyle.Body.GetStringSize(text, fontSize: fontSize).X;
		var baseline = new Vector2((Size.X - width) / 2, 42 + _depression);
		DrawString(PaperStyle.Body, baseline, text, fontSize: fontSize, modulate: foreground);
		// Reserve room for all nine boxes even when the unselected plate is shorter.
		// Fixed slots keep the stack anchored to the bottom as individual boxes disappear.
		float bottom = rect.End.Y - 6;
		float available = Mathf.Max(0, bottom - (baseline.Y + 12));
		float gap = Mathf.Min(3, available / 26);
		float boxHeight = Mathf.Min(16, Mathf.Max(0, (available - gap * 8) / 9));
		float boxWidth = Mathf.Max(0, rect.Size.X - 10);
		for (int i = 0; i < _boxes.Length; i++)
		{
			float progress = _boxes[i];
			if (progress <= .001f || boxHeight <= 0) continue;
			float scale = Mathf.Lerp(.75f, 1, progress);
			var size = new Vector2(boxWidth * scale, boxHeight * progress);
			var position = new Vector2(rect.Position.X + rect.Size.X / 2 - size.X / 2,
				bottom - i * (boxHeight + gap) - size.Y);
			_box.BgColor = PaperStyle.Ink with { A = progress * (Disabled ? .12f : .28f) };
			DrawStyleBox(_box, new Rect2(position, size));
		}
		DrawSetTransform(Vector2.Zero);
	}
}
