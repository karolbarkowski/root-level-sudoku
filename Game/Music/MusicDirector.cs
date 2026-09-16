using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voice = SudokuEndless.MusicLibrary.Voice;

namespace SudokuEndless;

/// <summary>
/// Autoload that plays the generative soundtrack under every scene.
/// <list type="bullet">
/// <item>Loops (drone, atmospheres, optional brushes) start together and never stop; sections come and go
/// by volume, so timed loops stay sample-locked to each other.</item>
/// <item>The composer's bar clock is read from the drone's playback position, so notes follow the audio
/// device rather than the frame clock and cannot drift from the loops.</item>
/// <item>Every voice is a set of samples keyed by pitch: a one-file instrument is pitched from its root,
/// a scanned Sounds folder plays each note from its own recording.</item>
/// <item>Notes play through three polyphonic players on left, centre and right buses; the buses add
/// delay and reverb, so every note shares one space.</item>
/// </list>
/// Buses are defined in <c>default_bus_layout.tres</c>; Settings should control the <c>Music</c> bus.
/// Missing files are reported and skipped, so the music still plays while sounds are being swapped.
/// </summary>
public partial class MusicDirector : Node
{
    private const string NotesBus = "MusicNotes";
    private const string NotesLeftBus = "MusicNotesL";
    private const string NotesRightBus = "MusicNotesR";
    private const string BedsBus = "MusicBeds";

    /// <summary>Overall level of the soundtrack before the Music bus.</summary>
    private const float MixDb = -4f;

    private const float AtmosphereLayerDb = 6f;
    private const float BrushesLayerDb = 4f;

    /// <summary>A bar is composed this many beats before it starts, so early-humanized notes still fit.</summary>
    private const double LookaheadBeats = .5;

    /// <summary>Notes later than this (after a stall) are dropped rather than played in a burst.</summary>
    private const double LateBeats = .25;

    /// <summary>Brushes stay out while the music opens.</summary>
    private const int BrushesFirstBar = 8;

    private static readonly int[] SectionBars = { 8, 12, 16 };

    /// <summary>Atmospheres cross-fade to the next one this often.</summary>
    private const int AtmosphereBars = 32;

    private const double OpeningFade = 3;
    private const double BrushesFadeIn = .6;
    private const double BrushesFadeOut = 1.5;
    private const double AtmosphereCrossfade = 4;

    private static readonly string[] AudioExtensions = { ".wav", ".ogg", ".mp3" };

    private sealed class LoopVoice
    {
        public MusicLibrary.Loop Loop;
        public AudioStreamPlayer Player;
        public float BaseDb;
        public double Gain;
        public double Target;
        public double FadeSeconds = 1;
    }

    /// <summary>The recordings one voice can play, by MIDI note.</summary>
    private sealed class VoiceSamples
    {
        public readonly SortedList<int, string> Paths = new();
        public float GainDb;
    }

    private readonly Dictionary<string, AudioStream> _streams = new();
    private readonly List<string> _loading = new();
    private readonly Dictionary<Voice, VoiceSamples> _voices = new();
    private readonly List<Composer.Note> _pending = new();
    private readonly List<LoopVoice> _loops = new();
    private readonly List<LoopVoice> _brushes = new();
    private readonly List<LoopVoice> _atmospheres = new();

    private AudioStreamPlaybackPolyphonic _left, _centre, _right;
    private AudioStreamPlayer _leftPlayer, _centrePlayer, _rightPlayer;

    private MusicLibrary.TempoSet _set;
    private Composer _composer;
    private Random _rng;
    private LoopVoice _clock;
    private double _clockLength;
    private double _lastClockPosition;
    private int _clockLoops;
    private int _nextBar;
    private bool _brushesOn;
    private int _brushesUntilBar;
    private int _brushIndex;
    private int _atmosphereIndex;
    private bool _appPaused;

    /// <summary>True once the streams are loaded and the music has started.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>Current position on the bar clock, in beats since the music started.</summary>
    public double Beat { get; private set; }

    public int Bpm => _set?.Bpm ?? 0;
    public string TempoSetName => _set?.Name ?? "";
    public int SetCount => MusicLibrary.Sets.Length;
    public int BrushesCount => _brushes.Count;
    public bool BrushesOn => _brushesOn;
    public string ChordName => Composer.ChordAt(Math.Max(0, _nextBar - 1)).Name;

    public int NotesPlayed { get; private set; }
    public int NotesDropped { get; private set; }
    public int NotesRejected { get; private set; }

    /// <summary>Every loop file the director loads, for the import-settings test.</summary>
    public string[] LoopPaths() =>
        MusicLibrary.Sets.SelectMany(s => s.Brushes.Concat(s.Atmospheres)).Append(MusicLibrary.Drone)
            .Select(l => l.Path).Distinct().ToArray();

