using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Generators.Sudoku;
using Sudoku;

namespace SudokuEndless;

public partial class StartScreen : Control, ITransitionScreen
{
	[ExportGroup("Entrance")]

	/// <summary>Whole menu cascade, from the first row leaving to the last one settling.</summary>
	[Export] public float MenuDuration { get; set; } = .5f;

	/// <summary>Optional delay before the title enters alongside the menu.</summary>
	[Export] public float TitleDelay { get; set; }

	[Export] public float TitleDuration { get; set; } = .3f;

	[ExportGroup("Exit")]

	/// <summary>Whole exit cascade. The transition cover starts fading in partway through.</summary>
	[Export] public float ExitDuration { get; set; } = .25f;

	/// <summary>Share of <see cref="MenuDuration"/> one row spends moving; the rest is stagger.</summary>
	private const float MenuRowShare = .6f;

	// The poster, in design units. Its height is the sum of its parts rather than a number to keep
	// in sync by hand, so changing the padding re-fits the whole sheet instead of cropping it.
	private const float PosterWidth = 540;
	private const float SidePadding = 32;
	private const float EdgePadding = 40;
	private const float HeroHeight = 320;
	private const float HeroGap = 10;
	private const float MenuHeight = 570;
	private const float PosterHeight = (EdgePadding * 2) + HeroHeight + HeroGap + MenuHeight;

	private Control _background;
	private Control _hero;
	private VBoxContainer _menu;
	private bool _leaving;
	private Tween _entry;
	private PaperButton _resume;
	private PaperButton _chosen;
	public override void _Ready()
	{
		// The game is portrait-only. Keep one logical poster for desktop and Android.
		GetWindow().MinSize = new Vector2I(280, 480);
		GetWindow().ContentScaleSize = new Vector2I(540, 1220);
		_background = GetNode<Control>("PaperBackground");
		_background.SetAnchorsPreset(LayoutPreset.TopLeft);
		_hero = GetNode<Control>("Hero");
		_menu = GetNode<VBoxContainer>("Menu");
		_resume = GetNode<PaperButton>("Resume");
		_resume.Visible = GameSession.HasPuzzle;
		_resume.Pressed += () => Navigate(_resume, SceneTransition.GamePath, resume: true);
		_menu.GetNode<Label>("Heading/Caption").Text = GameSession.HasPuzzle ? "START A NEW PUZZLE" : "CHOOSE YOUR DIFFICULTY";
		var rows = _menu.GetNode<VBoxContainer>("Difficulties");
		var scene = GD.Load<PackedScene>("res://UI/Paper/PaperButton.tscn");
		foreach (SudokuGenerator.Difficulty difficulty in Enum.GetValues<SudokuGenerator.Difficulty>())
		{
			if (difficulty == SudokuGenerator.Difficulty.Invalid) continue;
			var button = scene.Instantiate<PaperButton>();
			button.Caption = difficulty.ToString();
			button.Index = ((int)difficulty).ToString("00");
			button.Name = difficulty.ToString();
			button.Pressed += () => Navigate(button, SceneTransition.GamePath, difficulty);
			rows.AddChild(button);
		}
		var settings = _menu.GetNode<PaperButton>("Footer/Settings");
		settings.Pressed += () => Navigate(settings, SceneTransition.SettingsPath);
		var quit = _menu.GetNode<PaperButton>("Footer/Quit");
		quit.Pressed += () => Navigate(quit, null);
		Resized += Layout;
		Layout();
		if (!SceneTransition.IsTransitioning) PlayEntryWithoutTransition();
		// Keep every button neutral until the player uses the pointer or keyboard.
	}

	private void Layout()
	{
		// Scale one complete vertical poster. Never shrink the title independently
		// of the controls.
		float resumeHeight = _resume.Visible ? 108 : 0;
		float height = PosterHeight + resumeHeight;
		float scale = Mathf.Min(Size.X / PosterWidth, Size.Y / height);
		Vector2 origin = new Vector2((Size.X - PosterWidth * scale) / 2, Mathf.Min(24, (Size.Y - height * scale) / 2));
		const float column = PosterWidth - (SidePadding * 2);
		// Sized here rather than by its own full-rect anchors: on Android those never resolve
		// against this root and the sheet collapses to 0x0, leaving the bare clear colour.
		_background.Position = Vector2.Zero;
		_background.Size = Size;
		_hero.Position = origin + new Vector2(SidePadding, EdgePadding) * scale;
		_hero.Size = new Vector2(column, HeroHeight);
		_hero.Scale = Vector2.One * scale;
		_resume.Position = origin + new Vector2(SidePadding, EdgePadding + HeroHeight + 20) * scale;
		_resume.Size = new Vector2(column, 80);
		_resume.Scale = Vector2.One * scale;
		_menu.Position = origin + new Vector2(SidePadding, EdgePadding + HeroHeight + HeroGap + resumeHeight) * scale;
		_menu.Size = new Vector2(column, MenuHeight);
		_menu.Scale = Vector2.One * scale;
	}
	/// <summary>One slot per menu row. The spacer has nothing to show, so it does not take a slot.</summary>
	private List<Control> Rows()
	{
		var rows = new List<Control>();
		if (_resume.Visible) rows.Add(_resume);
		foreach (Node child in _menu.GetChildren())
		{
			if (child.Name == "Difficulties")
				foreach (Control row in child.GetChildren()) rows.Add(row);
			else if (child is Control control && control.Name != "Space") rows.Add(control);
		}
		return rows;
	}

