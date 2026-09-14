using Godot;
namespace SudokuEndless;
[Tool]
public partial class PaperHero : Control
{
	private float _progress = 1;
	private Tween _entry;
	public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }
	public void PrepareEntrance() { _entry?.Kill(); _progress = 0; Modulate = new Color(1,1,1,.001f); QueueRedraw(); }
	public void PlayTitle(double delay, double duration)
	{
		_entry = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		_entry.TweenProperty(this, "modulate:a", 1f, duration).SetDelay(delay);
		_entry.TweenMethod(Callable.From<float>(v => { _progress = v; QueueRedraw(); }), 0f, 1f, duration).SetDelay(delay);
	}
	public override void _ExitTree() => _entry?.Kill();
	public override void _Draw()
	{
		float scale = Mathf.Min(Size.X / 476, Size.Y / 320);
		DrawSetTransform(new Vector2(-24 * (1-_progress), 0), 0, Vector2.One * scale);
		DrawString(PaperStyle.Body, new Vector2(0,20), "A DAILY MOMENT OF FOCUS", fontSize:14, modulate:PaperStyle.Burgundy);
		DrawString(PaperStyle.Display, new Vector2(-3,150), "Sudoku", fontSize:132, modulate:PaperStyle.Ink);
		DrawString(PaperStyle.Display, new Vector2(-3,280), "Endless", fontSize:132, modulate:PaperStyle.Ink);
		DrawLine(new Vector2(0,313), new Vector2(58,313), PaperStyle.Burgundy, 4);
	}
}
