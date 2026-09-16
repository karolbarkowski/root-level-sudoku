using System;
using System.Collections.Generic;
using System.Linq;
using Voice = SudokuEndless.MusicLibrary.Voice;

namespace SudokuEndless;

/// <summary>
/// Writes the notes of one bar at a time in C Lydian, one chord every eight bars, with plenty of rest.
/// Pad, keys, mallet and chime follow the Sound Kit page; the runs voice walks across the scale on a
/// sampled instrument. Pure logic with no Godot types; timing is in beats.
/// </summary>
public sealed class Composer
{
    public const int BeatsPerBar = 4;
    public const int BarsPerChord = 8;

    public readonly record struct Chord(string Name, int[] Notes);

    public readonly record struct Note(double Beat, Voice Voice, int Midi, float Velocity, float Pan);

    /// <summary>C D E F# G A B. The raised fourth is what makes it Lydian.</summary>
    public static readonly int[] Scale = { 0, 2, 4, 6, 7, 9, 11 };

    /// <summary>Every chord sits on the C drone; D/C is the classic Lydian colour.</summary>
    public static readonly Chord[] Chords =
    {
        new("Cmaj9", new[] { 48, 52, 55, 62 }),
        new("D/C", new[] { 48, 54, 57, 62 }),
        new("Em7", new[] { 52, 55, 59, 62 }),
        new("Cmaj7#11", new[] { 48, 55, 59, 66 }),
    };

    // Keys keep to the pentatonic subset, so the F# belongs to the runs and chords.
    private static readonly int[] KeysRange = InScale(64, 81, 0, 2, 4, 7, 9);
    private static readonly int[] ChimeRange = InScale(72, 91, Scale);
    private static readonly int[] PadPans = { -1, 0, 1 };

    /// <summary>Melody moves mostly by step, sometimes leaps, rarely repeats.</summary>
    private static readonly (int Step, double Weight)[] KeysSteps =
        { (-2, .12), (-1, .3), (0, .05), (1, .28), (2, .15), (-3, .05), (3, .05) };

    private readonly Random _rng;
    private readonly double _beatsPerSecond;
    private readonly int[] _runNotes;
    private int _keysIndex = 5;

    /// <param name="runNotes">Notes the runs instrument has samples for; those outside the scale are ignored.</param>
    public Composer(int seed, int bpm, IEnumerable<int> runNotes)
    {
        _rng = new Random(seed);
        _beatsPerSecond = bpm / 60.0;
        _runNotes = runNotes.Where(m => Scale.Contains(PitchClass(m))).Distinct().Order().ToArray();
    }

    public static Chord ChordAt(int bar) => Chords[bar / BarsPerChord % Chords.Length];

    /// <summary>Appends the notes of <paramref name="bar"/> to <paramref name="notes"/>, unsorted.</summary>
    /// <param name="tight">Brushes are playing, so timing drifts less against the groove.</param>
    public void ComposeBar(int bar, bool tight, List<Note> notes)
    {
        double start = bar * BeatsPerBar;
        Chord chord = ChordAt(bar);
        double humanize = (tight ? .006 : .015) * _beatsPerSecond;

        void Add(double beat, Voice voice, int midi, double velocity, double pan) =>
            notes.Add(new Note(beat + Range(-humanize, humanize), voice, midi, (float)velocity, (float)pan));

        // Pad: re-breathe the chord every two bars, three of its four notes, slightly staggered.
        if (bar % 2 == 0)
        {
            int[] voicing = chord.Notes.OrderBy(_ => _rng.Next()).Take(3).ToArray();
            for (int i = 0; i < voicing.Length; i++)
                Add(start + i * .12 * _beatsPerSecond, Voice.Pad, voicing[i], Range(.55, .8), PadPans[i] * .45);
        }

        // Runs: every other bar, often, a sweep across the scale. Keys rest while it plays.
        bool run = _runNotes.Length >= 3 && bar % 2 == 1 && _rng.NextDouble() < .6;
        if (run) AddRun(start, chord, Add);

        // Keys: short phrases, a weighted walk over the pentatonic scale, lots of rest.
        if (!run && _rng.NextDouble() < .55)
        {
            int count = 1 + _rng.Next(3);
            int slot = _rng.Next(4);
            for (int i = 0; i < count && slot < 8; i++)
            {
                _keysIndex = Math.Clamp(_keysIndex + KeysStep(), 0, KeysRange.Length - 1);
                Add(start + slot * .5, Voice.Keys, KeysRange[_keysIndex], Range(.35, .6), Range(-.2, .2));
                slot += Pick(new[] { 1, 2, 2, 3 });
            }
        }

        // Mallet: sparse chord tones two octaves up.
        for (int beat = 0; beat < BeatsPerBar; beat++)
            if (_rng.NextDouble() < .16)
                Add(start + beat, Voice.Mallet, Pick(chord.Notes) + 24, Range(.35, .6), Range(-.6, .6));

        // Chime: rare, high, long.
        if (bar % 2 == 1 && !run && _rng.NextDouble() < .35)
        {
            int[] tones = ChimeRange.Where(m => chord.Notes.Any(c => PitchClass(c) == PitchClass(m))).ToArray();
            Add(start + _rng.Next(3), Voice.Chime, Pick(tones.Length > 0 ? tones : ChimeRange), Range(.4, .65), Range(-.5, .5));
        }
    }

