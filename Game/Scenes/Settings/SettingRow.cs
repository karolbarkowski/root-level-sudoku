using Godot;
using System;

namespace SudokuEndless;

/// <summary>A labeled setting with explicit ON/OFF choices and an optional volume slider.</summary>
public partial class SettingRow : VBoxContainer
{
    private static StyleBoxFlat Surface(Color color) => new()
    {
        BgColor = color, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
        CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
        ContentMarginLeft = 4, ContentMarginRight = 4
    };

    public void Configure(string title, string description, bool enabled, Action<bool> changed,
        int volume = 0, Action<int> volumeChanged = null)
    {
        AddThemeConstantOverride("separation", 8);
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 14);
        AddChild(line);
        var copy = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        line.AddChild(copy);
        var label = new Label { Text = title, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", 21);
        copy.AddChild(label);
        var detail = new Label { Text = description, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        detail.AddThemeFontSizeOverride("font_size", 15);
        detail.AddThemeColorOverride("font_color", PaperStyle.Muted);
        copy.AddChild(detail);
        var panel = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        panel.AddThemeStyleboxOverride("panel", Surface(PaperStyle.Surface));
        line.AddChild(panel);
        var choices = new HBoxContainer();
        choices.AddThemeConstantOverride("separation", 2);
        panel.AddChild(choices);
        var group = new ButtonGroup();
        Button MakeChoice(string text)
        {
            var button = new Button { Text = text, ToggleMode = true, ButtonGroup = group,
                CustomMinimumSize = new Vector2(48, 44), AccessibilityName = title + " " + text };
            button.AddThemeFontSizeOverride("font_size", 14);
            button.AddThemeStyleboxOverride("normal", Surface(Colors.Transparent));
            button.AddThemeStyleboxOverride("hover", Surface(PaperStyle.Surface.Lightened(.15f)));
            button.AddThemeStyleboxOverride("pressed", Surface(PaperStyle.Burgundy));
            button.AddThemeStyleboxOverride("hover_pressed", Surface(PaperStyle.Burgundy.Lightened(.1f)));
            button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            button.AddThemeColorOverride("font_color", PaperStyle.Muted);
            button.AddThemeColorOverride("font_pressed_color", PaperStyle.Ink);
            choices.AddChild(button);
            return button;
        }
        var on = MakeChoice("ON");
        var off = MakeChoice("OFF");
        on.SetPressedNoSignal(enabled);
        off.SetPressedNoSignal(!enabled);
        HSlider slider = null;
        HBoxContainer volumeRow = null;
        void Apply(bool value)
        {
            changed(value);
            if (slider == null) return;
            slider.Editable = value;
            slider.FocusMode = value ? FocusModeEnum.All : FocusModeEnum.None;
            volumeRow.Modulate = new Color(1, 1, 1, value ? 1 : .35f);
        }
        on.Toggled += value => { if (value) Apply(true); };
        off.Toggled += value => { if (value) Apply(false); };
        if (volumeChanged == null) return;
        volumeRow = new HBoxContainer();
        volumeRow.AddThemeConstantOverride("separation", 12);
        AddChild(volumeRow);
        volumeRow.AddChild(new Label { Text = "1" });
        slider = new HSlider { MinValue = 1, MaxValue = 10, Step = 1, Value = volume,
            TickCount = 10, TicksOnBorders = true, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 44), AccessibilityName = title + " volume",
            Editable = enabled, FocusMode = enabled ? FocusModeEnum.All : FocusModeEnum.None };
        slider.AddThemeStyleboxOverride("slider", Surface(PaperStyle.Surface));
        slider.AddThemeStyleboxOverride("grabber_area", Surface(PaperStyle.Burgundy));
        slider.AddThemeStyleboxOverride("grabber_area_highlight", Surface(PaperStyle.Burgundy.Lightened(.1f)));
        volumeRow.AddChild(slider);
        volumeRow.AddChild(new Label { Text = "10" });
        var readout = new Label { Text = volume + "/10", CustomMinimumSize = new Vector2(52, 0), HorizontalAlignment = HorizontalAlignment.Right };
        volumeRow.AddChild(readout);
        volumeRow.Modulate = new Color(1, 1, 1, enabled ? 1 : .35f);
        slider.ValueChanged += value => { readout.Text = (int)value + "/10"; volumeChanged((int)value); };
    }
}
