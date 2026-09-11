using Godot;

namespace Sudoku;

[Tool]
public partial class ModulateAccentShadow : TextureRect
{
	public override void _Ready()
	{
		Modulate = GameTheme.Default.ColorAccentShadow;
	}
}
