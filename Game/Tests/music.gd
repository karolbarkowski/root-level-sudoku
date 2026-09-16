extends SceneTree
# Checks the generative soundtrack end to end with the dummy audio driver, which still mixes in real time:
#   Godot --path Game --headless --script res://Tests/music.gd
# Takes about half a minute (a minute when a set has brushes, which only come in after eight bars).

var failures = 0

func check(ok: bool, message: String):
    if not ok:
        push_error(message)
        failures += 1

func _initialize():
    run.call_deferred()

func music() -> Node:
    return root.get_node("Music")

func wait(seconds: float):
    await create_timer(seconds, true).timeout

func run():
    check_buses()
    check_loops()

    var started = Time.get_ticks_msec()
    while not music().IsPlaying and Time.get_ticks_msec() - started < 10000:
        await process_frame
    check(music().IsPlaying, "music never started")
    if not music().IsPlaying:
        quit(1)
        return

    check_runs()
    for index in music().SetCount:
        await check_clock(index)
    await check_pause()
    await check_scene_change()
    await check_brushes()

    print("music: %d failure(s)" % failures)
    quit(failures)

func check_buses():
    var expected = {
        "Music": ["AudioEffectHardLimiter"],
        "MusicNotes": ["AudioEffectDelay", "AudioEffectReverb"],
        "MusicNotesL": ["AudioEffectPanner"],
        "MusicNotesR": ["AudioEffectPanner"],
        "MusicBeds": ["AudioEffectLowPassFilter"],
    }
    for bus in expected:
        var index = AudioServer.get_bus_index(bus)
        check(index >= 0, "missing bus %s" % bus)
        if index < 0:
            continue
        var effects = []
        for i in AudioServer.get_bus_effect_count(index):
            effects.append(AudioServer.get_bus_effect(index, i).get_class())
        check(effects == expected[bus], "bus %s has effects %s, expected %s" % [bus, effects, expected[bus]])
    check(AudioServer.get_bus_send(AudioServer.get_bus_index("MusicNotesL")) == "MusicNotes", "left notes must feed MusicNotes")
    check(AudioServer.get_bus_send(AudioServer.get_bus_index("MusicBeds")) == "Music", "beds must feed Music")

# Timed loops must loop and last exactly two or four bars at their own tempo.
func check_loops():
    var bpm_pattern = RegEx.create_from_string("\\[(\\d+)\\]")
    for path in music().LoopPaths():
        var stream = load(path) as AudioStreamWAV
        check(stream != null, "cannot load %s" % path)
        if stream == null:
            continue
        check(stream.loop_mode == AudioStreamWAV.LOOP_FORWARD, "%s does not loop forward" % path)
        var found = bpm_pattern.search(path.get_file())
        if found:
            var bar = 240.0 / float(found.get_string(1))
            var bars = stream.get_length() / bar
            check(abs(bars - round(bars)) * bar < .002, "%s is %.4f bars, not a whole number" % [path, bars])

# The bar clock follows the audio: beats advance at the set's tempo and notes are played on time.
func check_clock(index: int):
    music().Restart(index, 1234)
    await wait(.5)
    var played = music().NotesPlayed
    var dropped = music().NotesDropped
    var beat0 = music().Beat
    var t0 = Time.get_ticks_usec()
    await wait(8.0)
    var seconds = (Time.get_ticks_usec() - t0) / 1e6
    var beats = music().Beat - beat0
    var expected = seconds * music().Bpm / 60.0
    check(abs(beats - expected) < .35, "set %s: %.2f beats in %.2f s, expected %.2f" % [music().TempoSetName, beats, seconds, expected])
    check(music().NotesPlayed > played, "set %s played no notes in 8 s" % music().TempoSetName)
    check(music().NotesDropped == dropped, "set %s dropped late notes" % music().TempoSetName)
    check(music().NotesRejected == 0, "polyphony exhausted")
    print("music: set %s at %d BPM, %.2f beats in %.2f s, %d notes" % [music().TempoSetName, music().Bpm, beats, seconds, music().NotesPlayed - played])

# A backgrounded app stops the clock and picks up where it was.
func check_pause():
    music().notification(Node.NOTIFICATION_APPLICATION_PAUSED)
    await wait(.3)
    var frozen = music().Beat
    await wait(1.5)
    check(abs(music().Beat - frozen) < .01, "clock moved while the app was paused")
    var dropped = music().NotesDropped
    music().notification(Node.NOTIFICATION_APPLICATION_RESUMED)
    await wait(2.0)
    check(music().Beat > frozen + 1.0, "clock did not resume")
    check(music().NotesDropped == dropped, "notes burst or dropped after resume")

func check_scene_change():
    var beat = music().Beat
    change_scene_to_file("res://Scenes/StartScreen/StartScreen.tscn")
    await wait(3.0)
    check(music().IsPlaying and music().Beat > beat + 2.0, "music did not keep playing through a scene change")

# The Sounds folder is scanned by note name, and the runs voice plays its notes at their recorded pitch.
func check_runs():
    check(Array(music().SampleNotes("Runs")) == [48, 50, 52, 54, 55, 57, 59], "HollowTree should load C3 D3 E3 F#3 G3 A3 B3, got %s" % [music().SampleNotes("Runs")])
    var lydian = [0, 2, 4, 6, 7, 9, 11]
    var runs = 0
    for line in music().PreviewNotes(64, 7):
        var parts = line.split(" ")
        var midi = int(parts[1])
        var pitch = float(parts[2])
        check(pitch > 0, "%s has no sample to play" % line)
        check(lydian.has(midi % 12), "%s is outside C Lydian" % line)
        if parts[0] == "Runs":
            runs += 1
            check(is_equal_approx(pitch, 1.0), "%s is pitch-shifted instead of using its own recording" % line)
    check(runs >= 40, "only %d run notes in 64 bars" % runs)
    print("music: %d run notes in 64 bars" % runs)

# Brushes stay out for the first eight bars, then come in.
func check_brushes():
    if music().BrushesCount == 0:
        return
    music().Restart(1, 99)
    await wait(1.0)
    check(not music().BrushesOn, "brushes should not play in the opening bars")
    var bar_seconds = 240.0 / music().Bpm
    await wait(bar_seconds * 8.5 - 1.0)
    check(music().BrushesOn, "brushes did not come in after eight bars")
