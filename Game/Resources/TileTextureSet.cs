using Godot;

namespace SudokuEndless;

/// <summary>
/// Data-driven visual styling shared by every tile: the digit textures plus the colors used
/// for highlights, given (clue) digits, player digits, and pencil-mark hints.
///
/// Keeping this in a <see cref="Resource"/> lets the whole look be edited in the inspector and
/// swapped without touching code — tiles stay "dumb" views. Digit art can be dropped in later
/// (index 1-9 of <see cref="DigitTextures"/>); until then tiles fall back to a text Label.
/// </summary>
[Tool]
[GlobalClass]
public partial class TileTextureSet : Resource
{
	/// <summary>
	/// Digit textures indexed by value: index 1-9 hold the art for those digits. Index 0
	/// (empty) is unused. Any null entry makes the tile fall back to a text Label for that digit.
	/// </summary>
	[Export]
	public Texture2D[] DigitTextures { get; set; } = new Texture2D[10];

	[ExportGroup("Highlight")]

	/// <summary>Fill behind the selected cell (the stronger of the two highlight tiers).</summary>
	[Export]
	public Color SelectedHighlightColor { get; set; } = new Color(0.30f, 0.55f, 0.95f, 0.40f);

	/// <summary>Fill behind cells sharing the selection's row, column, or box (softer tier).</summary>
	[Export]
	public Color PeerHighlightColor { get; set; } = new Color(0.30f, 0.55f, 0.95f, 0.14f);

	[ExportGroup("Font")]

	/// <summary>Typeface for the value digit and pencil marks. Null falls back to the theme font.</summary>
	[Export]
	public Font Font { get; set; }

	/// <summary>Value digit height as a fraction of the cell's height (0.6 = 60% of the cell).</summary>
	[Export(PropertyHint.Range, "0.05,1.0,0.01")]
	public float ValueFontScale { get; set; } = 0.6f;

	/// <summary>Pencil-mark height as a fraction of the cell's height.</summary>
	[Export(PropertyHint.Range, "0.05,1.0,0.01")]
	public float HintFontScale { get; set; } = 0.2f;

	[ExportSubgroup("Colors")]

	/// <summary>Font color for given (clue) digits.</summary>
	[Export]
	public Color GivenColor { get; set; } = new Color(0.13f, 0.13f, 0.16f);

	/// <summary>Font color for player-entered digits.</summary>
	[Export]
	public Color PlayerColor { get; set; } = new Color(0.20f, 0.35f, 0.70f);

	/// <summary>Font color for pencil-mark hints.</summary>
	[Export]
	public Color HintColor { get; set; } = new Color(0.45f, 0.45f, 0.50f);

	/// <summary>Returns the texture for a value, or null when none is set (→ Label fallback).</summary>
	public Texture2D GetDigit(int value)
	{
		if (DigitTextures == null || value < 0 || value >= DigitTextures.Length)
		{
			return null;
		}

		return DigitTextures[value];
	}

	/// <summary>Value font size in px for a given cell height, from <see cref="ValueFontScale"/>.</summary>
	public int ValueFontSize(float cellHeight) => Mathf.Max(1, Mathf.RoundToInt(cellHeight * ValueFontScale));

	/// <summary>Hint font size in px for a given cell height, from <see cref="HintFontScale"/>.</summary>
	public int HintFontSize(float cellHeight) => Mathf.Max(1, Mathf.RoundToInt(cellHeight * HintFontScale));
}
