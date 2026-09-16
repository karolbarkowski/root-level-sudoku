extends SceneTree
var failures = 0
func check(ok: bool, message: String):
    if not ok:
        push_error(message)
        failures += 1
func _initialize():
    run.call_deferred()
func run():
    DirAccess.remove_absolute(ProjectSettings.globalize_path("user://session.json"))
    Engine.max_fps = 60
    var packed = load("res://Scenes/StartScreen/StartScreen.tscn")
    var settings = load("res://Resources/UiAnimationDefault.tres")
    settings.Enabled = true
    for replay in range(2):
        var scene = packed.instantiate()
        root.add_child(scene)
        current_scene = scene
        var hero = scene.get_node("Hero")
        var first = scene.get_node("Menu/Difficulties/Easy")
        var last = scene.get_node("Menu/Difficulties/Expert")
        check(hero.modulate.a < .01, "Entry must be prepared on every scene visit")
        # Cold-start rendering can consume the first timer delta while the UI is still warming.
        # Observe the actual entrance, whose budget begins after that prepared frame.
        var deadline = Time.get_ticks_msec() + 2000
        while hero.modulate.a <= .0011 and Time.get_ticks_msec() < deadline:
            await process_frame
        await create_timer(.08).timeout
        check(hero.modulate.a > .01 and hero.modulate.a < 1, "Title fades visibly")
        check(first.modulate.a > last.modulate.a, "Rows enter in a cascade")
        await create_timer(.8).timeout
        check(hero.modulate.a > .99 and last.modulate.a > .99, "Entry finishes within one second")
        scene.free()
    settings.Enabled = false
    var scene = packed.instantiate()
    root.add_child(scene)
    current_scene = scene
    check(scene.get_node("Hero").modulate.a == 1, "Disabled motion shows title immediately")
    check(scene.get_node("Menu/Difficulties/Easy").modulate.a == 1, "Disabled motion shows buttons immediately")
    settings.Enabled = true
    print("PAPER ENTRY: ", failures, " failures")
    quit(1 if failures else 0)
