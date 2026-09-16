using System.Text.RegularExpressions;

namespace SudokuEndless;

/// <summary>
/// Every sound the music plays, with the measurements the mix depends on. Loudness trims come from
/// analysing the source files (RMS and peak), so swapping one loop for another never jumps in level.
/// </summary>
public static class MusicLibrary
{
    private const string Reference = "res://Resources/Audio/Reference/";
    private const string Atmosphere = "res://Resources/Audio/Atmosphere/";
    private const string Sounds = "res://Resources/Audio/Sounds/";

    public enum Voice { Pad, Keys, Mallet, Chime, Runs }

    /// <summary>A single dry note, pitched from <paramref name="RootMidi"/> to whatever the composer asks for.</summary>
    public sealed record Instrument(Voice Voice, string Path, int RootMidi, float GainDb);

    /// <summary>
    /// A folder of single notes named by pitch (<c>C3.wav</c>, <c>F#3.wav</c>, <c>Bb2.wav</c>). The folder is
    /// scanned at startup, so adding a note is just adding a file; each note plays at its recorded pitch.
    /// </summary>
    public sealed record SampledInstrument(Voice Voice, string Folder, float GainDb);

    /// <summary>A looping file.</summary>
    /// <param name="Bpm">Tempo it was recorded at (the [NN] in its file name); 0 plays it untimed at its own pitch.</param>
    /// <param name="TrimDb">Brings the measured RMS to its layer's target without pushing peaks above -3 dBFS.</param>
    public sealed record Loop(string Path, int Bpm, float TrimDb);

    /// <summary>Loops that share a tempo; the composer plays at <paramref name="Bpm"/>.</summary>
    public sealed record TempoSet(string Name, int Bpm, Loop[] Brushes, Loop[] Atmospheres);

    /// <summary>Gains are the Sound Kit page's layer levels.</summary>
    public static readonly Instrument[] Instruments =
    {
        new(Voice.Pad, Reference + "pad_C3.wav", 48, -7.5f),
        new(Voice.Keys, Reference + "keys_C4.wav", 60, -5.2f),
        new(Voice.Mallet, Reference + "mallet_C5.wav", 72, -7.5f),
        new(Voice.Chime, Reference + "chime_C5.wav", 72, -10.5f),
    };

    /// <summary>HollowTree peaks near -17 dBFS; +8 dB puts it level with the mallet.</summary>
    public static readonly SampledInstrument[] SampledInstruments =
    {
        new(Voice.Runs, Sounds + "HollowTree/", 8f),
    };

    /// <summary>Harmonic floor under every set, and the clock the composer follows. Its C matches the key.</summary>
    public static readonly Loop Drone = new(Reference + "drone_C2_loop17s.wav", 0, -11f);

    // Atmospheres target -28 dB RMS.
    private static readonly Loop AtmoNoire = new(Atmosphere + "Atmo[67] Noire.wav", 67, 3.0f);
    private static readonly Loop AtmoSorrow = new(Atmosphere + "Atmo[75] Sorrow.wav", 0, 3.7f); // steady bed, needs no sync
    private static readonly Loop AtmoDaylight = new(Atmosphere + "Atmo[82] Daylight.wav", 82, -4.4f);

    /// <summary>
    /// Timed loops play at their recorded tempo, so they need no resampling. Brushes are optional: a set
    /// without them simply never has a brushes section.
    /// </summary>
    public static readonly TempoSet[] Sets =
    {
        new("Noire", 67, new Loop[0], new[] { AtmoNoire, AtmoSorrow }),
        new("Daylight", 82, new Loop[0], new[] { AtmoDaylight, AtmoSorrow }),
    };

    private static readonly Regex NoteName = new(@"^([A-G])(#|b)?(-?\d)$");
    private static readonly int[] NaturalPitchClasses = { 9, 11, 0, 2, 4, 5, 7 }; // A B C D E F G

    /// <summary>Reads a MIDI note from a file name such as <c>F#3.wav</c> (C4 = 60).</summary>
    public static bool TryParseNote(string fileName, out int midi)
    {
        midi = 0;
        Match match = NoteName.Match(System.IO.Path.GetFileNameWithoutExtension(fileName));
        if (!match.Success) return false;
        int pitchClass = NaturalPitchClasses[match.Groups[1].Value[0] - 'A'];
        int accidental = match.Groups[2].Value switch { "#" => 1, "b" => -1, _ => 0 };
        midi = (int.Parse(match.Groups[3].Value) + 1) * 12 + pitchClass + accidental;
        return true;
    }
}
