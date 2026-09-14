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

func transition() -> Node:
    return root.get_node("SceneTransition")

# Waits out a scene transition, including the cover lifting and the entrance playing.
func settle():
    while transition().Busy:
        await process_frame
    await create_timer(.7).timeout

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
    check(transition().Busy, "Tapping a difficulty starts the transition immediately")
    await create_timer(.45).timeout
    check(transition().get_node("Overlay").visible, "Loading message covers the scene change")
    await capture("generating")
    await settle()
    check(current_scene.scene_file_path.ends_with("Main.tscn"), "Game scene opens after generation")
    var board = current_scene.get_node("%Board")
    check(not transition().get_node("Overlay").visible, "Transition cover lifts after the puzzle is ready")
    check(board.visible, "Generated board becomes visible after loading")
    var empty = []
    for tile in tiles(board):
        if not tile.Data.IsGiven: empty.append(tile)
    check(empty.size() > 1, "Generated puzzle has editable cells")
    empty[0].emit_signal("Pressed", empty[0].Index)
    board.SetSelectedValue(4)
    await create_timer(.28).timeout
    check(current_scene.get_node("%NumberBar/Digit4").SelectionProgress > .9, "Filled user cell selects its number")
    current_scene.get_node("%NumberBar/Digit4").emit_signal("NumberPressed", 4)
    await create_timer(.28).timeout
    check(board.SelectedUserValue == 0, "Tapping the selected value erases it")
    current_scene.get_node("%NumberBar/Digit4").emit_signal("NumberPressed", 4)
    await create_timer(.28).timeout
    var given_index = -1
    for tile in tiles(board):
        if tile.Data.IsGiven:
            given_index = tile.Index
            break
    board.SelectCell(given_index)
    await create_timer(.28).timeout
    check(current_scene.get_node("%NumberBar/Digit4").SelectionProgress < .1, "Clue cell clears number selection")
    board.SelectCell(empty[1].Index)
    await create_timer(.28).timeout
    check(current_scene.get_node("%NumberBar/Digit4").SelectionProgress < .1, "Empty cell clears number selection")
    board.ClearSelection()
    await create_timer(.28).timeout
    check(current_scene.get_node("%NumberBar/Digit4").SelectionProgress < .1, "Clearing board selection clears number selection")
    empty[0].emit_signal("Pressed", empty[0].Index)
    empty[1].emit_signal("Pressed", empty[1].Index)
    current_scene.get_node("%ModeToggle").button_pressed = true
    current_scene.get_node("%NumberBar/Digit2").emit_signal("NumberPressed", 2)
    await create_timer(.3).timeout
    current_scene.get_node("%NumberBar/Digit7").emit_signal("NumberPressed", 7)
    check(current_scene.get_node("%NumberBar/Digit2").Selected, "Notes mode selects the first pencil mark")
    check(current_scene.get_node("%NumberBar/Digit7").Selected, "Notes mode supports multiple selected pencil marks")
    current_scene.get_node("%NumberBar/Digit2").emit_signal("NumberPressed", 2)
    await create_timer(.28).timeout
    check(not current_scene.get_node("%NumberBar/Digit2").Selected, "Tapping a pencil mark toggles it off")
    current_scene.get_node("%NumberBar/Digit2").emit_signal("NumberPressed", 2)
    await create_timer(.28).timeout
    current_scene.get_node("%NumberBar/Digit7").emit_signal("NumberPressed", 7)
    await create_timer(.28).timeout
    current_scene.get_node("%NumberBar/Digit7").emit_signal("NumberPressed", 7)
    await create_timer(.06).timeout
    var old_key = current_scene.get_node("%NumberBar/Digit2")
    var new_key = current_scene.get_node("%NumberBar/Digit7")
    check(new_key.SelectionProgress > 0 and new_key.SelectionProgress < 1, "Selected bar animates upward")
    check(is_equal_approx(old_key.SelectionProgress, 1), "Other selected notes remain selected")
    await create_timer(.25).timeout
    check(is_equal_approx(new_key.SelectionProgress, 1), "Selected bar reaches full height")
    check(is_equal_approx(old_key.SelectionProgress, 1), "Other selected note remains full height")
    var before = snapshot(board)
    var selected = board.SelectedIndex
    check(board.CanUndo, "Value move recorded")
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
    await settle()
    check(current_scene.get_node("Resume").visible, "Resume is visible after returning")
    await capture("resume")
    current_scene.get_node("Resume").emit_signal("pressed")
    await settle()
    board = current_scene.get_node("%Board")
    check(snapshot(board) == before, "Values, clues and pencil marks survive menu round trip")
    check(board.SelectedIndex == selected, "Selection restored")
    check(current_scene.get_node("%ModeToggle").button_pressed, "Notes mode restored")
    check(board.Undo(), "Undo history survives scene changes")
    check(board.Redo(), "Redo still works")
    check(snapshot(board) == before, "Undo/redo restores the previous values and notes")
    board.EraseSelected()
    for tile in tiles(board):
        if tile.Index == selected:
            check(Array(tile.Data.Hints).is_empty(), "Keyboard erase clears pencil marks")
    for tile in tiles(board):
        if not tile.Data.IsGiven:
            board.SelectCell(tile.Index)
            break
    var actions = current_scene.get_node("%Sheet/Actions").get_global_rect()
    for pressed in [true, false]:
        var tap = InputEventMouseButton.new()
        tap.position = Vector2(actions.get_center().x, actions.position.y - 8)
        tap.button_index = MOUSE_BUTTON_LEFT
        tap.pressed = pressed
        root.push_input(tap, true)
        await process_frame
    check(board.SelectedIndex == -1, "Tapping empty space clears the selection")
    check(board.HasProgress, "Played puzzle has progress to clear")
    check(not current_scene.get_node("%RestartButton").disabled, "Start over is available once there is progress")
    current_scene.get_node("%RestartButton").emit_signal("pressed")
    await create_timer(.3).timeout
    check(current_scene.get_node("%RestartConfirm").IsOpen, "Start over asks for confirmation")
    current_scene.get_node("%RestartConfirm").Close(true)
    await create_timer(.3).timeout
    check(not board.HasProgress and not board.CanUndo and not board.CanRedo, "Start over clears numbers, notes and history")
    for tile in tiles(board):
        if not tile.Data.IsGiven:
            check(tile.Data.Value == 0 and Array(tile.Data.Hints).is_empty(), "Start over empties every non-clue cell")
    check(current_scene.get_node("%RestartButton").disabled, "Start over is unavailable with nothing to clear")
    current_scene.notification(Node.NOTIFICATION_WM_GO_BACK_REQUEST)
    await settle()
    check(current_scene.scene_file_path.ends_with("StartScreen.tscn"), "Android back returns to menu")
    current_scene.get_node("Menu/Difficulties/Easy").emit_signal("pressed")
    await settle()
    board = current_scene.get_node("%Board")
    check(not board.CanUndo, "New puzzle starts with fresh history")
    check(not current_scene.get_node("%ModeToggle").button_pressed, "New puzzle resets notes mode")
    board.emit_signal("Solved")
    await settle()
    check(current_scene.scene_file_path.ends_with("Summary.tscn"), "Completion navigates to summary")
    change_scene_to_file("res://Scenes/StartScreen/StartScreen.tscn")
    await create_timer(1.1).timeout
    check(not current_scene.get_node("Resume").visible, "Completed games cannot be resumed")
    print("GAME FLOW: ", failures, " failures")
    quit(1 if failures else 0)
