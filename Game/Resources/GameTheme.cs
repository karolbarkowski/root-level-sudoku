using Godot;

namespace Sudoku;

/// <summary>
/// Global palette, authored in a <c>.tres</c> (see ThemeDefault.tres) and loaded on demand.
///
/// It is a <see cref="Resource"/> — deliberately NOT a Node/autoload — because the scripts that use
/// it (<c>ModulateAccent</c>, etc.) run as <c>[Tool]</c> in the editor, and autoloads do not exist
/// at edit time. <see cref="GD.Load{T}"/> works in both the editor and at runtime.
/// </summary>
[GlobalClass]
public partial class GameTheme : Resource
{
	private const string DefaultPath = "res://Resources/ThemeDefault.tres";

	[Export] public Color ColorBg { get; set; }
	[Export] public Color ColorAccent { get; set; }
	[Export] public Color ColorAccentShadow { get; set; }

	private static GameTheme _default;

	/// <summary>The default theme, loaded (and cached) from disk. Safe to call in the editor.</summary>
	public static GameTheme Default => _default ??= GD.Load<GameTheme>(DefaultPath);
}