    /// <summary>
    /// Four to seven neighbouring scale notes going up, down, or up and back, in eighths or triplets.
    /// It lands on a chord tone where it can, swells towards the middle, and pans with the direction of travel.
    /// </summary>
    private void AddRun(double barStart, Chord chord, Action<double, Voice, int, double, double> add)
    {
        int shape = _rng.Next(3); // 0 up, 1 down, 2 up and back
        int length = Math.Min(4 + _rng.Next(4), shape == 2 ? _runNotes.Length * 2 - 1 : _runNotes.Length);
        int climb = shape == 2 ? length / 2 + 1 : length; // notes up to and including the turn
        int first = shape == 1
            ? _runNotes.Length - 1 - _rng.Next(_runNotes.Length - climb + 1)
            : _rng.Next(_runNotes.Length - climb + 1);

        var sequence = new List<int>();
        for (int i = 0; i < length; i++)
        {
            int offset = shape == 2 && i >= climb ? 2 * (climb - 1) - i : i;
            sequence.Add(_runNotes[shape == 1 ? first - offset : first + offset]);
        }
        // Drop up to two trailing notes to finish on a chord tone.
        for (int trim = 0; trim < 2 && sequence.Count > 3 && !IsChordTone(sequence[^1], chord); trim++)
            sequence.RemoveAt(sequence.Count - 1);

        double step = _rng.NextDouble() < .25 ? 1 / 3.0 : .5;
        double beat = barStart + Pick(new[] { 0.0, .5, 1.0 });
        bool rising = shape != 1;
        for (int i = 0; i < sequence.Count; i++)
        {
            double t = sequence.Count == 1 ? .5 : i / (double)(sequence.Count - 1);
            double swell = Math.Sin(Math.PI * t);
            double pan = (rising ? t - .5 : .5 - t) * 1.2;
            add(beat + i * step, Voice.Runs, sequence[i], .45 + .25 * swell + Range(-.04, .04), pan);
        }
    }

    private static bool IsChordTone(int midi, Chord chord) => chord.Notes.Any(c => PitchClass(c) == PitchClass(midi));

    private int KeysStep()
    {
        double r = _rng.NextDouble();
        foreach (var (step, weight) in KeysSteps)
            if ((r -= weight) <= 0) return step;
        return 1;
    }

    private double Range(double min, double max) => min + _rng.NextDouble() * (max - min);

    private T Pick<T>(T[] items) => items[_rng.Next(items.Length)];

    private static int PitchClass(int midi) => (midi % 12 + 12) % 12;

    private static int[] InScale(int low, int high, params int[] pitchClasses) =>
        Enumerable.Range(low, high - low + 1).Where(m => pitchClasses.Contains(PitchClass(m))).ToArray();
}
