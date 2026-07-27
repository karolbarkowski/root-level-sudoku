using Godot;

namespace SudokuEndless;

/// <summary>How a tapped number is applied to the selected cell.</summary>
public enum InputMode
{
	/// <summary>The digit becomes the cell's value.</summary>
	Value,

	/// <summary>The digit is toggled as a pencil-mark hint.</summary>
	Hints,
}

/// <summary>
/// Top-level game coordinator. Builds the number-selection bar below the board, routes digit
/// taps into the board, and keeps each button's disabled state in sync with how many of that
/// digit are already placed (a digit is full at <see cref="BoardGeometry.Size"/> occurrences).
///
/// It mediates between the number bar and the board so neither needs to know about the other.
/// </summary>
[Tool]
public partial class Main : Control
{
	/// <summary>The NumberButton scene instanced once per digit (1-9).</summary>
	[Export]
	public PackedScene NumberButtonScene { get; set; }

	private BoardView _board;
	private HBoxContainer _numberBar;
	private CheckButton _modeToggle;
	private Button _undoButton;
	private Button _redoButton;
	private NumberButton[] _buttons;
	private InputMode _mode = InputMode.Value;

	public override void _Ready()
	{
		_board = GetNodeOrNull<BoardView>("%Board");
		_numberBar = GetNodeOrNull<HBoxContainer>("%NumberBar");
		if (_numberBar == null)
		{
			GD.PushError("Main: expected an HBoxContainer with unique name 'NumberBar'.");
			return;
		}

		_modeToggle = GetNodeOrNull<CheckButton>("%ModeToggle");
		if (_modeToggle != null)
		{
			_modeToggle.Toggled += OnModeToggled;
			_mode = _modeToggle.ButtonPressed ? InputMode.Hints : InputMode.Value;
		}

		_undoButton = GetNodeOrNull<Button>("%UndoButton");
		_redoButton = GetNodeOrNull<Button>("%RedoButton");
		if (_undoButton != null)
		{
			_undoButton.Pressed += OnUndo;
		}

		if (_redoButton != null)
		{
			_redoButton.Pressed += OnRedo;
		}

		if (_board != null)
		{
			_board.BoardChanged += OnBoardChanged;
		}

		BuildNumberButtons();
		OnBoardChanged();
	}

	private void OnModeToggled(bool hintsOn)
	{
		_mode = hintsOn ? InputMode.Hints : InputMode.Value;
	}

	private void OnUndo() => _board?.Undo();

	private void OnRedo() => _board?.Redo();

	// Any board change can affect both the digit counts and what can be undone/redone.
	private void OnBoardChanged()
	{
		RefreshDisabledStates();
		RefreshActionButtons();
	}

	private void RefreshActionButtons()
	{
		if (_undoButton != null)
		{
			_undoButton.Disabled = !(_board?.CanUndo ?? false);
		}

		if (_redoButton != null)
		{
			_redoButton.Disabled = !(_board?.CanRedo ?? false);
		}
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

		_buttons = new NumberButton[BoardGeometry.Size];
		for (int n = 1; n <= BoardGeometry.Size; n++)
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

	/// <summary>
	/// A digit was tapped: in value mode it becomes the cell's value; in hint mode it toggles a
	/// pencil mark. Erase (0) always clears the cell regardless of mode.
	/// </summary>
	private void OnNumberPressed(int number)
	{
		if (_board == null)
		{
			return;
		}

		if (number == 0 || _mode == InputMode.Value)
		{
			_board.SetSelectedValue(number);
		}
		else
		{
			_board.ToggleSelectedHint(number);
		}
	}

	/// <summary>Disables each digit button whose value already appears the maximum number of times.</summary>
	private void RefreshDisabledStates()
	{
		if (_buttons == null || _board == null)
		{
			return;
		}

		int[] counts = _board.GetValueCounts();
		for (int n = 1; n <= BoardGeometry.Size; n++)
		{
			_buttons[n - 1].IsDisabled = counts[n] >= BoardGeometry.Size;
		}
	}
}
