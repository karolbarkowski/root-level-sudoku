using Godot;

namespace Sudoku;

/// <summary>
/// Project-wide UI motion settings, authored in UiAnimationDefault.tres. Every scene and widget that
/// animates checks <see cref="Enabled"/> and snaps to its end state when it is off.
///
/// A <see cref="Resource"/> rather than an autoload, so <c>[Tool]</c> widgets can read it at edit time too.
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

	/// <summary>Master switch. When false every animation snaps to its end state instead of tweening.</summary>
	[Export] public bool Enabled { get; set; } = true;
}
