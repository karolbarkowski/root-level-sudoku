extends SceneTree

var failures = 0
var output_dir: String

func check(ok: bool, message: String):
    if not ok:
        push_error(message)
        failures += 1

func _initialize():
    run.call_deferred()

func tiles(board: Node) -> Array:
    var result = []
    for box in board.get_node("AspectRatioContainer/Frame/Grid").get_children():
        result.append_array(box.get_children())
    return result

func snapshot(board: Node) -> Array:
    var result = []
    for tile in tiles(board):
        result.append([tile.Data.Value, tile.Data.IsGiven, Array(tile.Data.Hints)])
    return result

func capture(label: String):
    await RenderingServer.frame_post_draw
    check(root.get_texture().get_image().save_png(output_dir.path_join(label + ".png")) == OK, "Screenshot failed")

func run():
    output_dir = ProjectSettings.globalize_path("res://").path_join("../output/game-redesign").simplify_path()
    DirAccess.make_dir_recursive_absolute(output_dir)
    change_scene_to_file("res://Scenes/StartScreen/StartScreen.tscn")
    await create_timer(1.1).timeout
    check(not current_scene.get_node("Resume").visible, "No resume before starting a game")
    check(not current_scene.has_node("Hero/Nine"), "Title numeral should be removed")
    await capture("start")
    current_scene.get_node("Menu/Difficulties/Easy").emit_signal("pressed")
    await create_timer(1.5).timeout
    var board = current_scene.get_node("%Board")
    var empty = []
    for tile in tiles(board):
        if not tile.Data.IsGiven: empty.append(tile)
    check(empty.size() > 1, "Generated puzzle has editable cells")
    empty[0].emit_signal("Pressed", empty[0].Index)
    board.SetSelectedValue(4)
    empty[1].emit_signal("Pressed", empty[1].Index)
    current_scene.get_node("%ModeToggle").button_pressed = true
    current_scene.get_node("%NumberBar/Digit2").emit_signal("NumberPressed", 2)
    await create_timer(.3).timeout
    current_scene.get_node("%NumberBar/Digit7").emit_signal("NumberPressed", 7)
    await create_timer(.06).timeout
    var old_key = current_scene.get_node("%NumberBar/Digit2")
    var new_key = current_scene.get_node("%NumberBar/Digit7")
    check(new_key.SelectionProgress > 0 and new_key.SelectionProgress < 1, "Selected bar animates upward")
    check(old_key.SelectionProgress > 0 and old_key.SelectionProgress < 1, "Previous bar animates downward")
    await create_timer(.25).timeout
    check(is_equal_approx(new_key.SelectionProgress, 1), "Selected bar reaches full height")
    check(is_zero_approx(old_key.SelectionProgress), "Previous bar returns to short height")
    var before = snapshot(board)
    var selected = board.SelectedIndex
    check(board.CanUndo, "Value move recorded")
    current_scene.get_node("%PauseButton").emit_signal("pressed")
    check(not board.visible and current_scene.get_node("%PausePanel").visible, "Pause hides the puzzle")
    check(current_scene.get_node("%ModeToggle").disabled, "Pause disables editing")
    current_scene.get_node("%NumberBar/Digit3").emit_signal("NumberPressed", 3)
    check(snapshot(board) == before, "Paused puzzle ignores digit entry")
    await capture("paused")
    current_scene.get_node("%PausePanel/Resume").emit_signal("pressed")
    check(board.visible, "Resume reveals the same puzzle")
    check(snapshot(board) == before, "Pause preserves values and pencil marks")
    var counts = board.GetValueCounts()
    for key in current_scene.get_node("%NumberBar").get_children():
        check(key.Remaining == max(0, 9-counts[key.Number]), "Number strip shows remaining counts")
    for size in [Vector2i(540,1220), Vector2i(360,780), Vector2i(280,640), Vector2i(768,1024)]:
        root.size = size
        await create_timer(.4).timeout
        var sheet = current_scene.get_node("%Sheet")
        check(abs(current_scene.get_node("%NumberBar").get_global_rect().end.y - current_scene.size.y) < 1, "Number bars must meet the bottom screen edge")
        check(sheet.get_global_rect().end.x <= current_scene.size.x + 1, "Game exceeds viewport width")
        check(sheet.get_global_rect().end.y <= current_scene.size.y + 1, "Game exceeds viewport height")
        var rect = board.get_global_rect()
        var header = sheet.get_node("Navigation").get_global_rect()
        var actions = sheet.get_node("Actions").get_global_rect()
        check(abs((rect.position.y - header.end.y) - (actions.position.y - rect.end.y)) < 1, "Board is centered between header and controls")
        check(not current_scene.has_node("%Status"), "Instruction text is removed")
        check(abs(rect.size.x - rect.size.y) < 1, "Board must remain square")
        check(board.Textures.CellGap * board.get_global_transform_with_canvas().get_scale().x >= .9, "Cell rules stay visible at narrow sizes")
        await capture("game-%dx%d" % [size.x,size.y])
    current_scene.get_node("%BackButton").emit_signal("pressed")
    await create_timer(1.1).timeout
    check(current_scene.get_node("Resume").visible, "Resume is visible after returning")
    await capture("resume")
    current_scene.get_node("Resume").emit_signal("pressed")
    await create_timer(1.1).timeout
    board = current_scene.get_node("%Board")
    check(snapshot(board) == before, "Values, clues and pencil marks survive menu round trip")
    check(board.SelectedIndex == selected, "Selection restored")
    check(current_scene.get_node("%ModeToggle").button_pressed, "Notes mode restored")
    check(board.Undo(), "Undo history survives scene changes")
    check(board.Redo(), "Redo still works")
    check(snapshot(board) == before, "Undo/redo restores the previous values and notes")
    current_scene.get_node("%EraseButton").emit_signal("pressed")
    for tile in tiles(board):
        if tile.Index == selected:
            check(Array(tile.Data.Hints).is_empty(), "Erase clears pencil marks")
    current_scene.notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
    await create_timer(1.1).timeout
    check(current_scene.scene_file_path.ends_with("StartScreen.tscn"), "Android back returns to menu")
    current_scene.get_node("Menu/Difficulties/Easy").emit_signal("pressed")
    await create_timer(1.1).timeout
    board = current_scene.get_node("%Board")
    check(not board.CanUndo, "New puzzle starts with fresh history")
    check(not current_scene.get_node("%ModeToggle").button_pressed, "New puzzle resets notes mode")
    board.emit_signal("Solved")
    await create_timer(.3).timeout
    check(current_scene.scene_file_path.ends_with("Summary.tscn"), "Completion navigates to summary")
    change_scene_to_file("res://Scenes/StartScreen/StartScreen.tscn")
    await create_timer(1.1).timeout
    check(not current_scene.get_node("Resume").visible, "Completed games cannot be resumed")
    print("GAME FLOW: ", failures, " failures")
    quit(1 if failures else 0)
