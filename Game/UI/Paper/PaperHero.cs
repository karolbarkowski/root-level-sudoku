using Godot;

namespace SudokuEndless;

/// <summary>
/// The poster's top half: a drawn text layer (masthead, title, subtitle) plus the separate
/// multiply-blended numeral child.
///
/// The two are on independent entrance timelines — the numeral slides in from the right, the title
/// from the left — so each has its own progress value and its own tween rather than one shared one.
/// </summary>
[Tool]
public partial class PaperHero : Control
{
	// Far enough to clear the screen edge, in the 480-wide design space the poster insets 32 into:
	// the numeral rests at x=171, the title spans roughly 0..282.
	private const float NineTravel = 350;
	private const float TitleTravel = 340;

	private float _nineProgress = 1;
	private float _titleProgress = 1;
	private Tween _nineEntrance;
	private Tween _titleEntrance;

	private Control Nine => GetNode<Control>("Nine");

	/// <summary>Parks both layers off-screen. Call once before the screen is first drawn.</summary>
	public void PrepareEntrance()
	{
		_nineEntrance?.Kill();
		_titleEntrance?.Kill();
		_nineProgress = 0;
		_titleProgress = 0;
		// Near-zero alpha rather than hidden, so the layer still draws and its fonts are uploaded
		// before the first animated frame. self_modulate covers only this node's own drawing and
		// leaves the numeral child alone, which is what puts the two on separate timelines.
		SelfModulate = new Color(1, 1, 1, .001f);
		Reflow();
	}

	/// <summary>Slides the numeral in from beyond the right edge.</summary>
	public void PlayNine(double delay, double duration)
	{
		_nineEntrance?.Kill();
		Control nine = Nine;
		// Multiply blending ignores modulate alpha, so this one is hidden outright until its turn —
		// faded it would still draw at full strength in the margin of a letterboxed window.
		nine.Visible = false;
		_nineEntrance = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		_nineEntrance.TweenCallback(Callable.From(() => nine.Visible = true)).SetDelay(delay);
		_nineEntrance.TweenMethod(Callable.From<float>(progress =>
		{
			_nineProgress = progress;
			Reflow();
		}), 0f, 1f, duration).SetDelay(delay);
	}

	/// <summary>Slides the title in from beyond the left edge, bringing the text layer with it.</summary>
	public void PlayTitle(double delay, double duration)
	{
		_titleEntrance?.Kill();
		_titleEntrance = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		// The masthead and subtitle hold their place and simply arrive with the title.
		_titleEntrance.TweenProperty(this, "self_modulate:a", 1f, duration * .4).SetDelay(delay);
		_titleEntrance.TweenMethod(Callable.From<float>(progress =>
		{
			_titleProgress = progress;
			QueueRedraw();
		}), 0f, 1f, duration).SetDelay(delay);
	}

	public override void _ExitTree()
	{
		_nineEntrance?.Kill();
		_titleEntrance?.Kill();
	}

	public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Resized += Reflow; Reflow(); }
	private void Reflow()
	{
		float scale = Mathf.Min(Size.X / 480, Size.Y / 600);
		var nine = GetNode<Control>("Nine");
		nine.Position = new Vector2((Size.X - 480 * scale) / 2, 0)
			+ new Vector2(171 + NineTravel * (1 - _nineProgress), 0) * scale;
		nine.Size = new Vector2(342, 590) * scale;
		QueueRedraw();
	}
	public override void _Draw()
	{
		float scale = Mathf.Min(Size.X / 480, Size.Y / 600);
		var origin = new Vector2((Size.X - 480 * scale) / 2, 0);
		var font = PaperStyle.Display;
		// Foreground lettering has a paper knockout where it crosses the separate numeral layer.
		var titleOrigin = origin - new Vector2(TitleTravel * (1 - _titleProgress) * scale, 0);
		DrawSetTransform(titleOrigin, 0, new Vector2(scale * .78f, scale));
		foreach (var line in new[] { ("Sudoku", 295f), ("Endless", 432f) })
		{
			DrawStringOutline(font, new Vector2(0, line.Item2), line.Item1, fontSize: 145, size: 14, modulate: PaperStyle.Paper);
			DrawString(font, new Vector2(0, line.Item2), line.Item1, fontSize: 145, modulate: PaperStyle.Ink);
		}
		DrawSetTransform(origin, 0, Vector2.One * scale);
		DrawStringOutline(PaperStyle.Body, new Vector2(0, 466), "A daily moment of focus.", fontSize: 19, size: 5, modulate: PaperStyle.Paper);
		DrawString(PaperStyle.Body, new Vector2(0, 466), "A daily moment of focus.", fontSize: 19, modulate: PaperStyle.Ink);
	}
}
