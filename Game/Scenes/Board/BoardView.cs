using System;
using Godot;
using Generators.Sudoku;

namespace SudokuEndless;

/// <summary>
/// The Godot view over a Sudoku game. It owns a domain <see cref="Board"/> (from the Generators
/// project) as the central manager — generation, move history, undo/redo, hints — and renders it
/// through a 9x9 grid of tiles. Data flows one way: the domain board is pushed DOWN into tiles,
/// and tap events come UP via signals.
///
/// The domain board only stores cell values, so two UI concerns are kept here: which cells are
/// givens (clues) and the player's pencil-mark hints. Both live on the per-tile <see cref="CellData"/>
/// view-models; each cell's value is synced from the domain board.
///
/// <c>[Tool]</c> so the grid is visible in the editor; generation is skipped there (it is expensive)
/// and an empty board is previewed instead.
/// </summary>
[Tool]
public partial class BoardView : Control
{
	/// <summary>Emitted after the board changes, so observers (e.g. the number bar) refresh.</summary>
	[Signal]
	public delegate void BoardChangedEventHandler();

	/// <summary>The Tile scene instanced once per cell.</summary>
	[Export]
	public PackedScene TileScene { get; set; }

	/// <summary>Shared styling passed down to every tile.</summary>
	[Export]
	public TileTextureSet Textures { get; set; }

	/// <summary>Difficulty used when generating a new game at startup.</summary>
	[Export]
	public SudokuGenerator.Difficulty Difficulty { get; set; } = SudokuGenerator.Difficulty.Easy;

	private GridContainer _grid;
	private ColorRect _background;
	private MarginContainer _frame;
	private readonly Tile[] _tiles = new Tile[BoardGeometry.CellCount];
	private readonly CellData[] _cells = new CellData[BoardGeometry.CellCount];
	private Board _game;
	private int _selectedIndex = -1;

	public bool CanUndo => _game?.CanUndo ?? false;
	public bool CanRedo => _game?.CanRedo ?? false;

	public override void _Ready()
	{
		_grid = GetNodeOrNull<GridContainer>("AspectRatioContainer/Frame/Grid");
		if (_grid == null)
		{
			GD.PushError("BoardView: expected a GridContainer at 'AspectRatioContainer/Frame/Grid'.");
			return;
		}

		_background = GetNodeOrNull<ColorRect>("AspectRatioContainer/Background");
		_frame = GetNodeOrNull<MarginContainer>("AspectRatioContainer/Frame");

		ApplyGridSpacing();
		ApplyBoardFrame();
		BuildBoard();

		// The generator does real work (simulated annealing); don't run it on every editor open.
		if (Engine.IsEditorHint())
		{
			LoadEmpty();
		}
		else
		{
			NewGame(Difficulty);
		}
	}

	// --- Central manager surface (delegates to the domain board) ---

	/// <summary>Generates a fresh puzzle at the given difficulty and renders it.</summary>
	public void NewGame(SudokuGenerator.Difficulty difficulty)
	{
		EnsureCells();
		_game = SudokuGenerator.Generate(difficulty);
		_selectedIndex = -1;

		for (int i = 0; i < BoardGeometry.CellCount; i++)
		{
			int value = ValueAt(i);
			_cells[i].Value = value;
			_cells[i].IsGiven = value != 0;   // non-empty cells after generation are the clues
			_cells[i].Hints = Array.Empty<int>();
		}

		RenderAll();
		ApplyHighlights();
	}

	/// <summary>Counts of each digit (1-9) currently on the board, indexed by value.</summary>
	public int[] GetValueCounts()
	{
		var counts = new int[BoardGeometry.Size + 1];
		if (_game == null)
		{
			return counts;
		}

		for (int i = 0; i < BoardGeometry.CellCount; i++)
		{
			int value = ValueAt(i);
			if (value >= 1 && value <= BoardGeometry.Size)
			{
				counts[value]++;
			}
		}

		return counts;
	}

	/// <summary>
	/// Writes <paramref name="value"/> (0 clears) into the selected cell via the domain board, so the
	/// move is recorded for undo. No-ops when nothing is selected or the cell is a given.
	/// </summary>
	public void SetSelectedValue(int value)
	{
		if (!CanEditSelection())
		{
			return;
		}

		int row = BoardGeometry.RowOf(_selectedIndex);
		int col = BoardGeometry.ColOf(_selectedIndex);
		_game.PlaceMove(row, col, value);

		_cells[_selectedIndex].Value = value;
		_cells[_selectedIndex].Hints = Array.Empty<int>();
		RefreshTile(_selectedIndex);
		EmitSignal(SignalName.BoardChanged);
	}

	/// <summary>
	/// Toggles pencil-mark <paramref name="n"/> on the selected cell. Hints are a UI-only concern
	/// (the domain board doesn't track them) and only live in empty, non-given cells.
	/// </summary>
	public void ToggleSelectedHint(int n)
	{
		if (!CanEditSelection() || !_cells[_selectedIndex].IsEmpty)
		{
			return;
		}

		_cells[_selectedIndex].ToggleHint(n);
		RefreshTile(_selectedIndex);
		EmitSignal(SignalName.BoardChanged);
	}

	/// <summary>Undoes the last value move. Returns false when there is nothing to undo.</summary>
	public bool Undo()
	{
		if (_game == null || !_game.Undo())
		{
			return false;
		}

		SyncAllValues();
		RenderAll();
		EmitSignal(SignalName.BoardChanged);
		return true;
	}

	/// <summary>Redoes the last undone value move. Returns false when there is nothing to redo.</summary>
	public bool Redo()
	{
		if (_game == null || !_game.Redo())
		{
			return false;
		}

		SyncAllValues();
		RenderAll();
		EmitSignal(SignalName.BoardChanged);
		return true;
	}

