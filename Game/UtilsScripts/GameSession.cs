using System;
using System.Collections.Generic;
using Generators.Sudoku;
using Godot;
using SudokuEndless;
namespace Sudoku;

/// <summary>
/// The live puzzle, including its undo history and pencil marks. It survives scene changes in
/// memory and app restarts on disk: <see cref="Save"/> writes it after every change, because a
/// mobile app is often killed without warning, and the first read after launch loads it back.
/// </summary>
public static class GameSession
{
	private const string SavePath = "user://session.json";
	private const string TempPath = "user://session.json.tmp";
	private const int SaveVersion = 1;

	private static bool _loaded;
	private static SudokuGenerator.Difficulty _difficulty = SudokuGenerator.Difficulty.Easy;
	private static Board _activeBoard;
	private static CellData[] _cells;
	private static int _selectedIndex = -1;
	private static bool _notesMode;

	public static SudokuGenerator.Difficulty Difficulty
	{
		get { EnsureLoaded(); return _difficulty; }
		set { EnsureLoaded(); _difficulty = value; }
	}

	public static Board ActiveBoard
	{
		get { EnsureLoaded(); return _activeBoard; }
		set { EnsureLoaded(); _activeBoard = value; }
	}

	public static CellData[] Cells
	{
		get { EnsureLoaded(); return _cells; }
		set { EnsureLoaded(); _cells = value; }
	}

	public static int SelectedIndex
	{
		get { EnsureLoaded(); return _selectedIndex; }
		set { EnsureLoaded(); _selectedIndex = value; }
	}

	public static bool NotesMode
	{
		get { EnsureLoaded(); return _notesMode; }
		set { EnsureLoaded(); _notesMode = value; }
	}

	public static bool HasPuzzle => ActiveBoard != null;

	/// <summary>Forgets the puzzle, in memory and on disk. The difficulty is kept for the next one.</summary>
	public static void Clear()
	{
		EnsureLoaded();
		_activeBoard = null;
		_cells = null;
		_selectedIndex = -1;
		_notesMode = false;
		DeleteSave();
	}

	/// <summary>
	/// Installs a freshly generated board; its non-empty cells become the clues. Call on the Godot
	/// thread — <see cref="CellData"/> is a Resource.
	/// </summary>
	public static void Start(Board board, SudokuGenerator.Difficulty difficulty)
	{
		var cells = new CellData[BoardGeometry.CellCount];
		for (int i = 0; i < cells.Length; i++)
		{
			int value = board[BoardGeometry.RowOf(i), BoardGeometry.ColOf(i)];
			cells[i] = new CellData { Value = value, IsGiven = value != 0 };
		}
		Clear();
		_difficulty = difficulty;
		_activeBoard = board;
		_cells = cells;
		Save();
	}

	/// <summary>
	/// Writes the current puzzle to disk, or removes the save when there is none. The file is written
	/// beside the save and then renamed over it, so a process killed mid-write leaves the previous
	/// save intact instead of a truncated one.
	/// </summary>
	public static void Save()
	{
		if (Engine.IsEditorHint()) return;
		EnsureLoaded();
		if (_activeBoard == null || _cells == null)
		{
			DeleteSave();
			return;
		}

		var givens = new char[BoardGeometry.CellCount];
		var hints = new Godot.Collections.Array();
		for (int i = 0; i < BoardGeometry.CellCount; i++)
		{
			givens[i] = _cells[i].IsGiven ? '1' : '0';
			int mask = 0;
			for (int n = 1; n <= 9; n++)
				if (_cells[i].HasHint(n)) mask |= 1 << n;
			hints.Add(mask);
		}

		var data = new Godot.Collections.Dictionary
		{
			["version"] = SaveVersion,
			["difficulty"] = (int)_difficulty,
			["cells"] = string.Concat(Array.ConvertAll(_activeBoard.GetCells(), v => (char)('0' + v))),
			["givens"] = new string(givens),
			["hints"] = hints,
			["undo"] = ToArray(_activeBoard.GetUndoHistory()),
			["redo"] = ToArray(_activeBoard.GetRedoHistory()),
			["selected"] = _selectedIndex,
			["notes"] = _notesMode,
		};

		using (var file = Godot.FileAccess.Open(TempPath, Godot.FileAccess.ModeFlags.Write))
		{
			if (file == null)
			{
				GD.PushWarning($"Cannot save the game: {Godot.FileAccess.GetOpenError()}");
				return;
			}
			file.StoreString(Json.Stringify(data));
		}
		Error renamed = DirAccess.RenameAbsolute(TempPath, SavePath);
		if (renamed != Error.Ok) GD.PushWarning($"Cannot save the game: {renamed}");
	}

