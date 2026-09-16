extends SceneTree
# Renders the settings as shown on the start screen and checks the volume sliders:
#   Godot --path Game --audio-driver Dummy --script res://Tests/settings_preview.gd

func _initialize():
    run.call_deferred()

func run():
    root.size = Vector2i(540, 1220)
    change_scene_to_file("res://Scenes/StartScreen/StartScreen.tscn")
    await create_timer(1).timeout
    current_scene.ShowSettings()
    await create_timer(.8).timeout
    await RenderingServer.frame_post_draw
    root.get_texture().get_image().save_png(ProjectSettings.globalize_path("res://../output/settings-preview.png"))
    var sliders = current_scene.get_node("SettingsMenu").find_children("*", "HSlider", true, false)
    assert(sliders.size() == 2, "Both audio settings need volume sliders")
    for slider in sliders:
        assert(slider.min_value == 1 and slider.max_value == 10 and slider.step == 1)
    print("Settings preview: two volume sliders verified.")
    quit()