	/// <summary>
	/// Asks the domain board for the next logical move, places it, and selects that cell. Returns
	/// false when no move can be suggested.
	/// </summary>
	public bool ApplyHint()
	{
		if (_game?.SuggestNextMove() is not Move move)
		{
			return false;
		}

		_game.PlaceMove(move.Row, move.Col, move.Value);
		int index = BoardGeometry.Index(move.Row, move.Col);
		_cells[index].Value = move.Value;
		_cells[index].Hints = Array.Empty<int>();
		_selectedIndex = index;

		RenderAll();
		ApplyHighlights();
		EmitSignal(SignalName.BoardChanged);
		return true;
	}

	/// <summary>
	/// Provisional completion check: every cell filled and no row/column duplicates. NOTE: the
	/// domain's Cost() does not yet validate boxes, so this is not a full solution check.
	/// </summary>
	public bool IsCompleted()
	{
		if (_game == null)
		{
			return false;
		}

		for (int i = 0; i < BoardGeometry.CellCount; i++)
		{
			if (ValueAt(i) == 0)
			{
				return false;
			}
		}

		return _game.Cost() == 0;
	}

	// --- Helpers ---

	private int ValueAt(int index) => _game[BoardGeometry.RowOf(index), BoardGeometry.ColOf(index)];

	private bool CanEditSelection() =>
		_selectedIndex >= 0 && _game != null && !_cells[_selectedIndex].IsGiven;

	private void EnsureCells()
	{
		for (int i = 0; i < BoardGeometry.CellCount; i++)
		{
			_cells[i] ??= new CellData();
		}
	}

	private void SyncAllValues()
	{
		for (int i = 0; i < BoardGeometry.CellCount; i++)
		{
			_cells[i].Value = ValueAt(i);
		}
	}

	// Editor-only preview: an empty grid, no generation.
	private void LoadEmpty()
	{
		EnsureCells();
		_game = null;
		_selectedIndex = -1;
		for (int i = 0; i < BoardGeometry.CellCount; i++)
		{
			_cells[i].Value = 0;
			_cells[i].IsGiven = false;
			_cells[i].Hints = Array.Empty<int>();
		}

		RenderAll();
	}

	private void RefreshTile(int index)
	{
		if (_tiles[index] != null)
		{
			_tiles[index].Data = _cells[index];
		}
	}

	private void RenderAll()
	{
		for (int i = 0; i < BoardGeometry.CellCount; i++)
		{
			if (_tiles[i] != null)
			{
				_tiles[i].Data = _cells[i];
			}
		}
	}

	// --- Layout / build (unchanged nesting: 3x3 grid of 3x3 box grids) ---

	private void ApplyGridSpacing()
	{
		if (_grid == null || Textures == null)
		{
			return;
		}

		_grid.AddThemeConstantOverride("h_separation", Textures.BoxGap);
		_grid.AddThemeConstantOverride("v_separation", Textures.BoxGap);
	}

	private void ApplyBoardFrame()
	{
		if (Textures == null)
		{
			return;
		}

		if (_background != null)
		{
			_background.Color = Textures.GridLineColor;
		}

		if (_frame != null)
		{
			foreach (string side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
			{
				_frame.AddThemeConstantOverride(side, Textures.BoxGap);
			}
		}
	}

	private void BuildBoard()
	{
		if (_grid == null || TileScene == null)
		{
			return;
		}

		foreach (Node child in _grid.GetChildren())
		{
			child.QueueFree();
		}

		_grid.Columns = BoardGeometry.BoxSize;
		int cellGap = Textures?.CellGap ?? 1;

		for (int boxRow = 0; boxRow < BoardGeometry.BoxSize; boxRow++)
		{
			for (int boxCol = 0; boxCol < BoardGeometry.BoxSize; boxCol++)
			{
				GridContainer box = CreateBox(cellGap);
				_grid.AddChild(box);

				for (int cellRow = 0; cellRow < BoardGeometry.BoxSize; cellRow++)
				{
					for (int cellCol = 0; cellCol < BoardGeometry.BoxSize; cellCol++)
					{
						int row = (boxRow * BoardGeometry.BoxSize) + cellRow;
						int col = (boxCol * BoardGeometry.BoxSize) + cellCol;
						int index = BoardGeometry.Index(row, col);

						var tile = TileScene.Instantiate<Tile>();
						tile.Index = index;
						tile.Textures = Textures;
						tile.Pressed += OnTilePressed;
						box.AddChild(tile);
						_tiles[index] = tile;
					}
				}
			}
		}
	}

	private static GridContainer CreateBox(int cellGap)
	{
		var box = new GridContainer
		{
			Columns = BoardGeometry.BoxSize,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		box.AddThemeConstantOverride("h_separation", cellGap);
		box.AddThemeConstantOverride("v_separation", cellGap);
		return box;
	}

	// --- Selection & highlighting ---

	private void OnTilePressed(int index)
	{
		_selectedIndex = index;
		ApplyHighlights();
	}

	private void ApplyHighlights()
	{
		for (int i = 0; i < BoardGeometry.CellCount; i++)
		{
			if (_tiles[i] != null)
			{
				_tiles[i].Highlight = HighlightFor(i);
			}
		}
	}

	private TileHighlight HighlightFor(int index)
	{
		if (_selectedIndex < 0)
		{
			return TileHighlight.None;
		}

		if (index == _selectedIndex)
		{
			return TileHighlight.Selected;
		}

		return BoardGeometry.ArePeers(index, _selectedIndex) ? TileHighlight.Peer : TileHighlight.None;
	}
}