	private static Godot.Collections.Array ToArray(MoveRecord[] records)
	{
		var array = new Godot.Collections.Array();
		foreach (MoveRecord r in records)
			array.Add(new Godot.Collections.Array { r.Row, r.Col, r.PreviousValue, r.NewValue });
		return array;
	}

	/// <summary>Loads the save once, on the first read after launch. An unreadable save is discarded.</summary>
	private static void EnsureLoaded()
	{
		if (_loaded || Engine.IsEditorHint()) return;
		_loaded = true;
		if (!Godot.FileAccess.FileExists(SavePath)) return;
		try
		{
			Load(Godot.FileAccess.GetFileAsString(SavePath));
		}
		catch (Exception e)
		{
			GD.PushWarning($"Discarding an unreadable saved game: {e.Message}");
			_activeBoard = null;
			_cells = null;
			_selectedIndex = -1;
			_notesMode = false;
			DeleteSave();
		}
	}

	private static void Load(string json)
	{
		var data = Json.ParseString(json).AsGodotDictionary();
		if (data == null || (int)data["version"] != SaveVersion)
			throw new FormatException("unknown save version");

		string cellText = (string)data["cells"];
		string givens = (string)data["givens"];
		var hints = data["hints"].AsGodotArray();
		if (cellText.Length != BoardGeometry.CellCount || givens.Length != BoardGeometry.CellCount || hints.Count != BoardGeometry.CellCount)
			throw new FormatException("wrong cell count");

		int[] values = new int[BoardGeometry.CellCount];
		var cells = new CellData[BoardGeometry.CellCount];
		for (int i = 0; i < values.Length; i++)
		{
			values[i] = cellText[i] - '0';
			int mask = (int)hints[i];
			var cell = new CellData { Value = values[i], IsGiven = givens[i] == '1' };
			var marks = new List<int>();
			for (int n = 1; n <= 9; n++)
				if ((mask & (1 << n)) != 0) marks.Add(n);
			cell.Hints = marks.ToArray();
			cells[i] = cell;
		}

		Board board = Board.Restore(values, ReadHistory(data["undo"]), ReadHistory(data["redo"]));
		int selected = (int)data["selected"];

		var difficulty = (SudokuGenerator.Difficulty)(int)data["difficulty"];
		if (!Enum.IsDefined(difficulty) || difficulty == SudokuGenerator.Difficulty.Invalid)
			throw new FormatException("unknown difficulty");

		_difficulty = difficulty;
		_activeBoard = board;
		_cells = cells;
		_selectedIndex = selected is >= 0 and < 81 ? selected : -1;
		_notesMode = (bool)data["notes"];
	}

	private static List<MoveRecord> ReadHistory(Variant value)
	{
		var records = new List<MoveRecord>();
		foreach (Variant entry in value.AsGodotArray())
		{
			var r = entry.AsGodotArray();
			records.Add(new MoveRecord((int)r[0], (int)r[1], (int)r[2], (int)r[3]));
		}
		return records;
	}

	private static void DeleteSave()
	{
		if (Engine.IsEditorHint()) return;
		foreach (string path in new[] { SavePath, TempPath })
			if (Godot.FileAccess.FileExists(path))
				DirAccess.RemoveAbsolute(path);
	}
}
