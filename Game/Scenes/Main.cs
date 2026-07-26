using Godot;

namespace SudokuEndless;

/// <summary>
/// Top-level game coordinator. Builds the number-selection bar below the board and keeps each
/// digit button's disabled state in sync with how many of that digit are already on the board
/// (a digit is fully placed at <see cref="BoardState.Size"/> occurrences).
///
/// Selection and board mutation are intentionally not implemented yet.
/// </summary>
[Tool]
public partial class Main : Control
{
	/// <summary>The board whose contents drive the digit counts.</summary>
	[Export]
	public Board Board { get; set; }

	/// <summary>The NumberButton scene instanced once per digit (1-9).</summary>
	[Export]
	public PackedScene NumberButtonScene { get; set; }

	private HBoxContainer _numberBar;
	private NumberButton[] _buttons;

	public override void _Ready()
	{
		_numberBar = GetNodeOrNull<HBoxContainer>("%NumberBar");
		if (_numberBar == null)
		{
			GD.PushError("Main: expected an HBoxContainer with unique name 'NumberBar'.");
			return;
		}

		BuildNumberButtons();
		RefreshDisabledStates();
	}

	private void BuildNumberButtons()
	{
		if (NumberButtonScene == null)
		{
			return;
		}

		foreach (Node child in _numberBar.GetChildren())
		{
			child.QueueFree();
		}

		_buttons = new NumberButton[BoardState.Size];
		for (int n = 1; n <= BoardState.Size; n++)
		{
			var button = NumberButtonScene.Instantiate<NumberButton>();
			button.Number = n;
			_numberBar.AddChild(button);
			_buttons[n - 1] = button;
		}
	}

	/// <summary>Disables each digit button whose value already appears the maximum number of times.</summary>
	private void RefreshDisabledStates()
	{
		if (_buttons == null || Board == null)
		{
			return;
		}

		int[] counts = Board.GetValueCounts();
		for (int n = 1; n <= BoardState.Size; n++)
		{
			_buttons[n - 1].IsDisabled = counts[n] >= BoardState.Size;
		}
	}
}
