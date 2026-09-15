extends SceneTree
# A puzzle must survive the app being killed. Runs as two processes, because only a real restart
# proves the game came back from disk:
#   Godot --path Game --audio-driver Dummy --script res://Tests/persistence.gd -- --phase=play
#   Godot --path Game --audio-driver Dummy --script res://Tests/persistence.gd -- --phase=resume
# The play phase kills its own process, so nothing can save on the way out.

const SAVE = "user://session.json"
const EXPECTED = "user://persistence_expected.json"

var failures = 0

func check(ok: bool, message: String):
    if not ok:
        push_error(message)
        failures += 1

func _initialize():
    run.call_deferred()

func transition() -> Node:
    return root.get_node("SceneTransition")

func settle():
    await create_timer(.1).timeout
    while transition().Busy:
        await process_frame
    await create_timer(.7).timeout

func tiles(board: Node) -> Array:
    var result = []
    for box in board.get_node("AspectRatioContainer/Frame/Grid").get_children():
        result.append_array(box.get_children())
    return result

func snapshot(scene: Node) -> Dictionary:
    var board = scene.get_node("%Board")
    var ordered = []
    for tile in tiles(board):
        ordered.append([tile.Index, tile.Data.Value, tile.Data.IsGiven, Array(tile.Data.Hints)])
    ordered.sort_custom(func(a, b): return a[0] < b[0])
    return {
        "cells": ordered,
        "selected": board.SelectedIndex,
        "notes": scene.get_node("%ModeToggle").button_pressed,
        "difficulty": scene.get_node("%Difficulty").text,
        "can_undo": board.CanUndo,
        "can_redo": board.CanRedo,
    }

func run():
    var phase = ""
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--phase="):
            phase = arg.substr(8)
    if phase == "play":
        await play()
    elif phase == "resume":
        await resume()
    else:
        push_error("Pass -- --phase=play, then -- --phase=resume")
        quit(2)

func play():
    DirAccess.remove_absolute(ProjectSettings.globalize_path(SAVE))
    DirAccess.remove_absolute(ProjectSettings.globalize_path(EXPECTED))
    change_scene_to_file("res://Scenes/StartScreen/StartScreen.tscn")
    await create_timer(1.0).timeout
    current_scene.get_node("Menu/Difficulties/Medium").emit_signal("pressed")
    await settle()
    var board = current_scene.get_node("%Board")
    var empty = []
    for tile in tiles(board):
        if not tile.Data.IsGiven:
            empty.append(tile.Index)
    # A value, a value that gets undone (so redo has something), pencil marks, then a selection.
    board.SelectCell(empty[0])
    board.SetSelectedValue(3)
    board.SelectCell(empty[1])
    board.SetSelectedValue(8)
    board.Undo()
    current_scene.get_node("%ModeToggle").button_pressed = true
    board.SelectCell(empty[2])
    for n in [1, 5, 9]:
        current_scene.get_node("%NumberBar/Digit" + str(n)).emit_signal("NumberPressed", n)
    board.SelectCell(empty[3])
    await create_timer(.2).timeout
    var expected = snapshot(current_scene)
    var file = FileAccess.open(EXPECTED, FileAccess.WRITE)
    file.store_string(JSON.stringify(expected))
    file.close()
    print("PERSISTENCE play: state recorded, killing the process")
    OS.kill(OS.get_process_id())

func resume():
    check(FileAccess.file_exists(EXPECTED), "Play phase must run first")
    var expected = JSON.parse_string(FileAccess.get_file_as_string(EXPECTED))
    change_scene_to_file("res://Scenes/StartScreen/StartScreen.tscn")
    await create_timer(1.0).timeout
    check(current_scene.get_node("Resume").visible, "Continue is offered after the app was killed")
    current_scene.get_node("Resume").emit_signal("pressed")
    await settle()
    check(current_scene.scene_file_path.ends_with("Main.tscn"), "Continue opens the game")
    var actual = JSON.parse_string(JSON.stringify(snapshot(current_scene)))
    for key in expected:
        check(actual[key] == expected[key], "Restored %s matches (expected %s, got %s)" % [key, expected[key], actual[key]])
    var board = current_scene.get_node("%Board")
    check(board.Redo() and board.Undo() and board.Undo(), "Undo/redo history works after restoring")
    # Leave nothing behind for the next run.
    board.emit_signal("Solved")
    await create_timer(.5).timeout
    check(not FileAccess.file_exists(SAVE), "Solving removes the save")
    DirAccess.remove_absolute(ProjectSettings.globalize_path(EXPECTED))
    print("PERSISTENCE: ", failures, " failures")
    quit(1 if failures else 0)
