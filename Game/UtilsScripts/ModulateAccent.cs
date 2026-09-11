using Godot;

namespace Sudoku;

[Tool]
public partial class ModulateAccent : TextureRect
{
	public override void _Ready()
	{
		Modulate = GameTheme.Default.ColorAccent;
	}
}
