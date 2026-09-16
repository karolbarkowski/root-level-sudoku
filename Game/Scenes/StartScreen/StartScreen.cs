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

	[ExportGroup("Settings")]

	/// <summary>Menu rows (or settings rows) leaving when the two swap.</summary>
	[Export] public float SwapOutDuration { get; set; } = .16f;

	/// <summary>The other set of rows arriving once the first has gone.</summary>
	[Export] public float SwapInDuration { get; set; } = .3f;

	/// <summary>Share of <see cref="MenuDuration"/> one row spends moving; the rest is stagger.</summary>
	private const float MenuRowShare = .6f;

	// The poster, in design units. Its height is the sum of its parts rather than a number to keep
	// in sync by hand, so changing the padding re-fits the whole sheet instead of cropping it.
	private const float PosterWidth = 540;
	private const float SidePadding = 32;
	private const float EdgePadding = 40;
	private const float HeroHeight = 320;
	private const float HeroGap = 24;
	private const float ResumeHeight = 80;
	private const float ResumeGap = 28;

	private const string SupportUrl = "https://buymeacoffee.com/rootlevelit";

	private Control _background;
	private Control _hero;
	private VBoxContainer _menu;
	private bool _leaving;
	private Tween _entry;
	private PaperButton _resume;
	private PaperButton _chosen;
	private PaperButton _settingsButton;
	private PaperButton _back;
	private VBoxContainer _settingsMenu;
	private SettingsView _settingsView;
	private bool _hasResume;
	private bool _showingSettings;
	private bool _swapping;
	private bool _previousQuitOnGoBack;

	/// <summary>True while the settings replace the menu (or are on their way in).</summary>
	public bool ShowingSettings => _showingSettings;
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
		_hasResume = GameSession.HasPuzzle;
		_resume.Visible = _hasResume;
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
		_settingsButton = _menu.GetNode<PaperButton>("Footer/Settings");
		_settingsButton.Pressed += ShowSettings;
		BuildSettings(scene);
		_previousQuitOnGoBack = GetTree().QuitOnGoBack;
		var quit = _menu.GetNode<PaperButton>("Footer/Quit");
		quit.Pressed += () => Navigate(quit, null);
		_menu.GetNode<PaperButton>("Support").Pressed += () => OS.ShellOpen(SupportUrl);
		Resized += Layout;
		Layout();
		if (!SceneTransition.IsTransitioning) PlayEntryWithoutTransition();
		// Keep every button neutral until the player uses the pointer or keyboard.
	}

	/// <summary>
	/// The settings section: same heading style as the menu, the settings rows, and Back. It shares
	/// the menu's column and scale, so the title does not move when the two swap.
	/// </summary>
	private void BuildSettings(PackedScene buttonScene)
	{
		_settingsMenu = new VBoxContainer { Name = "SettingsMenu", Visible = false };
		_settingsMenu.AddThemeConstantOverride("separation", 12);
		AddChild(_settingsMenu);
		var heading = (Control)_menu.GetNode("Heading").Duplicate();
		heading.GetNode<Label>("Caption").Text = "SETTINGS";
		_settingsMenu.AddChild(heading);
		_settingsView = new SettingsView { Name = "View", Separation = 20 };
		_settingsMenu.AddChild(_settingsView);
		_settingsMenu.AddChild(new Control { Name = "Space", CustomMinimumSize = new Vector2(0, 28), MouseFilter = MouseFilterEnum.Ignore });
		_back = buttonScene.Instantiate<PaperButton>();
		_back.Name = "Back";
		_back.Caption = "Back";
		_back.Secondary = true;
		_back.LeadingIcon = GD.Load<Texture2D>("res://Resources/icons/arrow-left.svg");
		_back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		_back.Pressed += ShowMenu;
		_settingsMenu.AddChild(_back);
		// After AddChild: PaperButton._Ready resets the minimum size. Caption plus icon plus side padding.
		_back.CustomMinimumSize = new Vector2(PaperStyle.Body.GetStringSize("Back", fontSize: _back.CaptionSize).X + 140, 66);
	}

	private void Layout()
	{
		// Scale one complete vertical poster. Never shrink the title independently
		// of the controls. The title sits at the top; the actions (continue and the menu) form one
		// section held to the bottom edge, and any spare height opens up between the two.
		// Sized for the taller of the menu and the settings, so swapping them never rescales the title.
		// Both sections hold to the bottom edge on their own.
		const float column = PosterWidth - (SidePadding * 2);
		float resumeHeight = _hasResume ? ResumeHeight + ResumeGap : 0;
		float menuHeight = _menu.GetCombinedMinimumSize().Y;
		float menuSection = resumeHeight + menuHeight;
		float settingsHeight = _settingsMenu.GetCombinedMinimumSize().Y;
		float sectionHeight = Mathf.Max(menuSection, settingsHeight);
		float height = (EdgePadding * 2) + HeroHeight + HeroGap + sectionHeight;
		float scale = Mathf.Min(Size.X / PosterWidth, Size.Y / height);
		Vector2 origin = new Vector2((Size.X - PosterWidth * scale) / 2, 0);
		Vector2 section = new Vector2(origin.X, Size.Y - (EdgePadding + menuSection) * scale);
		// Sized here rather than by its own full-rect anchors: on Android those never resolve
		// against this root and the sheet collapses to 0x0, leaving the bare clear colour.
		_background.Position = Vector2.Zero;
		_background.Size = Size;
		_hero.Position = origin + new Vector2(SidePadding, EdgePadding) * scale;
		_hero.Size = new Vector2(column, HeroHeight);
		_hero.Scale = Vector2.One * scale;
		_resume.Position = section + new Vector2(SidePadding, 0) * scale;
		_resume.Size = new Vector2(column, ResumeHeight);
		_resume.Scale = Vector2.One * scale;
		_menu.Position = section + new Vector2(SidePadding, resumeHeight) * scale;
		_menu.Size = new Vector2(column, menuHeight);
		_menu.Scale = Vector2.One * scale;
		_settingsMenu.Position = new Vector2(origin.X + SidePadding * scale, Size.Y - (EdgePadding + settingsHeight) * scale);
		_settingsMenu.Size = new Vector2(column, settingsHeight);
		_settingsMenu.Scale = Vector2.One * scale;
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

	/// <summary>The settings section's rows, in the same shape as <see cref="Rows"/>.</summary>
	private List<Control> SettingsRows()
	{
		var rows = new List<Control>();
		foreach (Node child in _settingsMenu.GetChildren())
		{
			if (child == _settingsView)
				foreach (Control row in _settingsView.GetChildren()) rows.Add(row);
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
		CascadeIn(Rows(), MenuDuration);
		((PaperHero)_hero).PlayTitle(TitleDelay, TitleDuration);
	}

	/// <summary>Rows rise into place one after another, top first, all within <paramref name="duration"/>.</summary>
	private void CascadeIn(List<Control> rows, double duration)
	{
		// Rows overlap: one row's travel plus the whole stagger has to fit inside the duration.
		double rowDuration = duration * MenuRowShare;
		double step = rows.Count > 1 ? (duration - rowDuration) / (rows.Count - 1) : 0;

		_entry?.Kill();
		_entry = CreateTween().SetParallel();
		for (int i = 0; i < rows.Count; i++)
		{
			Control row = rows[i];
			double delay = i * step;
			// Only PaperButtons can offset themselves past their container, so the plain rows
			// (rules, headings, settings) arrive on alpha alone.
			foreach (PaperButton button in ButtonsOf(row)) button.PlayEntrance(delay, rowDuration);
			_entry.TweenProperty(row, "modulate:a", 1f, rowDuration)
				.SetDelay(delay).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		}
	}

	/// <summary>Rows sink away bottom first, all within <paramref name="duration"/>.</summary>
	private void CascadeOut(List<Control> rows, double duration)
	{
		double rowDuration = duration * MenuRowShare;
		double step = rows.Count > 1 ? (duration - rowDuration) / (rows.Count - 1) : 0;

		_entry?.Kill();
		_entry = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		for (int i = 0; i < rows.Count; i++)
		{
			Control row = rows[rows.Count - 1 - i];
			foreach (PaperButton button in ButtonsOf(row)) button.PlayExit(i * step, rowDuration);
			_entry.TweenProperty(row, "modulate:a", 0f, rowDuration).SetDelay(i * step);
		}
	}

	public void ShowSettings() => Swap(toSettings: true);

	public void ShowMenu() => Swap(toSettings: false);

	/// <summary>
	/// Replaces the menu with the settings, or back. The title stays; the current rows sink away,
	/// then the other rows rise in. Keyboard focus follows to the matching button.
	/// </summary>
	private async void Swap(bool toSettings)
	{
		if (_leaving || _swapping || SceneTransition.IsTransitioning || toSettings == _showingSettings) return;
		_swapping = true;
		_showingSettings = toSettings;
		// Android back closes the settings instead of quitting while they are up.
		if (toSettings) GetTree().QuitOnGoBack = false;
		PaperButton source = toSettings ? _settingsButton : _back;
		bool keyboard = source.HasFocus();
		source.HoldHighlight();

		if (UiAnimationSettings.Default.Enabled)
		{
			CascadeOut(toSettings ? Rows() : SettingsRows(), SwapOutDuration);
			await ToSignal(GetTree().CreateTimer(SwapOutDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || !IsInsideTree()) return;
		}

		source.ReleaseHighlight();
		_menu.Visible = !toSettings;
		_resume.Visible = !toSettings && _hasResume;
		_settingsMenu.Visible = toSettings;
		if (!toSettings) GetTree().QuitOnGoBack = _previousQuitOnGoBack;
		List<Control> incoming = toSettings ? SettingsRows() : Rows();
		if (UiAnimationSettings.Default.Enabled)
		{
			foreach (Control row in incoming) row.Modulate = new Color(1, 1, 1, .001f);
			CascadeIn(incoming, SwapInDuration);
		}
		else
		{
			foreach (Control row in incoming) row.Modulate = Colors.White;
		}
		if (keyboard) (toSettings ? _back : _settingsButton).GrabFocus();
		_swapping = false;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMGoBackRequest && _showingSettings) ShowMenu();
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (!_showingSettings || !@event.IsActionPressed("ui_cancel")) return;
		ShowMenu();
		GetViewport().SetInputAsHandled();
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
		if (_leaving || _swapping || _showingSettings || SceneTransition.IsTransitioning) return;
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
		GetTree().QuitOnGoBack = _previousQuitOnGoBack;
	}
}
