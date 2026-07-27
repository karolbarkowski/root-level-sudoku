using System;
using Godot;
using Generators.Sudoku;

namespace SudokuEndless;

/// <summary>
/// Entry screen. Offers one button per difficulty (each starts a fresh game at that level), plus
/// Settings and Quit. Difficulty buttons are generated from the enum so all levels are covered
/// automatically (skipping the non-playable <see cref="SudokuGenerator.Difficulty.Invalid"/>).
/// </summary>
public partial class StartScreen : Control
{
	private const string GameScenePath = "res://Scenes/Game/Main.tscn";
	private const string SettingsScenePath = "res://Scenes/Settings/Settings.tscn";

	public override void _Ready()
	{
		Container difficultyButtons = GetNodeOrNull<Container>("%DifficultyButtons");
		if (difficultyButtons != null)
		{
			foreach (SudokuGenerator.Difficulty difficulty in Enum.GetValues<SudokuGenerator.Difficulty>())
			{
				if (difficulty == SudokuGenerator.Difficulty.Invalid)
				{
					continue;
				}

				SudokuGenerator.Difficulty captured = difficulty;
				var button = new Button
				{
					Text = difficulty.ToString(),
					CustomMinimumSize = new Vector2(220, 44),
				};
				button.Pressed += () => StartGame(captured);
				difficultyButtons.AddChild(button);
			}
		}

		Button settings = GetNodeOrNull<Button>("%SettingsButton");
		if (settings != null)
		{
			settings.Pressed += OnSettings;
		}

		Button quit = GetNodeOrNull<Button>("%QuitButton");
		if (quit != null)
		{
			quit.Pressed += OnQuit;
		}
	}

	private void StartGame(SudokuGenerator.Difficulty difficulty)
	{
		GameSession.Difficulty = difficulty;
		GetTree().ChangeSceneToFile(GameScenePath);
	}

	private void OnSettings() => GetTree().ChangeSceneToFile(SettingsScenePath);

	private void OnQuit() => GetTree().Quit();
}
