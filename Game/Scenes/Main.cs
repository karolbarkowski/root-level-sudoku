using Godot;

namespace SudokuEndless;

/// <summary>
/// Top-level game coordinator. Builds the number-selection bar below the board, routes digit
/// taps into the board, and keeps each button's disabled state in sync with how many of that
/// digit are already placed (a digit is full at <see cref="BoardState.Size"/> occurrences).
///
/// It mediates between the number bar and the board so neither needs to know about the other.
/// </summary>
[Tool]
public partial class Main : Control
{
	/// <summary>The NumberButton scene instanced once per digit (1-9).</summary>
	[Export]
	public PackedScene NumberButtonScene { get; set; }

	private Board _board;
	private HBoxContainer _numberBar;
	private NumberButton[] _buttons;

	public override void _Ready()
	{
		_board = GetNodeOrNull<Board>("%Board");
		_numberBar = GetNodeOrNull<HBoxContainer>("%NumberBar");
		if (_numberBar == null)
		{
			GD.PushError("Main: expected an HBoxContainer with unique name 'NumberBar'.");
			return;
		}

		if (_board != null)
		{
			_board.BoardChanged += RefreshDisabledStates;
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
			_buttons[n - 1] = AddButton(n);
		}

		// Erase button: same row, rendered blank, number 0 (which the board reads as "clear").
		// It is not tracked in _buttons, so it is never disabled by digit counts.
		AddButton(0);
	}

	private NumberButton AddButton(int number)
	{
		var button = NumberButtonScene.Instantiate<NumberButton>();
		button.Number = number;
		button.Pressed += OnNumberPressed;
		_numberBar.AddChild(button);
		return button;
	}

	/// <summary>A digit was tapped: place it into the board's currently selected cell.</summary>
	private void OnNumberPressed(int number)
	{
		_board?.SetSelectedValue(number);
	}

	/// <summary>Disables each digit button whose value already appears the maximum number of times.</summary>
	private void RefreshDisabledStates()
	{
		if (_buttons == null || _board == null)
		{
			return;
		}

		int[] counts = _board.GetValueCounts();
		for (int n = 1; n <= BoardState.Size; n++)
		{
			_buttons[n - 1].IsDisabled = counts[n] >= BoardState.Size;
		}
	}
}