    /// <summary>MIDI notes a voice has loaded recordings for, by voice name (e.g. "Runs").</summary>
    public int[] SampleNotes(string voice) =>
        Enum.TryParse(voice, out Voice v) && _voices.TryGetValue(v, out VoiceSamples samples) ? samples.Paths.Keys.ToArray() : Array.Empty<int>();

    /// <summary>
    /// Composes <paramref name="bars"/> bars with a fresh composer and returns each note as
    /// "voice midi pitchScale" (pitch 0 when the voice has no samples), without playing anything.
    /// Lets tests inspect the composition directly.
    /// </summary>
    public string[] PreviewNotes(int bars, int seed)
    {
        var composer = new Composer(seed, Bpm > 0 ? Bpm : 67, SampleNotes(nameof(Voice.Runs)));
        var notes = new List<Composer.Note>();
        for (int bar = 0; bar < bars; bar++) composer.ComposeBar(bar, false, notes);
        return notes.Select(n => $"{n.Voice} {n.Midi} {Resolve(n.Voice, n.Midi).Pitch:0.000}").ToArray();
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _leftPlayer = AddNotePlayer(NotesLeftBus);
        _centrePlayer = AddNotePlayer(NotesBus);
        _rightPlayer = AddNotePlayer(NotesRightBus);

        foreach (MusicLibrary.Instrument instrument in MusicLibrary.Instruments)
            VoiceFor(instrument.Voice, instrument.GainDb).Paths[instrument.RootMidi] = instrument.Path;
        foreach (MusicLibrary.SampledInstrument instrument in MusicLibrary.SampledInstruments)
            ScanNotes(instrument);

        // Load off the Godot thread; the music starts once everything is in.
        IEnumerable<string> paths = _voices.Values.SelectMany(v => v.Paths.Values).Concat(LoopPaths());
        foreach (string path in paths.Distinct())
        {
            Error request = ResourceLoader.LoadThreadedRequest(path);
            if (request == Error.Ok) _loading.Add(path);
            else GD.PushError($"Music: cannot load {path}: {request}");
        }
    }

    public override void _Process(double delta)
    {
        if (_loading.Count > 0)
        {
            PollLoading();
            return;
        }
        if (!IsPlaying || _appPaused) return;

        Beat = Math.Max(Beat, ReadClockBeats());
        while (Beat + LookaheadBeats >= _nextBar * Composer.BeatsPerBar)
            StartBar(_nextBar++);
        PlayDueNotes();
        UpdateFades(delta);
    }

    public override void _Notification(int what)
    {
        // A backgrounded phone app must go quiet, and resume where it left off.
        if (what == NotificationApplicationPaused) SetAppPaused(true);
        else if (what == NotificationApplicationResumed) SetAppPaused(false);
    }

    /// <summary>Restarts the soundtrack with a given tempo set and seed. Starts at random on launch.</summary>
    public void Restart(int setIndex, int seed)
    {
        if (_loading.Count > 0)
        {
            GD.PushError("Music: Restart called before the streams finished loading.");
            return;
        }
        if (!_streams.ContainsKey(MusicLibrary.Drone.Path))
        {
            GD.PushError($"Music: {MusicLibrary.Drone.Path} is missing; it is the clock the composer follows.");
            return;
        }
        foreach (LoopVoice voice in _loops) voice.Player.QueueFree();
        _loops.Clear();
        _brushes.Clear();
        _atmospheres.Clear();
        _pending.Clear();

        _set = MusicLibrary.Sets[Math.Clamp(setIndex, 0, MusicLibrary.Sets.Length - 1)];
        _rng = new Random(seed);
        _composer = new Composer(seed, _set.Bpm, SampleNotes(nameof(Voice.Runs)));
        ConfigureEcho(_set.Bpm);

        foreach (MusicLibrary.Loop loop in _set.Brushes)
            if (_streams.ContainsKey(loop.Path)) _brushes.Add(AddLoop(loop, BrushesLayerDb));
        foreach (MusicLibrary.Loop loop in _set.Atmospheres)
            if (_streams.ContainsKey(loop.Path)) _atmospheres.Add(AddLoop(loop, AtmosphereLayerDb));
        _clock = AddLoop(MusicLibrary.Drone, 0);

        _clockLength = _clock.Player.Stream.GetLength();
        _clockLoops = 0;
        _lastClockPosition = 0;
        Beat = 0;
        _nextBar = 0;
        _brushesOn = false;
        _brushesUntilBar = BrushesFirstBar;
        _brushIndex = -1;
        _atmosphereIndex = 0;
        if (_atmospheres.Count > 0) FadeTo(_atmospheres[0], 1, OpeningFade);
        FadeTo(_clock, 1, OpeningFade);

        // All loops start in the same frame so timed ones share a downbeat; untimed ones start anywhere,
        // except the drone, whose position is the clock.
        foreach (LoopVoice voice in _loops)
        {
            UpdateVolume(voice);
            bool untimed = voice.Loop.Bpm == 0 && voice != _clock;
            voice.Player.Play(untimed ? (float)(_rng.NextDouble() * voice.Player.Stream.GetLength()) : 0);
        }
        foreach (AudioStreamPlayer player in new[] { _leftPlayer, _centrePlayer, _rightPlayer })
            if (!player.Playing) player.Play();
        _left = (AudioStreamPlaybackPolyphonic)_leftPlayer.GetStreamPlayback();
        _centre = (AudioStreamPlaybackPolyphonic)_centrePlayer.GetStreamPlayback();
        _right = (AudioStreamPlaybackPolyphonic)_rightPlayer.GetStreamPlayback();
        SetAppPaused(_appPaused);
        IsPlaying = true;
    }

