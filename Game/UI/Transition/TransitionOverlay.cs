using System.Threading.Tasks;
using Godot;
using Sudoku;

namespace SudokuEndless;

/// <summary>
/// Input-blocking cover owned by <see cref="SceneTransition"/>. The paper-coloured cover and its
/// message (title, detail and spinner) fade separately, so a quick scene change can be a plain
/// cross-fade while a slow one says what it is waiting for.
/// </summary>
[Tool]
public partial class TransitionOverlay : Control
{
    private const double CoverInDuration = .2;
    private const double CoverOutDuration = .25;
    private const double ContentInDuration = .25;
    private const double ContentOutDuration = .15;

    /// <summary>How far below its resting place the message starts. A nudge, not a fly-in.</summary>
    private const float ContentRise = 8;

    private const float SpinnerTop = -54;
    private const float SpinnerBottom = 10;

    private Tween _tween;
    private ColorRect _spinner;
    private string _title = "GENERATING PUZZLE";
    private string _detail = "Preparing your board...";
    private float _contentAlpha = 1;
    private float _contentShift;

    // Bumped by every Show/Hide so a superseded hide cannot switch off a newer show.
    private int _version;

    /// <summary>Message shown above the spinner while this overlay is active.</summary>
    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? string.Empty;
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        _spinner = GetNode<ColorRect>("Spinner");
        MouseFilter = MouseFilterEnum.Stop;
        ApplyContent();
    }

    /// <summary>
    /// Keeps the overlay drawing at an imperceptible alpha so the spinner shader compiles now,
    /// rather than on the frame of the first tap.
    /// </summary>
    public void Prewarm()
    {
        Visible = true;
        MouseFilter = MouseFilterEnum.Ignore;
        Modulate = new Color(1, 1, 1, .01f);
        SetContent(1, 0);
    }

    /// <summary>Hides a <see cref="Prewarm"/>ed overlay, unless a real transition has taken it over.</summary>
    public void EndPrewarm()
    {
        if (_version == 0) Visible = false;
    }

    /// <summary>Fades the cover in and, when a title is given, the message after it.</summary>
    public async Task ShowAsync(string title, double delay)
    {
        int version = ++_version;
        _tween?.Kill();
        bool message = !string.IsNullOrEmpty(title);
        bool hidden = !Visible || Modulate.A <= .02f;
        if (message) Title = title;
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;

        if (!UiAnimationSettings.Default.Enabled)
        {
            Modulate = Colors.White;
            SetContent(message ? 1 : 0, 0);
            return;
        }

        // Start from wherever a cancelled hide left the cover.
        if (hidden) Modulate = new Color(1, 1, 1, 0);
        if (!message) SetContent(0, 0);
        else if (hidden || _contentAlpha < .01f) SetContent(0, ContentRise);
        _tween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _tween.TweenProperty(this, "modulate:a", 1f, CoverInDuration).SetDelay(delay);
        double total = delay + CoverInDuration;
        if (message)
        {
            double contentDelay = delay + CoverInDuration * .5;
            _tween.TweenMethod(Callable.From<float>(a => SetContent(a, _contentShift)), _contentAlpha, 1f, ContentInDuration).SetDelay(contentDelay);
            _tween.TweenMethod(Callable.From<float>(s => SetContent(_contentAlpha, s)), _contentShift, 0f, ContentInDuration).SetDelay(contentDelay);
            total = contentDelay + ContentInDuration;
        }
        await Wait(total);
        if (version == _version) Modulate = Colors.White;
    }

    /// <summary>
    /// Fades the message out, then the cover. Input passes through as soon as the cover starts to
    /// lift, and <paramref name="uncovering"/> runs a moment later so the next scene's entrance
    /// plays while it is still visible.
    /// </summary>
    public async Task HideAsync(System.Action uncovering)
    {
        int version = ++_version;
        _tween?.Kill();
        if (!UiAnimationSettings.Default.Enabled)
        {
            MouseFilter = MouseFilterEnum.Ignore;
            uncovering?.Invoke();
            Visible = false;
            return;
        }

        if (_contentAlpha > .01f)
        {
            _tween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            _tween.TweenMethod(Callable.From<float>(a => SetContent(a, _contentShift)), _contentAlpha, 0f, ContentOutDuration);
            await Wait(ContentOutDuration);
            if (version != _version) return;
            SetContent(0, 0);
        }

        MouseFilter = MouseFilterEnum.Ignore;
        _tween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        _tween.TweenProperty(this, "modulate:a", 0f, CoverOutDuration);
        await Wait(.06);
        if (version != _version) return;
        uncovering?.Invoke();
        await Wait(CoverOutDuration - .06);
        if (version == _version) Visible = false;
    }

    private async Task Wait(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private void SetContent(float alpha, float shift)
    {
        _contentAlpha = alpha;
        _contentShift = shift;
        ApplyContent();
    }

    private void ApplyContent()
    {
        if (_spinner != null)
        {
            _spinner.Modulate = new Color(1, 1, 1, _contentAlpha);
            _spinner.OffsetTop = SpinnerTop + _contentShift;
            _spinner.OffsetBottom = SpinnerBottom + _contentShift;
        }
        QueueRedraw();
    }

    public override void _ExitTree() => _tween?.Kill();

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), PaperStyle.Paper);
        if (_contentAlpha <= 0) return;
        Vector2 center = new(Size.X / 2, Size.Y / 2 - 22 + _contentShift);
        float width = PaperStyle.Body.GetStringSize(_title, fontSize: 18).X;
        DrawString(PaperStyle.Body, new Vector2((Size.X - width) / 2, center.Y + 66), _title, fontSize: 18, modulate: PaperStyle.Ink with { A = _contentAlpha });
        float detailWidth = PaperStyle.Body.GetStringSize(_detail, fontSize: 14).X;
        DrawString(PaperStyle.Body, new Vector2((Size.X - detailWidth) / 2, center.Y + 94), _detail, fontSize: 14, modulate: PaperStyle.Muted with { A = _contentAlpha });
    }
}
