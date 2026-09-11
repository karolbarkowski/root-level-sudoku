using Godot;

namespace Sudoku;

/// <summary>
/// Every tunable number the UI tween behaviours in <c>res://UI/Tweens</c> use, in one place. Change
/// a value here and every hover, press and entrance animation in the game changes with it.
///
/// Like <see cref="GameTheme"/> this is a <see cref="Resource"/> rather than an autoload, so the
/// <c>[Tool]</c> behaviours can read it at edit time too. Authored in UiAnimationDefault.tres.
///
/// A behaviour may point at a different settings resource via its <c>SettingsOverride</c> export
/// when one specific widget needs to break the rule — everything else keeps following this file.
/// </summary>
[GlobalClass]
public partial class UiAnimationSettings : Resource
{
	private const string DefaultPath = "res://Resources/UiAnimationDefault.tres";

	private static UiAnimationSettings _default;

	/// <summary>
	/// The project-wide settings, loaded (and cached) from disk. Falls back to the C# defaults below
	/// if the .tres is missing, so a broken path degrades into "still animates" rather than a crash.
	/// </summary>
	public static UiAnimationSettings Default =>
		_default ??= GD.Load<UiAnimationSettings>(DefaultPath) ?? new UiAnimationSettings();

	[ExportGroup("Global")]

	/// <summary>Master switch. When false every behaviour snaps to its target value instead of tweening.</summary>
	[Export] public bool Enabled { get; set; } = true;

	/// <summary>Divides every duration. 2 = everything twice as fast, 0.5 = half speed.</summary>
	[Export(PropertyHint.Range, "0.1,4,0.05")] public float SpeedScale { get; set; } = 1f;

	[ExportGroup("Normal")]

	/// <summary>Tint of an idle, interactive widget. Almost always plain white.</summary>
	[Export] public Color NormalModulate { get; set; } = Colors.White;

	[ExportGroup("Hover")]

	/// <summary>Size of a hovered widget, relative to its resting size.</summary>
	[Export(PropertyHint.Range, "0.5,2,0.005")] public float HoverScale { get; set; } = 1.06f;

	/// <summary>Tint of a hovered widget. Values above 1 brighten it.</summary>
	[Export] public Color HoverModulate { get; set; } = new Color(1.12f, 1.12f, 1.12f, 1f);

	/// <summary>Time to grow into the hovered look.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float HoverInDuration { get; set; } = 0.12f;

	/// <summary>Time to settle back once the pointer leaves. Slightly slower than in reads as "soft".</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float HoverOutDuration { get; set; } = 0.18f;

	[Export] public Tween.TransitionType HoverTransition { get; set; } = Tween.TransitionType.Back;
	[Export] public Tween.EaseType HoverEase { get; set; } = Tween.EaseType.Out;

	[ExportGroup("Press")]

	/// <summary>Size of a held-down widget. Below 1 so the press reads as a physical dip.</summary>
	[Export(PropertyHint.Range, "0.5,1.5,0.005")] public float PressScale { get; set; } = 0.94f;

	/// <summary>Tint of a held-down widget.</summary>
	[Export] public Color PressModulate { get; set; } = new Color(0.88f, 0.88f, 0.88f, 1f);

	/// <summary>Time to dip on press. Keep this short — the press must feel immediate.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float PressDownDuration { get; set; } = 0.06f;

	/// <summary>Time to spring back on release.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float PressUpDuration { get; set; } = 0.16f;

	[Export] public Tween.TransitionType PressTransition { get; set; } = Tween.TransitionType.Back;
	[Export] public Tween.EaseType PressEase { get; set; } = Tween.EaseType.Out;

	[ExportGroup("Disabled")]

	/// <summary>Tint of a widget that cannot be interacted with.</summary>
	[Export] public Color DisabledModulate { get; set; } = new Color(1f, 1f, 1f, 0.35f);

	/// <summary>Time to cross-fade in and out of the disabled look.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float DisabledDuration { get; set; } = 0.12f;

	[ExportGroup("Appear")]

	/// <summary>Size a widget starts its entrance from.</summary>
	[Export(PropertyHint.Range, "0,2,0.005")] public float AppearFromScale { get; set; } = 0.9f;

	/// <summary>Length of the entrance animation.</summary>
	[Export(PropertyHint.Range, "0,2,0.01")] public float AppearDuration { get; set; } = 0.28f;

	/// <summary>Delay added per <c>StaggerIndex</c> step, so a row of buttons cascades in.</summary>
	[Export(PropertyHint.Range, "0,0.5,0.01")] public float AppearStagger { get; set; } = 0.05f;

	/// <summary>Whether the entrance fades alpha in as well as scaling up.</summary>
	[Export] public bool AppearFade { get; set; } = true;

	[Export] public Tween.TransitionType AppearTransition { get; set; } = Tween.TransitionType.Back;
	[Export] public Tween.EaseType AppearEase { get; set; } = Tween.EaseType.Out;

	/// <summary>Applies <see cref="SpeedScale"/> to an authored duration.</summary>
	public float Duration(float seconds) => SpeedScale <= 0.01f ? 0f : seconds / SpeedScale;
}
