using System;
using System.Collections.Generic;
using Godot;
using Generators.Sudoku;
using Sudoku;

namespace SudokuEndless;

public partial class StartScreen : Control
{
	[ExportGroup("Entrance")]

	/// <summary>Whole menu cascade, from the first row leaving to the last one settling.</summary>
	[Export] public float MenuDuration { get; set; } = .5f;

	/// <summary>Pause between the menu settling and the numeral starting. Zero runs them back to back.</summary>
	[Export] public float NineDelay { get; set; }

	[Export] public float NineDuration { get; set; } = .3f;

	/// <summary>Pause between the numeral settling and the title starting. Zero runs them back to back.</summary>
	[Export] public float TitleDelay { get; set; }

	[Export] public float TitleDuration { get; set; } = .3f;

	/// <summary>Share of <see cref="MenuDuration"/> one row spends moving; the rest is stagger.</summary>
	private const float MenuRowShare = .6f;

	// The poster, in design units. Its height is the sum of its parts rather than a number to keep
	// in sync by hand, so changing the padding re-fits the whole sheet instead of cropping it.
	private const float PosterWidth = 540;
	private const float SidePadding = 32;
	private const float EdgePadding = 40;
	private const float HeroHeight = 600;
	private const float HeroGap = 10;
	private const float MenuHeight = 570;
	private const float PosterHeight = (EdgePadding * 2) + HeroHeight + HeroGap + MenuHeight;

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
		float scale = Mathf.Min(Size.X / PosterWidth, Size.Y / PosterHeight);
		Vector2 origin = (Size - new Vector2(PosterWidth, PosterHeight) * scale) / 2;
		const float column = PosterWidth - (SidePadding * 2);
		// Sized here rather than by its own full-rect anchors: on Android those never resolve
		// against this root and the sheet collapses to 0x0, leaving the bare clear colour.
		_background.Position = Vector2.Zero;
		_background.Size = Size;
		_hero.Position = origin + new Vector2(SidePadding, EdgePadding) * scale;
		_hero.Size = new Vector2(column, HeroHeight);
		_hero.Scale = Vector2.One * scale;
		_menu.Position = origin + new Vector2(SidePadding, EdgePadding + HeroHeight + HeroGap) * scale;
		_menu.Size = new Vector2(column, MenuHeight);
		_menu.Scale = Vector2.One * scale;
	}
	/// <summary>
	/// Three beats, in order: the menu rises row by row, then the numeral slides in from the right,
	/// then the title from the left. Each beat waits for the previous one to settle, so the gaps are
	/// measured from the end of the beat before rather than from a shared zero.
	/// </summary>
	private async void PlayEntry()
	{
		if (!UiAnimationSettings.Default.Enabled) return;
		var hero = (PaperHero)_hero;

		// One slot per menu row. The spacer has nothing to show, so it does not take a slot.
		var targets = new List<Control>();
		foreach (Node child in _menu.GetChildren())
		{
			if (child.Name == "Difficulties")
				foreach (Control row in child.GetChildren()) targets.Add(row);
			else if (child is Control control && control.Name != "Space") targets.Add(control);
		}

		// Rows overlap: one row's travel plus the whole stagger has to fit inside MenuDuration.
		double rowDuration = MenuDuration * MenuRowShare;
		double step = targets.Count > 1 ? (MenuDuration - rowDuration) / (targets.Count - 1) : 0;

		foreach (Control target in targets)
		{
			// Near-zero alpha keeps drawing active to warm fonts/textures without a visible flash.
			target.Modulate = new Color(1, 1, 1, .001f);
			foreach (PaperButton button in ButtonsOf(target)) button.PrepareEntrance();
		}
		hero.PrepareEntrance();

		// Do not spend the animation budget loading the initial GPU frame.
		// Start on the following process frame, once the prepared screen has been drawn.
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		if (!IsInsideTree()) return;
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (!IsInsideTree() || _leaving) return;

		_entry = CreateTween().SetParallel();
		for (int i = 0; i < targets.Count; i++)
		{
			Control target = targets[i];
			double delay = i * step;
			// Only PaperButtons can offset themselves past their container, so the plain rows
			// (rules, headings, hint) arrive on alpha alone.
			foreach (PaperButton button in ButtonsOf(target)) button.PlayEntrance(delay, rowDuration);
			_entry.TweenProperty(target, "modulate:a", 1f, rowDuration)
				.SetDelay(delay).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		}

		double nine = MenuDuration + NineDelay;
		hero.PlayNine(nine, NineDuration);
		hero.PlayTitle(nine + NineDuration + TitleDelay, TitleDuration);
	}

	/// <summary>The row itself when it is a button, plus any it wraps (the footer holds two).</summary>
	private static IEnumerable<PaperButton> ButtonsOf(Control row)
	{
		if (row is PaperButton button) yield return button;
		foreach (Node child in row.GetChildren())
			if (child is PaperButton nested) yield return nested;
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
