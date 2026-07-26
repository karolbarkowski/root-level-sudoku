using Godot;

namespace SudokuEndless;

/// <summary>
/// Builds and drives the 9x9 grid of tiles. The board owns the authoritative <see cref="BoardState"/>
/// and is the single source of truth: it pushes cell data DOWN into tiles and receives tap events
/// UP from them (via the tile's <c>Pressed</c> signal). Tiles never touch game state directly.
///
/// The script is a <c>[Tool]</c> so the populated grid is visible in the editor. Tiles are created
/// at runtime and added without an owner, so opening/saving this scene never bakes them into the
/// <c>.tscn</c>.
/// </summary>
[Tool]
public partial class Board : Control
{
    /// <summary>The Tile scene instanced once per cell.</summary>
    [Export]
    public PackedScene TileScene { get; set; }

    /// <summary>Shared styling passed down to every tile.</summary>
    [Export]
    public TileTextureSet Textures { get; set; }

    private GridContainer _grid;
    private ColorRect _background;
    private MarginContainer _frame;
    private readonly Tile[] _tiles = new Tile[BoardState.CellCount];
    private BoardState _state;
    private int _selectedIndex = -1;

    public override void _Ready()
    {
        _grid = GetNodeOrNull<GridContainer>("AspectRatioContainer/Frame/Grid");
        if (_grid == null)
        {
            GD.PushError("Board: expected a GridContainer at 'AspectRatioContainer/Frame/Grid'.");
            return;
        }

        _background = GetNodeOrNull<ColorRect>("AspectRatioContainer/Background");
        _frame = GetNodeOrNull<MarginContainer>("AspectRatioContainer/Frame");

        ApplyGridSpacing();
        ApplyBoardFrame();
        BuildAndRender();
    }

    /// <summary>
    /// Counts of each digit (1-9) currently on the board, indexed by value. Used by the main
    /// script to disable number buttons whose digit is fully placed.
    /// </summary>
    public int[] GetValueCounts() => _state?.GetValueCounts() ?? new int[BoardState.Size + 1];

    /// <summary>Loads the hardcoded puzzle, (re)builds the tile grid, and renders it.</summary>
    public void BuildAndRender()
    {
        _state = SudokuPuzzles.CreateHardcoded();
        BuildBoard();
        RenderAll();
    }

    /// <summary>Applies the larger inter-box gap to the outer grid (see <see cref="BuildBoard"/>).</summary>
    private void ApplyGridSpacing()
    {
        if (_grid == null || Textures == null)
        {
            return;
        }

        _grid.AddThemeConstantOverride("h_separation", Textures.BoxGap);
        _grid.AddThemeConstantOverride("v_separation", Textures.BoxGap);
    }

    /// <summary>
    /// Frames the board: paints the shared grid-line color behind the grid and insets the grid by
    /// BoxGap on every side, so the same color that fills the gaps also forms a BoxGap-wide border.
    /// </summary>
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
    /// Builds the board as a 3x3 grid of 3x3 box grids. A single GridContainer can only apply one
    /// uniform separation, so nesting is what lets tiles inside a box sit closer (CellGap) than the
    /// boxes themselves (BoxGap). Tiles are still tracked by their logical board index, so the rest
    /// of the code is unaffected by the visual nesting.
    /// </summary>
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

        _grid.Columns = BoardState.BoxSize;
        int cellGap = Textures?.CellGap ?? 1;

        for (int boxRow = 0; boxRow < BoardState.BoxSize; boxRow++)
        {
            for (int boxCol = 0; boxCol < BoardState.BoxSize; boxCol++)
            {
                GridContainer box = CreateBox(cellGap);
                _grid.AddChild(box);

                for (int cellRow = 0; cellRow < BoardState.BoxSize; cellRow++)
                {
                    for (int cellCol = 0; cellCol < BoardState.BoxSize; cellCol++)
                    {
                        int row = (boxRow * BoardState.BoxSize) + cellRow;
                        int col = (boxCol * BoardState.BoxSize) + cellCol;
                        int index = BoardState.Index(row, col);

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
            Columns = BoardState.BoxSize,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        box.AddThemeConstantOverride("h_separation", cellGap);
        box.AddThemeConstantOverride("v_separation", cellGap);
        return box;
    }

    /// <summary>Pushes every cell's state down to its tile (state flows one way: board → tile).</summary>
    private void RenderAll()
    {
        if (_state == null)
        {
            return;
        }

        for (int i = 0; i < BoardState.CellCount; i++)
        {
            if (_tiles[i] != null)
            {
                _tiles[i].Data = _state.GetCell(i);
            }
        }
    }

    /// <summary>
    /// Events-up handler. For this slice it just moves the selection highlight; value entry will
    /// mutate <see cref="_state"/> here and re-render, keeping the flow unidirectional.
    /// </summary>
    private void OnTilePressed(int index)
    {
        _selectedIndex = index;
        ApplyHighlights();
    }

    /// <summary>
    /// Recomputes every tile's highlight tier from the current selection: the selected cell plus
    /// its whole row, column, and 3x3 box. Cheap enough (81 cells) to redo on each tap rather than
    /// track deltas.
    /// </summary>
    private void ApplyHighlights()
    {
        for (int i = 0; i < BoardState.CellCount; i++)
        {
            if (_tiles[i] == null)
            {
                continue;
            }

            _tiles[i].Highlight = HighlightFor(i);
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

        return BoardState.ArePeers(index, _selectedIndex) ? TileHighlight.Peer : TileHighlight.None;
    }
}