    private VoiceSamples VoiceFor(Voice voice, float gainDb)
    {
        if (!_voices.TryGetValue(voice, out VoiceSamples samples)) _voices[voice] = samples = new VoiceSamples();
        samples.GainDb = gainDb;
        return samples;
    }

    /// <summary>Adds every file in the instrument's folder whose name is a note.</summary>
    private void ScanNotes(MusicLibrary.SampledInstrument instrument)
    {
        // ListDirectory, unlike DirAccess, still sees the original file names in an exported build.
        VoiceSamples samples = VoiceFor(instrument.Voice, instrument.GainDb);
        foreach (string file in ResourceLoader.ListDirectory(instrument.Folder))
        {
            if (!AudioExtensions.Contains(System.IO.Path.GetExtension(file).ToLowerInvariant())) continue;
            if (MusicLibrary.TryParseNote(file, out int midi)) samples.Paths[midi] = instrument.Folder + file;
            else GD.PushWarning($"Music: {instrument.Folder}{file} is not named after a note (like C3 or F#3); skipped.");
        }
        if (samples.Paths.Count == 0) GD.PushWarning($"Music: no notes found in {instrument.Folder}.");
    }

    private void PollLoading()
    {
        for (int i = _loading.Count - 1; i >= 0; i--)
        {
            string path = _loading[i];
            ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(path);
            if (status == ResourceLoader.ThreadLoadStatus.InProgress) continue;
            if (status == ResourceLoader.ThreadLoadStatus.Loaded && ResourceLoader.LoadThreadedGet(path) is AudioStream stream)
                _streams[path] = stream;
            else
                GD.PushError($"Music: failed to load {path}.");
            _loading.RemoveAt(i);
        }
        if (_loading.Count > 0) return;

        foreach (VoiceSamples samples in _voices.Values)
            foreach (int midi in samples.Paths.Where(p => !_streams.ContainsKey(p.Value)).Select(p => p.Key).ToArray())
                samples.Paths.Remove(midi);
        foreach (string path in LoopPaths())
            if (_streams.GetValueOrDefault(path) is AudioStreamWav { LoopMode: AudioStreamWav.LoopModeEnum.Disabled })
                GD.PushError($"Music: {path} does not loop. Set Loop Mode to Forward in its import settings.");

        Restart(Random.Shared.Next(MusicLibrary.Sets.Length), Random.Shared.Next());
    }

    private AudioStreamPlayer AddNotePlayer(string bus)
    {
        var player = new AudioStreamPlayer { Stream = new AudioStreamPolyphonic { Polyphony = 32 }, Bus = bus };
        AddChild(player);
        return player;
    }

    private LoopVoice AddLoop(MusicLibrary.Loop loop, float layerDb)
    {
        var player = new AudioStreamPlayer
        {
            Stream = _streams[loop.Path],
            Bus = BedsBus,
            PitchScale = loop.Bpm == 0 ? 1 : _set.Bpm / (float)loop.Bpm,
        };
        AddChild(player);
        var voice = new LoopVoice { Loop = loop, Player = player, BaseDb = MixDb + layerDb + loop.TrimDb };
        _loops.Add(voice);
        return voice;
    }

    /// <summary>Dotted-eighth echo, as on the Sound Kit page.</summary>
    private static void ConfigureEcho(int bpm)
    {
        int bus = AudioServer.GetBusIndex(NotesBus);
        for (int i = 0; bus >= 0 && i < AudioServer.GetBusEffectCount(bus); i++)
            if (AudioServer.GetBusEffect(bus, i) is AudioEffectDelay delay)
                delay.FeedbackDelayMs = (float)(.75 * 60000.0 / bpm);
    }

