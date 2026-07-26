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

    [Export]
    public Color HighlightColor { get; set; } = new Color(0.30f, 0.55f, 0.95f, 0.35f);

    [Export]
    public Color GivenColor { get; set; } = new Color(0.13f, 0.13f, 0.16f);

    [Export]
    public Color PlayerColor { get; set; } = new Color(0.20f, 0.35f, 0.70f);

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
}
