using Godot;
using Sudoku;
namespace SudokuEndless;

/// <summary>Reusable vector icon action with native keyboard/touch button semantics.</summary>
[Tool]
public partial class PaperIconButton : Button
{
	public enum Glyph { Back, Pause, Play, Undo, Redo, Erase, Edit }
	[Export] public Glyph Symbol { get; set; }
	[Export] public Texture2D SvgIcon { get; set; }

	/// <summary>Paints <see cref="SvgIcon"/> in the accent colour instead of its own.</summary>
	[Export] public bool TintIcon { get; set; }
	[Export] public string Caption { get; set; } = "";
	[Export] public bool Accent { get; set; }

	/// <summary>Writes <see cref="Caption"/> under the icon; the circle shrinks to leave room.</summary>
	[Export] public bool ShowCaption { get; set; }

	private const float CaptionHeight = 24;
	private const int CaptionSize = 15;
	private float _feedback;
	private bool _hover;
	private bool _touch;
	private Tween _tween;
	public override void _Ready()
	{
		if (TintIcon) Material = PaperStyle.IconTint;
		foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
			AddThemeStyleboxOverride(state, new StyleBoxEmpty());
		AccessibilityName = Caption.Length > 0 ? Caption : Symbol.ToString();
		TooltipText = AccessibilityName;
		MouseDefaultCursorShape = CursorShape.PointingHand;
		MouseEntered += () => { _hover = true; RefreshFeedback(); };
		MouseExited += () => { _hover = false; RefreshFeedback(); };
		FocusEntered += RefreshFeedback;
		FocusExited += RefreshFeedback;
		ButtonDown += RefreshFeedback;
		ButtonUp += RefreshFeedback;
		Resized += QueueRedraw;
	}
	public override void _Input(InputEvent input)
	{
		if (input is InputEventScreenTouch) _touch = true;
		else if (input is InputEventKey || input is InputEventMouseMotion && input.Device >= 0) _touch = false;
	}
	public void RefreshFeedback()
	{
		_tween?.Kill();
		float target = !Disabled && (IsPressed() || !_touch && (_hover || HasFocus())) ? 1 : 0;
		if (Engine.IsEditorHint() || !UiAnimationSettings.Default.Enabled) { _feedback = target; QueueRedraw(); return; }
		_tween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		_tween.TweenMethod(Callable.From<float>(v => { _feedback = v; QueueRedraw(); }), _feedback, target, .12);
	}
	public override void _ExitTree() => _tween?.Kill();
	public override void _Draw()
	{
		bool active = Accent || ButtonPressed;
		float iconHeight = ShowCaption ? Size.Y - CaptionHeight : Size.Y;
		var center = new Vector2(Size.X / 2, iconHeight / 2 + _feedback * 2);
		float radius = Mathf.Min(Size.X, iconHeight) / 2 - 5;
		float glyphScale = ShowCaption ? 1.45f : 1.15f;
		Color fill = active ? PaperStyle.Burgundy : PaperStyle.Surface;
		fill = fill.Lerp(PaperStyle.Ink, _feedback * .22f);
		Color ink = PaperStyle.Ink;
		if (Disabled) { fill.A = .45f; ink.A = .3f; }
		DrawCircle(center, radius - _feedback, fill);
		if (HasFocus() && !_touch) DrawArc(center, radius + 3, 0, Mathf.Tau, 48, PaperStyle.Burgundy, 2, true);
		DrawSetTransform(center, 0, Vector2.One * glyphScale);
		void Line(Vector2 a, Vector2 b) => DrawLine(a, b, ink, 2.2f, true);
		if (SvgIcon != null) {
			Color icon = TintIcon ? PaperStyle.Burgundy with { A = ink.A } : ink;
			DrawTextureRect(SvgIcon, new Rect2(-14, -14, 28, 28), false, icon);
		} else if (Symbol == Glyph.Back) {
			Line(new(-10,0), new(11,0)); Line(new(-10,0),new(-2,-8)); Line(new(-10,0),new(-2,8));
		} else if (Symbol == Glyph.Pause) {
			Line(new(-5,-9),new(-5,9)); Line(new(5,-9),new(5,9));
		} else if (Symbol == Glyph.Play) {
			DrawColoredPolygon(new[] { new Vector2(-6,-10), new Vector2(10,0), new Vector2(-6,10) }, ink);
		} else if (Symbol == Glyph.Undo || Symbol == Glyph.Redo) {
			float d = Symbol == Glyph.Undo ? 1 : -1;
			DrawSetTransform(center, 0, new Vector2(d,1) * glyphScale);
			DrawArc(new Vector2(0,3), 10, -Mathf.Pi/2, Mathf.Pi/2, 24, ink, 2.2f, true);
			Line(new(0,-7),new(-11,-7));
			Line(new(-11,-7),new(-5,-13)); Line(new(-11,-7),new(-5,-1));
		} else if (Symbol == Glyph.Erase) {
			DrawPolyline(new[] { new Vector2(-12,3), new Vector2(1,-10), new Vector2(12,1), new Vector2(3,10), new Vector2(-5,10), new Vector2(-12,3) }, ink, 2.2f,true);
			Line(new(-5,-4),new(6,7)); Line(new(-5,10),new(14,10));
		} else {
			DrawPolyline(new[] { new Vector2(-10,11), new Vector2(-7,2), new Vector2(6,-11), new Vector2(12,-5), new Vector2(-1,8), new Vector2(-10,11) }, ink, 2.2f,true);
			Line(new(3,-8),new(9,-2));
		}
		DrawSetTransform(Vector2.Zero);
		if (ShowCaption && Caption.Length > 0)
		{
			Color caption = PaperStyle.Muted.Lerp(PaperStyle.Ink, _feedback);
			if (Disabled) caption.A = .35f;
			DrawString(PaperStyle.Body, new Vector2(0, Size.Y - 5), Caption, HorizontalAlignment.Center, Size.X, CaptionSize, caption);
		}
	}
}
