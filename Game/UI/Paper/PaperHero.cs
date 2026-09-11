using Godot;

namespace SudokuEndless;

[Tool]
public partial class PaperHero : Control
{
    private float _entranceProgress = 1;
    private Tween _entrance;

    public void PrepareEntrance()
    {
        _entrance?.Kill();
        _entranceProgress = 0;
        Reflow();
    }

    public void PlayEntrance()
    {
        PrepareEntrance();
        _entrance = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _entrance.TweenMethod(Callable.From<float>(progress =>
        {
            _entranceProgress = progress;
            Reflow();
        }), 0f, 1f, .28);
    }

    public override void _ExitTree() => _entrance?.Kill();

    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Resized += Reflow; Reflow(); }
    private void Reflow()
    {
        float scale = Mathf.Min(Size.X / 480, Size.Y / 600);
        var nine = GetNode<Control>("Nine");
        nine.Position = new Vector2((Size.X - 480 * scale) / 2, 0)
            + new Vector2(171 + 36 * (1 - _entranceProgress), 12 * (1 - _entranceProgress)) * scale;
        nine.Size = new Vector2(342, 590) * scale;
        QueueRedraw();
    }
    public override void _Draw()
    {
        float scale = Mathf.Min(Size.X / 480, Size.Y / 600);
        var origin = new Vector2((Size.X - 480 * scale) / 2, 0);
        DrawSetTransform(origin, 0, Vector2.One * scale);
        var font = PaperStyle.Display;
        DrawString(PaperStyle.Body, new Vector2(0, 30), "S U D O K U   E N D L E S S", fontSize: 12, modulate: PaperStyle.Ink);
        DrawLine(new Vector2(240, 25), new Vector2(280, 25), PaperStyle.Ink, 1);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                DrawRect(new Rect2(416 + x * 11, 32 + y * 11, 8, 8), PaperStyle.Paper);
        // Foreground lettering has a paper knockout where it crosses the separate numeral layer.
        var titleOrigin = origin + new Vector2(-26, 14) * (1 - _entranceProgress) * scale;
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

