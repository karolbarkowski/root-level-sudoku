using Godot;
using System;

namespace SudokuEndless;

/// <summary>A labeled setting with an ON/OFF toggle and an optional volume slider.</summary>
public partial class SettingRow : VBoxContainer
{
    private const int GrabberSize = 30;
    private const int ToggleInset = 4;
    private const int ChoiceGap = 2;
    private static ImageTexture _grabber;

    /// <summary>A white, anti-aliased disc for the slider handle; shared by every row.</summary>
    private static ImageTexture Grabber => _grabber ??= Disc(GrabberSize, PaperStyle.Ink);

    private static StyleBoxFlat Surface(Color color, int radius = 10) => new()
    {
        BgColor = color, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ContentMarginLeft = 4, ContentMarginRight = 4
    };

    private static ImageTexture Disc(int size, Color color)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x + .5f - radius, y + .5f - radius).Length();
                image.SetPixel(x, y, color with { A = Mathf.Clamp(radius - distance, 0, 1) });
            }
        return ImageTexture.CreateFromImage(image);
    }

    public void Configure(string title, string description, bool enabled, Action<bool> changed,
        int volume = 0, Action<int> volumeChanged = null)
    {
        AddThemeConstantOverride("separation", 8);
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 14);
        AddChild(line);
        var copy = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        line.AddChild(copy);
        // No wrapping: the copy fits the design column, and a fixed height lets hidden screens lay out.
        var label = new Label { Text = title };
        label.AddThemeFontSizeOverride("font_size", 21);
        copy.AddChild(label);
        var detail = new Label { Text = description };
        detail.AddThemeFontSizeOverride("font_size", 15);
        detail.AddThemeColorOverride("font_color", PaperStyle.Muted);
        copy.AddChild(detail);

        // One button covers the whole switch, so a tap anywhere on it flips the state.
        var toggle = new Button { ToggleMode = true, SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(106, 44), AccessibilityName = title,
            MouseDefaultCursorShape = CursorShape.PointingHand };
        toggle.AddThemeStyleboxOverride("normal", Surface(PaperStyle.Surface));
        toggle.AddThemeStyleboxOverride("pressed", Surface(PaperStyle.Surface));
        toggle.AddThemeStyleboxOverride("hover", Surface(PaperStyle.Surface.Lightened(.1f)));
        toggle.AddThemeStyleboxOverride("hover_pressed", Surface(PaperStyle.Surface.Lightened(.1f)));
        toggle.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        line.AddChild(toggle);
        // An orange thumb behind the captions slides to the chosen side.
        var thumb = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        thumb.AddThemeStyleboxOverride("panel", Surface(PaperStyle.Burgundy));
        toggle.AddChild(thumb);
        var choices = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        choices.AddThemeConstantOverride("separation", ChoiceGap);
        choices.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        choices.OffsetLeft = ToggleInset;
        choices.OffsetRight = -ToggleInset;
        toggle.AddChild(choices);
        Label MakeChoice(string text)
        {
            var caption = new Label { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            caption.AddThemeFontSizeOverride("font_size", 14);
            choices.AddChild(caption);
            return caption;
        }
        Label off = MakeChoice("OFF");
        Label on = MakeChoice("ON");
        float progress = enabled ? 1 : 0; // 0 = OFF, 1 = ON
        Tween slide = null;
        void Layout()
        {
            float chip = (toggle.Size.X - ToggleInset * 2 - ChoiceGap) / 2;
            thumb.Position = new Vector2(ToggleInset + progress * (chip + ChoiceGap), 0);
            thumb.Size = new Vector2(chip, toggle.Size.Y);
            off.AddThemeColorOverride("font_color", PaperStyle.Ink.Lerp(PaperStyle.Muted, progress));
            on.AddThemeColorOverride("font_color", PaperStyle.Muted.Lerp(PaperStyle.Ink, progress));
        }
        void Paint(bool value)
        {
            slide?.Kill();
            float target = value ? 1 : 0;
            if (!global::Sudoku.UiAnimationSettings.Default.Enabled || !toggle.IsInsideTree())
            {
                progress = target;
                Layout();
                return;
            }
            slide = toggle.CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            slide.TweenMethod(Callable.From<float>(v => { progress = v; Layout(); }), progress, target, .2);
        }
        toggle.Resized += Layout;
        toggle.SetPressedNoSignal(enabled);
        Layout();

        HSlider slider = null;
        void Apply(bool value)
        {
            Paint(value);
            changed(value);
            if (slider == null) return;
            slider.Editable = value;
            slider.FocusMode = value ? FocusModeEnum.All : FocusModeEnum.None;
            slider.Modulate = new Color(1, 1, 1, value ? 1 : .35f);
        }
        toggle.Toggled += Apply;
        if (volumeChanged == null) return;

        slider = new HSlider { MinValue = 1, MaxValue = 10, Step = 1, Value = volume,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 44), AccessibilityName = title + " volume",
            Editable = enabled, FocusMode = enabled ? FocusModeEnum.All : FocusModeEnum.None,
            Modulate = new Color(1, 1, 1, enabled ? 1 : .35f) };
        // The track's height is its style's vertical margins: a 4 px line across the full width.
        StyleBoxFlat Track(Color color)
        {
            StyleBoxFlat box = Surface(color, 2);
            box.ContentMarginTop = box.ContentMarginBottom = 2;
            return box;
        }
        slider.AddThemeStyleboxOverride("slider", Track(PaperStyle.Surface.Lightened(.1f)));
        slider.AddThemeStyleboxOverride("grabber_area", Track(PaperStyle.Burgundy));
        slider.AddThemeStyleboxOverride("grabber_area_highlight", Track(PaperStyle.Burgundy.Lightened(.1f)));
        slider.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        foreach (string icon in new[] { "grabber", "grabber_highlight", "grabber_disabled" })
            slider.AddThemeIconOverride(icon, Grabber);
        AddChild(slider);
        slider.ValueChanged += value => volumeChanged((int)value);
    }
}
