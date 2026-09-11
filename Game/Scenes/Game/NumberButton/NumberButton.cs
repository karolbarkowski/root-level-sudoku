using Godot;

namespace SudokuEndless;

/// <summary>
/// A single digit (1-9) in the number-selection bar, or the erase key (0, rendered blank).
///
/// The root is a real <see cref="Button"/>, so press semantics, keyboard/gamepad focus,
/// <see cref="BaseButton.Disabled"/> and optional <see cref="ButtonGroup"/> selection all come from
/// the engine rather than being hand-rolled here. Everything visible is a child node with
/// <c>mouse_filter = Ignore</c> so clicks fall through to the root — including the hover and press
/// animations, which are behaviour nodes from <c>res://UI/Tweens</c> baked into the scene.
///
/// The only thing left for this script is the digit itself.
/// </summary>
[Tool]
public partial class NumberButton : Button
{
	/// <summary>
	/// Emitted when the button is activated. Carries the digit it represents; the board reads 0 as
	/// "clear the cell". Named apart from <see cref="BaseButton.Pressed"/>, which it rides on.
	/// </summary>
	[Signal]
	public delegate void NumberPressedEventHandler(int number);

	private int _number = 1;
	private Label _label;

	/// <summary>The digit this button represents (1-9), or 0 for the erase button.</summary>
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

	public override void _Ready()
	{
		_label = GetNodeOrNull<Label>("Label");
		RefreshText();
	}

	public override void _Pressed() => EmitSignal(SignalName.NumberPressed, _number);

	// Number is usually assigned before the node enters the tree, so this runs again from _Ready.
	private void RefreshText()
	{
		if (_label != null)
		{
			_label.Text = _number == 0 ? string.Empty : _number.ToString();
		}
	}
}