    /// <summary>Beats elapsed, from the clock loop's position plus the audio not yet reported by the mixer.</summary>
    private double ReadClockBeats()
    {
        // The mixer gap is capped so a long hitch or a resume cannot push the clock ahead of the audio.
        double position = _clock.Player.GetPlaybackPosition() + Math.Min(AudioServer.GetTimeSinceLastMix(), .1);
        if (position < _lastClockPosition - _clockLength / 2) _clockLoops++;
        _lastClockPosition = position;
        return (_clockLoops * _clockLength + position) * _set.Bpm / 60.0;
    }

    private void StartBar(int bar)
    {
        if (_brushes.Count > 0 && bar >= _brushesUntilBar)
        {
            _brushesOn = !_brushesOn;
            _brushesUntilBar = bar + SectionBars[_rng.Next(SectionBars.Length)];
            if (_brushesOn) _brushIndex = (_brushIndex + 1) % _brushes.Count;
        }
        for (int i = 0; i < _brushes.Count; i++)
        {
            bool on = _brushesOn && i == _brushIndex;
            FadeTo(_brushes[i], on ? 1 : 0, on ? BrushesFadeIn : BrushesFadeOut);
        }

        if (bar > 0 && bar % AtmosphereBars == 0 && _atmospheres.Count > 1)
        {
            _atmosphereIndex = (_atmosphereIndex + 1) % _atmospheres.Count;
            for (int i = 0; i < _atmospheres.Count; i++)
                FadeTo(_atmospheres[i], i == _atmosphereIndex ? 1 : 0, AtmosphereCrossfade);
        }

        int from = _pending.Count;
        _composer.ComposeBar(bar, _brushesOn, _pending);
        if (_pending.Count > from) _pending.Sort((a, b) => a.Beat.CompareTo(b.Beat));
    }

    private void PlayDueNotes()
    {
        int due = 0;
        while (due < _pending.Count && _pending[due].Beat <= Beat) due++;
        for (int i = 0; i < due; i++)
        {
            Composer.Note note = _pending[i];
            if (Beat - note.Beat > LateBeats) NotesDropped++;
            else PlayNote(note);
        }
        _pending.RemoveRange(0, due);
    }

    /// <summary>The recording nearest to <paramref name="midi"/> and the pitch shift that reaches it; pitch 0 if the voice has none.</summary>
    private (AudioStream Stream, float Pitch, float GainDb) Resolve(Voice voice, int midi)
    {
        if (!_voices.TryGetValue(voice, out VoiceSamples samples) || samples.Paths.Count == 0) return (null, 0, 0);
        int nearest = samples.Paths.Keys.MinBy(root => Math.Abs(root - midi));
        _streams.TryGetValue(samples.Paths[nearest], out AudioStream stream);
        return (stream, Mathf.Pow(2f, (midi - nearest) / 12f), samples.GainDb);
    }

    private void PlayNote(Composer.Note note)
    {
        (AudioStream stream, float pitch, float gainDb) = Resolve(note.Voice, note.Midi);
        if (stream == null) return;
        (AudioStreamPlaybackPolyphonic playback, string bus) = note.Pan switch
        {
            < -.25f => (_left, NotesLeftBus),
            > .25f => (_right, NotesRightBus),
            _ => (_centre, NotesBus),
        };
        float volumeDb = MixDb + gainDb + Mathf.LinearToDb(note.Velocity);
        long id = playback.PlayStream(stream, 0, volumeDb, pitch, AudioServer.PlaybackType.Default, bus);
        if (id == AudioStreamPlaybackPolyphonic.InvalidId) NotesRejected++;
        else NotesPlayed++;
    }

    private static void FadeTo(LoopVoice voice, double target, double seconds)
    {
        voice.Target = target;
        voice.FadeSeconds = seconds;
    }

    private void UpdateFades(double delta)
    {
        foreach (LoopVoice voice in _loops)
        {
            if (Math.Abs(voice.Gain - voice.Target) < 1e-4) voice.Gain = voice.Target;
            // Exponential approach; FadeSeconds is roughly where it sounds finished (three time constants).
            else voice.Gain += (voice.Target - voice.Gain) * (1 - Math.Exp(-3 * delta / voice.FadeSeconds));
            UpdateVolume(voice);
        }
    }

    private static void UpdateVolume(LoopVoice voice) =>
        voice.Player.VolumeDb = voice.BaseDb + (float)Mathf.LinearToDb(Math.Max(voice.Gain, 1e-4));

    private void SetAppPaused(bool paused)
    {
        _appPaused = paused;
        foreach (Node child in GetChildren())
            if (child is AudioStreamPlayer player) player.StreamPaused = paused;
    }
}
