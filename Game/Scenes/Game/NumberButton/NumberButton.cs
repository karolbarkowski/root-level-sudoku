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
	public int Remaining { get; set; } = 9;
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
			_selectionTween.TweenMethod(Callable.From<float>(v => { _selection = v; QueueRedraw(); }), _selection, target, .22);
		}
	}
	private readonly StyleBoxFlat _plate = new() { CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 };
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

	public override void _ExitTree() { _feedback?.Kill(); _selectionTween?.Kill(); }

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
		Color foreground = PaperStyle.Ink.Lerp(PaperStyle.Paper, h);
		if (Disabled) foreground = foreground with { A = .3f };
		float inset = _depression * 2;
		float top = Size.Y * .22f * (1 - _selection);
		DrawSetTransform(new Vector2(0, top));
		var rect = new Rect2(new Vector2(inset, inset + _depression), Size - new Vector2(0,top) - (Vector2.One * inset * 2));

		_plate.BgColor = PaperStyle.Paper.Darkened(.06f).Lerp(PaperStyle.Burgundy, h);
		DrawStyleBox(_plate, rect);
		DrawRect(new Rect2(rect.Position + new Vector2(0,rect.Size.Y-3),new Vector2(rect.Size.X,3)), PaperStyle.Burgundy with { A = Disabled ? .15f : .65f });

		if (_number == 0)
		{
			DrawErase(rect.Position + (rect.Size / 2), foreground);
			return;
		}

		int fontSize = Mathf.RoundToInt(Mathf.Min(32, Size.X * .65f));
		string text = _number.ToString();
		float width = PaperStyle.Body.GetStringSize(text, fontSize: fontSize).X;
		var baseline = new Vector2((Size.X - width) / 2, 42 + _depression);
		DrawString(PaperStyle.Body, baseline, text, fontSize: fontSize, modulate: foreground);
		string count = $"×{Remaining}";
		float countWidth = PaperStyle.Body.GetStringSize(count, fontSize:13).X;
		DrawString(PaperStyle.Body, new Vector2((Size.X-countWidth)/2,65+_depression),count,fontSize:13,modulate:foreground);
	}

	private void DrawErase(Vector2 center, Color color)
	{
		float arm = Mathf.Min(Size.X, Size.Y) * .18f;
		DrawLine(center + new Vector2(-arm, -arm), center + new Vector2(arm, arm), color, 1.8f, true);
		DrawLine(center + new Vector2(arm, -arm), center + new Vector2(-arm, arm), color, 1.8f, true);
	}
}
