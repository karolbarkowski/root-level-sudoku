using Godot;

namespace SudokuEndless;

/// <summary>
/// A single digit (1-9) in the number-selection bar. For now it only displays its number and a
/// toned-down disabled state — actual selection and board mutation are intentionally not wired up
/// yet (a Pressed signal + input handling will be added when selection lands).
///
/// It is a <see cref="Control"/> rather than a <see cref="Button"/> so <see cref="IsDisabled"/> is
/// unambiguously ours (BaseButton already carries its own disabled concept).
/// </summary>
[Tool]
public partial class NumberButton : Control
{
	/// <summary>Emitted when an enabled button is tapped. Carries the digit it represents.</summary>
	[Signal]
	public delegate void PressedEventHandler(int number);

	private int _number = 1;
	private bool _isDisabled;

	private Label _label;
	private bool _nodesReady;

	/// <summary>
	/// The digit this button represents (1-9), or 0 for the erase button (rendered blank). Tapping
	/// emits this value; the board treats 0 as "clear the cell".
	/// </summary>
	[Export(PropertyHint.Range, "0,9")]
	public int Number
	{
		get => _number;
		set
		{
			_number = Mathf.Clamp(value, 0, 9);
			RefreshText();
		}
	}

	/// <summary>
	/// When true the button is toned down and stops receiving input. The main script sets this
	/// once all nine of this digit are already placed on the board.
	/// </summary>
	[Export]
	public bool IsDisabled
	{
		get => _isDisabled;
		set
		{
			_isDisabled = value;
			RefreshDisabled();
		}
	}

	public override void _Ready()
	{
		_label = GetNodeOrNull<Label>("Label");
		_nodesReady = _label != null;
		RefreshText();
		RefreshDisabled();
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (_isDisabled)
		{
			return;
		}

		bool pressed =
			@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } ||
			@event is InputEventScreenTouch { Pressed: true };

		if (pressed)
		{
			EmitSignal(SignalName.Pressed, _number);
			AcceptEvent();
		}
	}

	private void RefreshText()
	{
		if (_nodesReady)
		{
			_label.Text = _number == 0 ? string.Empty : _number.ToString();
		}
	}

	private void RefreshDisabled()
	{
		Modulate = _isDisabled ? new Color(1f, 1f, 1f, 0.35f) : Colors.White;
		MouseFilter = _isDisabled ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
	}
}
