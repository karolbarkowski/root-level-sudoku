using Godot;
using System.Collections.Generic;
using System.Linq;

namespace SudokuEndless;

/// <summary>
/// The state of a single Sudoku cell. This is the tile's own state object and the unit of
/// data the board pushes down to each tile (state flows one way: board -> tile).
///
/// It is a <see cref="Resource"/> so it is fully inspectable in the editor and can be stored
/// as a sub-resource. Transient view state (e.g. selection highlight) is NOT stored here — it
/// lives on the <c>Tile</c> and is driven by the board.
/// </summary>
[Tool]
[GlobalClass]
public partial class CellData : Resource
{
    private int _value;
    private readonly HashSet<int> _hints = [];

    /// <summary>Current value: 0 = empty, 1-9 = filled.</summary>
    [Export(PropertyHint.Range, "0,9")]
    public int Value
    {
        get => _value;
        set
        {
            _value = Mathf.Clamp(value, 0, 9);
            EmitChanged();
        }
    }

    /// <summary>True for puzzle clues (immutable, styled differently from player entries).</summary>
    [Export]
    public bool IsGiven { get; set; }

    /// <summary>
    /// Pencil-mark hints (values 1-9). Backed by a <see cref="HashSet{T}"/> for simple set logic;
    /// exposed as a sorted array because Godot cannot export a HashSet — the array is the
    /// serialization / inspector surface, the set is the source of truth.
    /// </summary>
    [Export]
    public int[] Hints
    {
        get => _hints.OrderBy(n => n).ToArray();
        set
        {
            _hints.Clear();
            if (value != null)
            {
                _hints.UnionWith(value);
            }

            EmitChanged();
        }
    }

    public bool IsEmpty => _value == 0;

    public bool HasHint(int n) => _hints.Contains(n);

    /// <summary>Adds the hint if absent, removes it if present. Ignores out-of-range values.</summary>
    public void ToggleHint(int n)
    {
        if (n < 1 || n > 9)
        {
            return;
        }

        if (!_hints.Add(n))
        {
            _hints.Remove(n);
        }

        EmitChanged();
    }

    /// <summary>Resets the cell to empty with no hints (value and pencil marks cleared).</summary>
    public void Clear()
    {
        Value = 0;
        _hints.Clear();
        EmitChanged();
    }

    /// <summary>Returns an independent deep copy so shared references cannot leak between owners.</summary>
    public CellData Copy() => (CellData)Duplicate(true);
}
