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

	/// <summary>
	/// Optional. Assign NumberSelection.tres to turn the digit buttons into a radio group, so the bar
	/// also shows which digit was tapped last. Purely visual: input stays cell-first, and tapping the
	/// already-selected digit still applies it.
	/// </summary>
	[Export]
	public ButtonGroup NumberSelection { get; set; }

	/// <summary>Scene shown once the puzzle is solved.</summary>
	private const string SummaryScenePath = "res://Scenes/Summary/Summary.tscn";

	/// <summary>Side inset of the sheet, matching the margins set on the Sheet container.</summary>
	private const float SheetMargin = 32;

	private BoardView _board;
	private HBoxContainer _numberBar;
	private PaperButton _modeToggle;
	private Button _undoButton;
	private Button _redoButton;
	private NumberButton[] _buttons;
	private InputMode _mode = InputMode.Value;

	public override void _Ready()
	{
		_board = GetNodeOrNull<BoardView>("%Board");
		Resized += Layout;
		Layout();

		_numberBar = GetNodeOrNull<HBoxContainer>("%NumberBar");
		if (_numberBar == null)
		{
			GD.PushError("Main: expected an HBoxContainer with unique name 'NumberBar'.");
			return;
		}

		_modeToggle = GetNodeOrNull<PaperButton>("%ModeToggle");
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
			_board.Solved += OnSolved;
		}

		BuildNumberButtons();
		OnBoardChanged();
	}

	/// <summary>
	/// The board is square, but as a plain Control it reports no minimum, so left to expand it would
	/// float centred in whatever height is left over. Pin its height to the sheet's width instead and
	/// let the spacer below it take the slack, which seats the grid at the top of the sheet.
	/// </summary>
	private void Layout()
	{
		if (_board != null)
		{
			_board.CustomMinimumSize = new Vector2(0, Mathf.Max(0, Size.X - (SheetMargin * 2)));
		}
	}

	public override void _ExitTree() => Resized -= Layout;

	private void OnModeToggled(bool hintsOn)
	{
		_mode = hintsOn ? InputMode.Hints : InputMode.Value;
	}

	private void OnUndo() => _board?.Undo();

	private void OnRedo() => _board?.Redo();

	private void OnSolved()
	{
		GetTree().ChangeSceneToFile(SummaryScenePath);
	}

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
		button.NumberPressed += OnNumberPressed;

		// Erase stays outside the group — it is an action, not one of the choices.
		if (NumberSelection != null && number != 0)
		{
			button.ToggleMode = true;
			button.ButtonGroup = NumberSelection;
		}

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
			_buttons[n - 1].Disabled = counts[n] >= BoardGeometry.Size;
			// Disabled has no change signal, and the key draws itself.
			_buttons[n - 1].RefreshAvailability();
		}
	}
}
