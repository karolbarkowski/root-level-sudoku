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
    private readonly Tile[] _tiles = new Tile[BoardState.CellCount];
    private BoardState _state;
    private int _selectedIndex = -1;

    public override void _Ready()
    {
        _grid = GetNodeOrNull<GridContainer>("AspectRatioContainer/Grid");
        if (_grid == null)
        {
            GD.PushError("Board: expected a GridContainer at 'AspectRatioContainer/Grid'.");
            return;
        }

        BuildAndRender();
    }

    /// <summary>Loads the hardcoded puzzle, (re)builds the tile grid, and renders it.</summary>
    public void BuildAndRender()
    {
        _state = SudokuPuzzles.CreateHardcoded();
        BuildBoard();
        RenderAll();
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

        _grid.Columns = BoardState.Size;

        for (int i = 0; i < BoardState.CellCount; i++)
        {
            var tile = TileScene.Instantiate<Tile>();
            tile.Index = i;
            tile.Textures = Textures;
            tile.Pressed += OnTilePressed;
            _grid.AddChild(tile);
            _tiles[i] = tile;
        }
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
        if (_selectedIndex >= 0 && _tiles[_selectedIndex] != null)
        {
            _tiles[_selectedIndex].ClearHighlight();
        }

        _selectedIndex = index;
        if (_tiles[index] != null)
        {
            _tiles[index].Highlight();
        }
    }
}