	/// <summary>Parks the title and every row in their pre-entrance state: drawn, but invisible.</summary>
	private void PrepareEntry()
	{
		foreach (Control row in Rows())
		{
			// Near-zero alpha keeps drawing active to warm fonts/textures without a visible flash.
			row.Modulate = new Color(1, 1, 1, .001f);
			foreach (PaperButton button in ButtonsOf(row)) button.PrepareEntrance();
		}
		((PaperHero)_hero).PrepareEntrance();
	}

	public Task PrepareRevealAsync()
	{
		if (UiAnimationSettings.Default.Enabled) PrepareEntry();
		return Task.CompletedTask;
	}

	/// <summary>
	/// The title slides into place while the menu rises row by row.
	/// </summary>
	public void PlayEntry()
	{
		if (!UiAnimationSettings.Default.Enabled || _leaving) return;
		List<Control> rows = Rows();

		// Rows overlap: one row's travel plus the whole stagger has to fit inside MenuDuration.
		double rowDuration = MenuDuration * MenuRowShare;
		double step = rows.Count > 1 ? (MenuDuration - rowDuration) / (rows.Count - 1) : 0;

		_entry?.Kill();
		_entry = CreateTween().SetParallel();
		for (int i = 0; i < rows.Count; i++)
		{
			Control row = rows[i];
			double delay = i * step;
			// Only PaperButtons can offset themselves past their container, so the plain rows
			// (rules, headings, hint) arrive on alpha alone.
			foreach (PaperButton button in ButtonsOf(row)) button.PlayEntrance(delay, rowDuration);
			_entry.TweenProperty(row, "modulate:a", 1f, rowDuration)
				.SetDelay(delay).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		}

		((PaperHero)_hero).PlayTitle(TitleDelay, TitleDuration);
	}

	/// <summary>Launched directly (the app's first screen), there is no cover to wait behind.</summary>
	private async void PlayEntryWithoutTransition()
	{
		if (!UiAnimationSettings.Default.Enabled) return;
		PrepareEntry();

		// Do not spend the animation budget loading the initial GPU frame.
		// Start on the following process frame, once the prepared screen has been drawn.
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		if (!IsInstanceValid(this) || !IsInsideTree()) return;
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (!IsInstanceValid(this) || !IsInsideTree()) return;
		PlayEntry();
	}

	/// <summary>
	/// Rows sink away from the bottom up while the title slides back out. The chosen row keeps its
	/// highlight and leaves last, so the tap reads as the cause of what follows.
	/// </summary>
	public async Task PlayExitAsync()
	{
		if (!UiAnimationSettings.Default.Enabled) return;
		List<Control> rows = Rows();
		double rowDuration = ExitDuration * MenuRowShare;
		double step = rows.Count > 1 ? (ExitDuration - rowDuration) / (rows.Count - 1) : 0;

		_entry?.Kill();
		_entry = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		for (int i = 0; i < rows.Count; i++)
		{
			Control row = rows[rows.Count - 1 - i];
			bool chosen = _chosen != null && (row == _chosen || _chosen.GetParent() == row);
			double delay = chosen ? ExitDuration - rowDuration : i * step;
			foreach (PaperButton button in ButtonsOf(row)) button.PlayExit(delay, rowDuration);
			_entry.TweenProperty(row, "modulate:a", 0f, rowDuration).SetDelay(delay);
		}
		((PaperHero)_hero).PlayExit(0, ExitDuration);
		await ToSignal(GetTree().CreateTimer(ExitDuration), SceneTreeTimer.SignalName.Timeout);
	}

	/// <summary>The row itself when it is a button, plus any it wraps (the footer holds two).</summary>
	private static IEnumerable<PaperButton> ButtonsOf(Control row)
	{
		if (row is PaperButton button) yield return button;
		foreach (Node child in row.GetChildren())
			if (child is PaperButton nested) yield return nested;
	}

	private async void Navigate(PaperButton source, string path, SudokuGenerator.Difficulty difficulty = SudokuGenerator.Difficulty.Easy, bool resume = false)
	{
		if (_leaving || SceneTransition.IsTransitioning) return;
		_leaving = true;
		_chosen = source;
		source?.HoldHighlight();
		if (path == null)
		{
			if (UiAnimationSettings.Default.Enabled)
				await ToSignal(GetTree().CreateTimer(.10), SceneTreeTimer.SignalName.Timeout);
			if (IsInstanceValid(this) && IsInsideTree()) GetTree().Quit();
			return;
		}

		bool started = path == SceneTransition.GamePath
			? resume ? SceneTransition.GoTo(path, "LOADING BOARD") : SceneTransition.StartNewGame(difficulty)
			: SceneTransition.GoTo(path);
		if (!started)
		{
			_leaving = false;
			_chosen = null;
			source?.ReleaseHighlight();
		}
	}

	public override void _ExitTree()
	{
		Resized -= Layout;
		_entry?.Kill();
	}
}
