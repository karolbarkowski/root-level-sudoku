extends SceneTree
var failures = 0
func check(ok: bool, message: String):
    if not ok:
        push_error(message)
        failures += 1
func _initialize():
    run.call_deferred()
func run():
    root.size = Vector2i(540,1220)
    var output_dir = ProjectSettings.globalize_path("res://").path_join("../output/start-screen/entry").simplify_path()
    DirAccess.make_dir_recursive_absolute(output_dir)
    var scene = load("res://Scenes/StartScreen/StartScreen.tscn").instantiate()
    root.add_child(scene)
    current_scene = scene
    var hero = scene.get_node("Hero")
    var nine = hero.get_node("Nine")
    var initial_x = nine.position.x
    check(hero.modulate.a < .01, "Entrance must be prepared before the first draw")
    var first_row = scene.get_node("Menu/Difficulties/Easy")
    var last_row = scene.get_node("Menu/Difficulties/Beyond")
    for frame in range(24):
        await RenderingServer.frame_post_draw
        if frame == 0:
            check(hero.modulate.a < .01, "Initial render must precede the animation")
        if frame == 6:
            check(hero.modulate.a > .01 and hero.modulate.a < 1, "Hero must visibly fade during entry")
            check(nine.position.x < initial_x, "Numeral must move toward its resting position")
            check(first_row.modulate.a > last_row.modulate.a, "Buttons must enter in a cascade")
        if frame in [0, 6, 12, 23]:
            root.get_texture().get_image().save_png(output_dir.path_join("frame-%02d.png" % frame))
    check(is_equal_approx(hero.modulate.a, 1), "Hero must finish its fade")
    check(is_equal_approx(last_row.modulate.a, 1), "Last row must finish its fade")
    var expected_x = 171.0 * min(hero.size.x / 480.0, hero.size.y / 600.0)
    check(abs(nine.position.x - expected_x) < .1, "Numeral must finish at the original position")
    # Fresh instances must replay the entrance, including returns from other scenes.
    var packed = load("res://Scenes/StartScreen/StartScreen.tscn")
    scene.free()
    scene = packed.instantiate()
    root.add_child(scene)
    current_scene = scene
    check(scene.get_node("Hero").modulate.a < .01, "Re-entering the scene must replay the entrance")
    await create_timer(.4).timeout
    scene.free()
    var settings = load("res://Resources/UiAnimationDefault.tres")
    settings.set("Enabled", false)
    scene = packed.instantiate()
    root.add_child(scene)
    current_scene = scene
    check(scene.get_node("Hero").modulate.a == 1, "Disabled motion must show hero immediately")
    check(scene.get_node("Menu/Difficulties/Easy").modulate.a == 1, "Disabled motion must show buttons immediately")
    settings.set("Enabled", true)
    print("PAPER ENTRY: ", failures, " failures; first-frame preparation, visible motion, cascade, completion, replay and disabled motion checked.")
    quit(1 if failures else 0)
