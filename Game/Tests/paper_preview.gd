extends SceneTree
# Run with Godot --path Game --script res://Tests/paper_preview.gd.
# Captures real rendered layouts and verifies navigation/focus without touching saved puzzles.
var failures = 0
func check(ok: bool, message: String):
    if not ok:
        push_error(message)
        failures += 1
func _initialize():
    run.call_deferred()
func run():
    var output_dir = ProjectSettings.globalize_path("res://").path_join("../output/start-screen").simplify_path()
    check(DirAccess.make_dir_recursive_absolute(output_dir) == OK, "Preview output directory must be writable")
    var screen = load("res://Scenes/StartScreen/StartScreen.tscn").instantiate()
    root.add_child(screen)
    current_scene = screen
    var sizes = [Vector2i(540,1220), Vector2i(360,780), Vector2i(280,640), Vector2i(746,1311), Vector2i(768,1024)]
    var labels = ["portrait", "phone", "narrow", "user-window", "tablet"]
    for i in sizes.size():
        root.size = sizes[i]
        await create_timer(1.1).timeout
        var rows = screen.get_node("Menu/Difficulties")
        check(root.size == sizes[i], "Window must allow requested size at " + labels[i])
        check(not rows.get_child(0).has_focus(), "Easy must not be focused by default")
        var hero_rect = screen.get_node("Hero").get_global_rect()
        var menu_rect = screen.get_node("Menu").get_global_rect()
        check(hero_rect.end.y < menu_rect.position.y, "Hero must stay above menu at " + labels[i])
        check(abs(hero_rect.size.x - menu_rect.size.x) < 1, "Title and menu must scale together")
        check(screen.get_node("PaperBackground") is ColorRect, "Background must be a flat dark surface")
        check(rows.get_child_count() == 5, "All five difficulties must exist")
        for row in rows.get_children():
            check(row.get_global_rect().end.x <= screen.size.x + 1, "Button exceeds width at " + labels[i])
            check(row.get_global_rect().end.y <= screen.size.y + 1, "Button exceeds height at " + labels[i])
            check(row.modulate.a > .99, "Entry must finish within one second")
        var footer = screen.get_node("Menu/Footer")
        check(footer.get_global_rect().end.y <= screen.size.y + 1, "Footer clipped at " + labels[i])
        await RenderingServer.frame_post_draw
        check(root.get_texture().get_image().save_png(output_dir.path_join(labels[i] + ".png")) == OK, "Screenshot must save")
    var settings = screen.get_node("Menu/Footer/Settings")
    settings.grab_focus()
    check(settings.has_focus(), "Keyboard focus must reach Settings")
    var down = InputEventAction.new()
    down.action = "ui_accept"
    down.pressed = true
    Input.parse_input_event(down)
    await create_timer(.08).timeout
    var up = InputEventAction.new()
    up.action = "ui_accept"
    up.pressed = false
    Input.parse_input_event(up)
    await create_timer(.3).timeout
    check(current_scene.scene_file_path.ends_with("Settings.tscn"), "Keyboard activation must navigate to Settings")
    current_scene.get_node("%BackButton").emit_signal("pressed")
    await create_timer(1.1).timeout
    check(current_scene.scene_file_path.ends_with("StartScreen.tscn"), "Settings Back must restore start screen")
    var easy = current_scene.get_node("Menu/Difficulties/Easy")
    var point = easy.get_global_rect().get_center()
    var move = InputEventMouseMotion.new()
    move.position = point
    root.push_input(move, true)
    await create_timer(.12).timeout
    var click = InputEventMouseButton.new()
    click.position = point
    click.button_index = MOUSE_BUTTON_LEFT
    click.pressed = true
    root.push_input(click, true)
    await create_timer(.08).timeout
    click = InputEventMouseButton.new()
    click.position = point
    click.button_index = MOUSE_BUTTON_LEFT
    click.pressed = false
    root.push_input(click, true)
    await create_timer(.4).timeout
    check(current_scene.scene_file_path.ends_with("Main.tscn"), "Mouse click on Easy must start gameplay")
    var board = current_scene.get_node_or_null("%Board")
    check(board != null, "Gameplay board must exist after selecting Easy")
    if board != null:
        var counts = board.GetValueCounts()
        var givens = 0
        for number in counts:
            givens += number
        check(givens > 0 and givens < 81, "New game must contain a generated, unfinished puzzle")
    print("PAPER UI: ", failures, " failures; five portrait layouts, neutral Easy, image assets, keyboard Settings/Back and mouse Easy-to-puzzle navigation checked.")
    quit(1 if failures else 0)


