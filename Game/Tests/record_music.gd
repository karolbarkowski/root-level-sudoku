extends SceneTree
# Records what the soundtrack sounds like, one WAV per tempo set, for tuning the mix without a device:
#   Godot --path Game --headless --script res://Tests/record_music.gd -- --seconds=75 [--mute=MusicNotes]
# Writes output/music/<set>.wav next to the Game folder. Runs in real time. --mute silences a bus,
# e.g. MusicNotes to hear only the loops or MusicBeds to hear only the notes.

func _initialize():
    run.call_deferred()

func run():
    var seconds = 75.0
    var suffix = ""
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--seconds="):
            seconds = float(arg.substr(10))
        elif arg.begins_with("--mute="):
            var bus = arg.substr(7)
            AudioServer.set_bus_mute(AudioServer.get_bus_index(bus), true)
            suffix = "-no-" + bus.to_lower()

    var music = root.get_node("Music")
    while not music.IsPlaying:
        await process_frame

    var recorder = AudioEffectRecord.new()
    AudioServer.add_bus_effect(0, recorder)
    var output_dir = ProjectSettings.globalize_path("res://").path_join("../output/music").simplify_path()
    DirAccess.make_dir_recursive_absolute(output_dir)

    for index in music.SetCount:
        music.Restart(index, 2026)
        recorder.set_recording_active(true)
        await create_timer(seconds).timeout
        recorder.set_recording_active(false)
        var path = output_dir.path_join("%s-%dbpm%s.wav" % [music.TempoSetName.to_lower(), music.Bpm, suffix])
        recorder.get_recording().save_to_wav(path)
        print("recorded %s (%d notes so far)" % [path, music.NotesPlayed])
    quit()
