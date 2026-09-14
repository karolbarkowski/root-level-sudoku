using Godot;

namespace SudokuEndless;

/// <summary>How strongly a tile is highlighted, driven by the board's current selection.</summary>
public enum TileHighlight
{
	/// <summary>Not related to the selection.</summary>
	None,

	/// <summary>Shares the selected cell's row, column, or 3x3 box.</summary>
	Peer,

	/// <summary>Is the selected cell itself.</summary>
	Selected,
}

/// <summary>
/// A single Sudoku cell's view. It is a pure, "dumb" view: it renders the <see cref="CellData"/>
/// the board pushes into it and reports taps back up via <see cref="PressedEventHandler"/>. It
/// never mutates game state — that keeps the data flow one-directional (state down, events up).
///
/// The script is a <c>[Tool]</c> so it renders live in the editor. Exported properties are the
/// tile's public surface: <see cref="Data"/> (its state object), <see cref="IsHighlighted"/>,
/// and <see cref="Textures"/>. <see cref="CurrentValue"/> and <see cref="Hints"/> are code-level
/// convenience accessors over <see cref="Data"/>.
/// </summary>
[Tool]
public partial class Tile : Control
{
	/// <summary>Emitted when the tile is tapped/clicked. Carries the tile's board index.</summary>
	[Signal]
	public delegate void PressedEventHandler(int index);

	/// <summary>Board position (row*9+col). Assigned by the board when it builds the grid.</summary>
	public int Index { get; set; }

	private CellData _data;
	private TileHighlight _highlight;
	private TileTextureSet _textures;

	// Cached child nodes (see CacheNodes). Guarded by _nodesReady until _Ready has run.
	private ColorRect _background;
	private ColorRect _highlightRect;
	private TextureRect _valueTexture;
	private Label _valueLabel;
	private GridContainer _hintsGrid;
	private readonly Label[] _hintLabels = new Label[9];
	private bool _nodesReady;

	/// <summary>The cell state this tile displays. Set by the board; treated as read-only here.</summary>
	[Export]
	public CellData Data
	{
		get => _data;
		set
		{
			_data = value;
			RefreshVisuals();
		}
	}

	/// <summary>
	/// Transient selection highlight tier (none / peer / selected). Driven by the board, not part
	/// of the puzzle model.
	/// </summary>
	[Export]
	public TileHighlight Highlight
	{
		get => _highlight;
		set
		{
			_highlight = value;
			RefreshHighlight();
		}
	}

	/// <summary>Shared visual styling (digit textures + colors). Injected by the board.</summary>
	[Export]
	public TileTextureSet Textures
	{
		get => _textures;
		set
		{
			_textures = value;
			RefreshVisuals();
			RefreshHighlight();
		}
	}

	/// <summary>Convenience accessor for the displayed value (0 = empty), backed by <see cref="Data"/>.</summary>
	public int CurrentValue
	{
		get => _data?.Value ?? 0;
		set
		{
			EnsureData();
			_data.Value = value;
			RefreshVisuals();
		}
	}

	/// <summary>Convenience accessor for the pencil-mark hints, backed by <see cref="Data"/>.</summary>
	public int[] Hints
	{
		get => _data?.Hints ?? System.Array.Empty<int>();
		set
		{
			EnsureData();
			_data.Hints = value;
			RefreshVisuals();
		}
	}

	public override void _Ready()
	{
		CacheNodes();
		RefreshVisuals();
		RefreshHighlight();
	}

	public override void _GuiInput(InputEvent @event)
	{
		bool pressed =
			@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } ||
			@event is InputEventScreenTouch { Pressed: true };

		if (pressed)
		{
			EmitSignal(SignalName.Pressed, Index);
			AcceptEvent();
		}
	}

	private void CacheNodes()
	{
		_background = GetNodeOrNull<ColorRect>("Background");
		_highlightRect = GetNodeOrNull<ColorRect>("HighlightRect");
		_valueTexture = GetNodeOrNull<TextureRect>("ValueTexture");
		_valueLabel = GetNodeOrNull<Label>("ValueLabel");
		_hintsGrid = GetNodeOrNull<GridContainer>("HintsGrid");

		if (_hintsGrid != null)
		{
			for (int i = 0; i < _hintLabels.Length; i++)
			{
				_hintLabels[i] = _hintsGrid.GetNodeOrNull<Label>($"Hint{i + 1}");
			}
		}

		_nodesReady = _valueLabel != null && _hintsGrid != null;
	}

	private void EnsureData() => _data ??= new CellData();

	private void RefreshVisuals()
	{
		if (!_nodesReady)
		{
			return;
		}

		if (_background != null && _textures != null)
		{
			_background.Color = _textures.TileColor;
		}

		int value = _data?.Value ?? 0;
		bool isGiven = _data?.IsGiven ?? false;
		bool hasValue = value != 0;

		Texture2D texture = _textures?.GetDigit(value);

		// A filled cell shows its digit (texture preferred, Label fallback); hints are hidden.
		if (hasValue && texture != null)
		{
			_valueTexture.Texture = texture;
			_valueTexture.Visible = true;
			_valueLabel.Visible = false;
		}
		else if (hasValue)
		{
			_valueLabel.Text = value.ToString();
			_valueLabel.Visible = true;
			_valueTexture.Visible = false;

			if (_textures != null)
			{
				_valueLabel.AddThemeColorOverride(
					"font_color", _highlight == TileHighlight.Selected || isGiven ? _textures.GivenColor : _textures.PlayerColor);
			}
		}
		else
		{
			_valueTexture.Visible = false;
			_valueLabel.Visible = false;
		}

		// Pencil marks only render in an empty cell.
		bool anyHint = false;
		for (int i = 0; i < _hintLabels.Length; i++)
		{
			Label label = _hintLabels[i];
			if (label == null)
			{
				continue;
			}

			bool present = !hasValue && (_data?.HasHint(i + 1) ?? false);
			anyHint |= present;
			label.Text = present ? (i + 1).ToString() : string.Empty;
			if (present && _textures != null)
			{
				label.AddThemeColorOverride("font_color", _highlight == TileHighlight.Selected ? _textures.GivenColor : _textures.HintColor);
			}
		}

		// Hidden unless it has marks to show: a visible grid of nine empty labels still costs a
		// layout pass per tile, and most cells never get notes.
		_hintsGrid.Visible = anyHint;
	}

	private void RefreshHighlight()
	{
		RefreshVisuals();
		if (!_nodesReady || _highlightRect == null)
		{
			return;
		}

		if (_highlight == TileHighlight.None)
		{
			_highlightRect.Visible = false;
			return;
		}

		// Keep the scene's default color if no texture set is injected yet.
		if (_textures != null)
		{
			_highlightRect.Color = _highlight == TileHighlight.Selected
				? _textures.SelectedHighlightColor
				: _textures.PeerHighlightColor;
		}

		_highlightRect.Visible = true;
	}
}
