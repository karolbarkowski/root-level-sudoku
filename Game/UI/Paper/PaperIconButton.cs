using Godot;
using Sudoku;
namespace SudokuEndless;

/// <summary>Reusable round icon action with native keyboard/touch button semantics.</summary>
[Tool]
public partial class PaperIconButton : Button
{
	[Export] public Texture2D SvgIcon { get; set; }

	/// <summary>Paints <see cref="SvgIcon"/> in the accent colour instead of its own.</summary>
	[Export] public bool TintIcon { get; set; }
	[Export] public string Caption { get; set; } = "";
	[Export] public bool Accent { get; set; }

	/// <summary>Writes <see cref="Caption"/> under the icon; the circle shrinks to leave room.</summary>
	[Export] public bool ShowCaption { get; set; }
	[Export(PropertyHint.Range, "0.5,1,0.05")] public float VisualScale { get; set; } = 1;

	private const float CaptionHeight = 24;
	private const int CaptionSize = 15;
	private float _feedback;
	private bool _hover;
	private bool _touch;
	private Tween _tween;
	public override void _Ready()
	{
		Material = PaperStyle.IconTint;
		TextureFilter = TextureFilterEnum.LinearWithMipmaps;
		foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
			AddThemeStyleboxOverride(state, new StyleBoxEmpty());
		AccessibilityName = Caption;
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
		float iconHeight = ShowCaption ? Size.Y - CaptionHeight : Size.Y;
		var center = new Vector2(Size.X / 2, iconHeight / 2 + _feedback * 2);
		float radius = (Mathf.Min(Size.X, iconHeight) / 2 - 5) * VisualScale;
		float iconScale = (ShowCaption ? 1.45f : 1.15f) * VisualScale;
		Color fill = PaperStyle.Surface;
		if (ButtonPressed) fill = fill.Lerp(PaperStyle.Ink, .12f);
		fill = fill.Lerp(PaperStyle.Ink, _feedback * .22f);
		Color ink = PaperStyle.Ink;
		if (Disabled) { fill.A = .45f; ink.A = .3f; }
		DrawCircle(center, radius - _feedback, fill, true, -1, true);
		DrawSetTransform(center, 0, Vector2.One * iconScale);
		if (SvgIcon != null)
		{
			Color icon = ink;
			DrawTextureRect(SvgIcon, new Rect2(-14, -14, 28, 28), false, icon);
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
