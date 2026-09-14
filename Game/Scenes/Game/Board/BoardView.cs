using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using Generators.Sudoku;
using Sudoku;

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

	/// <summary>Emitted once the last cell is filled and the completed board is a valid solution.</summary>
	[Signal]
	public delegate void SolvedEventHandler();

	/// <summary>Emitted when the selected cell changes, carrying the user-entered value or 0.</summary>
	[Signal]
	public delegate void SelectionChangedEventHandler(int value);

	/// <summary>The Tile scene instanced once per cell.</summary>
	[Export]
	public PackedScene TileScene { get; set; }

	/// <summary>Shared styling passed down to every tile.</summary>
	[Export]
	public TileTextureSet Textures { get; set; }

	private GridContainer _grid;
	private ColorRect _background;
	private MarginContainer _frame;
	private readonly Tile[] _tiles = new Tile[BoardGeometry.CellCount];
	private readonly CellData[] _cells = new CellData[BoardGeometry.CellCount];
	private Board _game;
	private int _selectedIndex = -1;
	private Task _built = Task.CompletedTask;

	/// <summary>
	/// Fonts for every tile's labels. One shared theme, resized with the cells: per-label overrides
	/// on 81 tiles × 10 labels re-theme and re-measure ~800 labels on every layout pass.
	/// </summary>
	private readonly Theme _tileTheme = new();
	private const string TileValueType = "TileValue";
	private int _valueFontSize;
	private int _hintFontSize;

	/// <summary>
	/// Frame budget for building tiles in game. Each tile is a small scene, and building all 81 at
	/// once stalls the frame long enough to freeze the loading spinner.
	/// </summary>
	private const double BuildBudgetMs = 6;

	public bool CanUndo => _game?.CanUndo ?? false;
	public bool CanRedo => _game?.CanRedo ?? false;

	/// <summary>Completes once every tile exists and shows the current state. Built over several frames in game.</summary>
	public Task WhenBuilt => _built;

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
		ApplyTileTheme();

		// The generator does real work (simulated annealing); the editor previews an empty board.
		if (!Engine.IsEditorHint() && GameSession.HasPuzzle)
		{
			_game = GameSession.ActiveBoard;
			Array.Copy(GameSession.Cells, _cells, _cells.Length);
			_selectedIndex = GameSession.SelectedIndex;
		}
		else LoadEmpty();

		_built = BuildBoardAsync(sliced: !Engine.IsEditorHint());
	}

	// --- Central manager surface (delegates to the domain board) ---

	/// <summary>Clears the player's numbers and notes, keeps the clues, and starts the history afresh.</summary>
	public void Restart()
	{
		if (_game == null)
		{
			return;
		}

		Span<bool> isGiven = stackalloc bool[BoardGeometry.CellCount];
		for (int i = 0; i < BoardGeometry.CellCount; i++)
		{
			isGiven[i] = _cells[i].IsGiven;
			if (!isGiven[i])
			{
				_cells[i].Clear();
			}
		}

		_game.Restart(isGiven);
		_selectedIndex = -1;
		GameSession.SelectedIndex = -1;
		RenderAll();
		ApplyHighlights();
		EmitSignal(SignalName.SelectionChanged, 0);
		EmitSignal(SignalName.BoardChanged);
	}

	/// <summary>True when there is anything <see cref="Restart"/> would clear: a number, a note or history.</summary>
	public bool HasProgress
	{
		get
		{
			if (_game == null)
			{
				return false;
			}

			if (_game.CanUndo || _game.CanRedo)
			{
				return true;
			}

			foreach (CellData cell in _cells)
			{
				if (cell != null && !cell.IsGiven && (cell.Value != 0 || cell.HasHints))
				{
					return true;
				}
			}

			return false;
		}
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
		if (!CanEditSelection() || value < 0 || value > 9 || ValueAt(_selectedIndex) == value)
		{
			return;
		}

		int row = BoardGeometry.RowOf(_selectedIndex);
		int col = BoardGeometry.ColOf(_selectedIndex);
		_game.PlaceMove(row, col, value);

		// Keep the cell's hints: the tile hides them while a value is present, so they simply
		// reappear if the value is later erased or undone (rather than being lost).
		_cells[_selectedIndex].Value = value;
		RefreshTile(_selectedIndex);
		EmitSignal(SignalName.SelectionChanged, SelectedUserValue);
		EmitSignal(SignalName.BoardChanged);
		CheckForCompletion();
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

	/// <summary>Erase a value, or clear pencil marks when the selected cell is already empty.</summary>
	public void EraseSelected()
	{
		if (!CanEditSelection()) return;
		if (ValueAt(_selectedIndex) != 0) { SetSelectedValue(0); return; }
		_cells[_selectedIndex].Hints = Array.Empty<int>();
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
		EmitSignal(SignalName.SelectionChanged, SelectedUserValue);
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
		EmitSignal(SignalName.SelectionChanged, SelectedUserValue);
		EmitSignal(SignalName.BoardChanged);
		CheckForCompletion();
		return true;
	}

	/// <summary>
	/// Runs the win check only when the board has just been fully filled (the last free cell), then
	/// asks the domain board to validate the solution. Emits <see cref="Solved"/> when correct.
	/// </summary>
	private void CheckForCompletion()
	{
		if (_game != null && IsFull() && _game.IsSolved())
		{
			EmitSignal(SignalName.Solved);
		}
	}

	private bool IsFull()
	{
		for (int i = 0; i < BoardGeometry.CellCount; i++)
		{
			if (ValueAt(i) == 0)
			{
				return false;
			}
		}

		return true;
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

	private void ApplyTileTheme()
	{
		if (_grid == null || Textures == null)
		{
			return;
		}

		_tileTheme.SetTypeVariation(TileValueType, "Label");
		Font hintFont = Textures.HintFont ?? Textures.Font;
		if (hintFont != null) _tileTheme.SetFont("font", "Label", hintFont);
		if (Textures.Font != null) _tileTheme.SetFont("font", TileValueType, Textures.Font);
		_grid.Theme = _tileTheme;
	}

	/// <summary>Font sizes are a fraction of the cell height, so follow the first tile's size.</summary>
	private void OnFirstTileResized() => UpdateTileFonts(_tiles[0]?.Size.Y ?? 0);

	/// <summary>
	/// The cell height the grid's layout will produce, worked out from the square frame. Lets the fonts
	/// be sized before any tile exists: resizing them afterwards re-measures every label on the board.
	/// </summary>
	private float EstimateCellHeight()
	{
		if (_frame == null || Textures == null) return 0;
		float side = Mathf.Min(_frame.Size.X, _frame.Size.Y) - Textures.BoxGap * 2;
		float box = (side - Textures.BoxGap * (BoardGeometry.BoxSize - 1)) / BoardGeometry.BoxSize;
		return (box - Textures.CellGap * (BoardGeometry.BoxSize - 1)) / BoardGeometry.BoxSize;
	}

	private void UpdateTileFonts(float cellHeight)
	{
		if (Textures == null || cellHeight <= 0)
		{
			return;
		}

		int valueSize = Textures.ValueFontSize(cellHeight);
		int hintSize = Textures.HintFontSize(cellHeight);
		if (valueSize == _valueFontSize && hintSize == _hintFontSize)
		{
			return;
		}

		_valueFontSize = valueSize;
		_hintFontSize = hintSize;
		_tileTheme.SetFontSize("font_size", TileValueType, valueSize);
		_tileTheme.SetFontSize("font_size", "Label", hintSize);
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

	/// <summary>
	/// Builds the 3x3 grid of boxes. When <paramref name="sliced"/>, yields to the next frame whenever
	/// <see cref="BuildBudgetMs"/> is spent; without it, completes synchronously.
	/// </summary>
	private async Task BuildBoardAsync(bool sliced)
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
		var clock = Stopwatch.StartNew();

		if (sliced)
		{
			// Let the empty frame lay out first, so the estimate below has a real size to work from.
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (!IsInstanceValid(this) || !IsInsideTree()) return;
			clock.Restart();
		}
		UpdateTileFonts(EstimateCellHeight());

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
						tile.Data = _cells[index];
						tile.Highlight = HighlightFor(index);
						tile.Pressed += OnTilePressed;
						box.AddChild(tile);
						_tiles[index] = tile;

						if (sliced && clock.Elapsed.TotalMilliseconds > BuildBudgetMs)
						{
							await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
							if (!IsInstanceValid(this) || !IsInsideTree()) return;
							clock.Restart();
						}
					}
				}
			}
		}

		// Containers pass through transient sizes while tiles are being added. Following those would
		// re-size every font several times, so only track resizes once the board has settled.
		if (sliced)
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (!IsInstanceValid(this) || !IsInsideTree()) return;
		}
		_tiles[0].Resized += OnFirstTileResized;
		OnFirstTileResized();
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
		box.Draw += () => box.DrawRect(new Rect2(Vector2.Zero, box.Size), new Color("596267"));
		box.Resized += box.QueueRedraw;
		return box;
	}

	// --- Selection & highlighting ---

	public void SelectCell(int index)
	{
		if (index < 0 || index >= _cells.Length) return;
		_selectedIndex = index;
		GameSession.SelectedIndex = index;
		ApplyHighlights();
		EmitSignal(SignalName.SelectionChanged, SelectedUserValue);
		EmitSignal(SignalName.BoardChanged);
	}

	private void OnTilePressed(int index) => SelectCell(index);
	public bool CanEdit => CanEditSelection();
	public int SelectedIndex => _selectedIndex;
	public int SelectedUserValue =>
		_selectedIndex >= 0 && !_cells[_selectedIndex].IsGiven ? _cells[_selectedIndex].Value : 0;
	public bool SelectedHasHint(int number) =>
		_selectedIndex >= 0 && !_cells[_selectedIndex].IsGiven && _cells[_selectedIndex].IsEmpty &&
		_cells[_selectedIndex].HasHint(number);

	/// <summary>Clears both the board highlight and the number-strip selection.</summary>
	public void ClearSelection()
	{
		if (_selectedIndex < 0) return;
		_selectedIndex = -1;
		GameSession.SelectedIndex = -1;
		ApplyHighlights();
		EmitSignal(SignalName.SelectionChanged, 0);
		EmitSignal(SignalName.BoardChanged);
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
