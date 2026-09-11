using System;
using System.Collections.Generic;
using Godot;
using Generators.Sudoku;
using Sudoku;

namespace SudokuEndless;

public partial class StartScreen : Control
{
	private Control _background;
	private Control _hero;
	private VBoxContainer _menu;
	private bool _leaving;
	private Tween _entry;
	public override void _Ready()
	{
		// The game is portrait-only. Keep one logical poster for desktop and Android.
		GetWindow().MinSize = new Vector2I(280, 480);
		GetWindow().ContentScaleSize = new Vector2I(540, 1220);
		_background = GetNode<Control>("PaperBackground");
		_hero = GetNode<Control>("Hero");
		_menu = GetNode<VBoxContainer>("Menu");
		var rows = _menu.GetNode<VBoxContainer>("Difficulties");
		var scene = GD.Load<PackedScene>("res://UI/Paper/PaperButton.tscn");
		foreach (SudokuGenerator.Difficulty difficulty in Enum.GetValues<SudokuGenerator.Difficulty>())
		{
			if (difficulty == SudokuGenerator.Difficulty.Invalid) continue;
			var button = scene.Instantiate<PaperButton>();
			button.Caption = difficulty.ToString();
			button.Index = ((int)difficulty).ToString("00");
			button.Name = difficulty.ToString();
			button.Pressed += () => Navigate("res://Scenes/Game/Main.tscn", difficulty);
			rows.AddChild(button);
		}
		_menu.GetNode<PaperButton>("Footer/Settings").Pressed += () => Navigate("res://Scenes/Settings/Settings.tscn");
		_menu.GetNode<PaperButton>("Footer/Quit").Pressed += () => Navigate(null);
		Resized += Layout;
		Layout();
		PlayEntry();
		// Keep every button neutral until the player uses the pointer or keyboard.
	}

	private void Layout()
	{
		// Scale one complete vertical poster. Never shrink the title independently
		// of the controls.
		float scale = Mathf.Min(Size.X / 540f, Size.Y / 1220f);
		Vector2 origin = (Size - new Vector2(540, 1220) * scale) / 2;
		// Sized here rather than by its own full-rect anchors: on Android those never resolve
		// against this root and the sheet collapses to 0x0, leaving the bare clear colour.
		_background.Position = Vector2.Zero;
		_background.Size = Size;
		_hero.Position = origin + new Vector2(32, 20) * scale;
		_hero.Size = new Vector2(476, 600);
		_hero.Scale = Vector2.One * scale;
		_menu.Position = origin + new Vector2(32, 630) * scale;
		_menu.Size = new Vector2(476, 570);
		_menu.Scale = Vector2.One * scale;
	}
	private async void PlayEntry()
	{
		if (!UiAnimationSettings.Default.Enabled) return;
		var targets = new List<(Control Control, double Delay)> { (_hero, 0) };
		int index = 0;
		foreach (Node child in _menu.GetChildren())
		{
			if (child.Name == "Difficulties")
				foreach (Control row in child.GetChildren()) targets.Add((row, .02 + index++ * .012));
			else if (child is Control control) targets.Add((control, .06));
		}
		foreach (var target in targets)
		{
			// Near-zero alpha keeps drawing active to warm fonts/textures without a visible flash.
			target.Control.Modulate = new Color(1, 1, 1, .001f);
			if (target.Control is PaperButton button) button.PrepareEntrance();
			if (target.Control.Name == "Footer")
				foreach (PaperButton secondary in target.Control.GetChildren()) secondary.PrepareEntrance();
		}
		((PaperHero)_hero).PrepareEntrance();

		// Do not spend the 280 ms animation budget loading the initial GPU frame.
		// Start on the following process frame, once the prepared screen has been drawn.
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		if (!IsInsideTree()) return;
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (!IsInsideTree() || _leaving) return;
		_entry = CreateTween().SetParallel();
		((PaperHero)_hero).PlayEntrance();
		foreach (var target in targets) Fade(target.Control, target.Delay);
	}

	private void Fade(Control target, double delay)
	{
		if (target is PaperButton button) button.PlayEntrance(delay);
		if (target.Name == "Footer")
			foreach (PaperButton secondary in target.GetChildren()) secondary.PlayEntrance(delay);
		// Last row: 68 ms delay + 210 ms motion/fade = 278 ms; hero ends at 280 ms.
		_entry.TweenProperty(target, "modulate:a", 1f, .21).SetDelay(delay).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}

	private async void Navigate(string path, SudokuGenerator.Difficulty difficulty = SudokuGenerator.Difficulty.Easy)
	{
		if (_leaving) return;
		_leaving = true;
		if (UiAnimationSettings.Default.Enabled)
			await ToSignal(GetTree().CreateTimer(.10), SceneTreeTimer.SignalName.Timeout);
		if (!IsInsideTree()) return;
		if (path == null) { GetTree().Quit(); return; }
		if (path.Contains("/Game/")) GameSession.Difficulty = difficulty;
		Error error = GetTree().ChangeSceneToFile(path);
		if (error != Error.Ok) { _leaving = false; GD.PushError($"Cannot open {path}: {error}"); }
	}

	public override void _ExitTree()
	{
		Resized -= Layout;
		_entry?.Kill();
	}
}
