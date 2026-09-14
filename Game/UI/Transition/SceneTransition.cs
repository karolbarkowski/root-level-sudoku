using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Generators.Sudoku;
using Godot;
using Sudoku;

namespace SudokuEndless;

/// <summary>
/// Autoload that performs every scene change behind one persistent cover, so the player never sees
/// a scene being swapped:
/// <list type="number">
/// <item>the outgoing scene animates away while the cover (and optional message) fades in;</item>
/// <item>the next scene loads on a worker thread, alongside any background work such as puzzle generation;</item>
/// <item>the new scene is swapped in under the cover and given time to build and draw once;</item>
/// <item>the cover lifts as the new scene plays its entrance.</item>
/// </list>
/// Nothing slow runs on the Godot thread while the cover is up, so its spinner keeps turning.
/// </summary>
public partial class SceneTransition : CanvasLayer
{
    public const string StartScreenPath = "res://Scenes/StartScreen/StartScreen.tscn";
    public const string GamePath = "res://Scenes/Game/Main.tscn";
    public const string SettingsPath = "res://Scenes/Settings/Settings.tscn";

    /// <summary>Delay before the cover follows an animated exit, so the exit is seen first.</summary>
    private const double CoverDelay = .1;

    /// <summary>Shortest time a message stays readable once the cover is up, so it never just flashes.</summary>
    private const double MinMessageTime = .45;

    /// <summary>Frame budget for freeing the previous scene once the new one has settled.</summary>
    private const double FreeBudgetMs = 4;

    /// <summary>How long after the cover lifts the previous scene starts being freed, so the entrance runs first.</summary>
    private const double FreeDelay = .5;

    /// <summary>Print phase timings and the longest frame of each transition (debug builds only).</summary>
    private static readonly bool LogTimings = OS.IsDebugBuild();

    private static SceneTransition _instance;

    private GenerationOverlay _overlay;
    private bool _busy;
    private int _run;
    private double _worstFrame;

    // Scenes detached by a transition and not yet freed; see FreeGradually.
    private readonly HashSet<Node> _detached = new();

    /// <summary>
    /// True from the moment a transition starts until its cover begins to lift. A scene reads this
    /// in <c>_Ready</c> to know whether it should run its own entrance (launched directly) or wait.
    /// </summary>
    public static bool IsTransitioning => _instance?._busy ?? false;

    /// <summary>Instance mirror of <see cref="IsTransitioning"/> for GDScript tests.</summary>
    public bool Busy => _busy;

    public override async void _Ready()
    {
        _instance = this;
        Layer = 100;
        _overlay = GetNode<GenerationOverlay>("Overlay");
        _overlay.Prewarm();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        _overlay.EndPrewarm();
    }

    public override void _ExitTree()
    {
        if (_instance == this) _instance = null;
        // Quitting mid-way through freeing a previous scene: finish the job so nothing leaks.
        foreach (Node scene in _detached)
            if (IsInstanceValid(scene)) scene.Free();
        _detached.Clear();
    }

    public override void _Process(double delta)
    {
        if (_busy) _worstFrame = Math.Max(_worstFrame, delta);
    }

    /// <summary>Changes to <paramref name="path"/>. Returns false if a transition is already running.</summary>
    /// <param name="message">Title shown while waiting; null for a plain cross-fade.</param>
    /// <param name="work">Started immediately, alongside the scene load; the new scene is not built until it completes.</param>
    public static bool GoTo(string path, string message = null, Func<Task> work = null)
    {
        if (_instance == null)
        {
            GD.PushError("SceneTransition autoload is missing.");
            return false;
        }
        if (_instance._busy) return false;
        _instance.Run(path, message, work);
        return true;
    }

    /// <summary>Generates a fresh puzzle off the Godot thread and opens the game once it is ready.</summary>
    public static bool StartNewGame(SudokuGenerator.Difficulty difficulty) =>
        GoTo(GamePath, "GENERATING PUZZLE", async () =>
        {
            GameSession.Clear();
            GameSession.Difficulty = difficulty;
            Board board = await Task.Run(() => SudokuGenerator.Generate(difficulty));
            GameSession.Start(board, difficulty);
        });

    private async void Run(string path, string message, Func<Task> work)
    {
        int run = ++_run;
        _busy = true;
        _worstFrame = 0;
        var clock = Stopwatch.StartNew();
        var timings = new System.Text.StringBuilder();
        SceneTree tree = GetTree();
        try
        {
            // Everything slow starts on the tap frame and runs while the exit animation plays.
            Error request = ResourceLoader.LoadThreadedRequest(path);
            if (request != Error.Ok) GD.PushError($"Cannot load {path}: {request}");
            Task workTask = work?.Invoke() ?? Task.CompletedTask;

            Node outgoing = tree.CurrentScene;
            var exiting = outgoing as ITransitionScreen;
            await Task.WhenAll(
                exiting?.PlayExitAsync() ?? Task.CompletedTask,
                _overlay.ShowAsync(message, exiting != null ? CoverDelay : 0));
            double covered = clock.Elapsed.TotalSeconds;
            Mark(timings, clock, "covered");

            while (ResourceLoader.LoadThreadedGetStatus(path) == ResourceLoader.ThreadLoadStatus.InProgress)
                await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var packed = ResourceLoader.LoadThreadedGet(path) as PackedScene;
            Mark(timings, clock, "scene loaded");

            try
            {
                await workTask;
                Mark(timings, clock, "work done");
            }
            catch (Exception e)
            {
                GD.PushError($"Scene transition work failed: {e.GetBaseException().Message}");
                packed = path == StartScreenPath ? packed : GD.Load<PackedScene>(StartScreenPath);
            }

            if (packed == null)
            {
                GD.PushError($"Cannot open {path}.");
                packed = GD.Load<PackedScene>(StartScreenPath);
            }

            // Swap under the cover. The old scene leaves the tree first so its _ExitTree settles
            // shared state before the new scene's _Ready reads it.
            // Freeing a whole scene at once (the game is ~1200 nodes) stalls a frame, so the old one is
            // only detached now and freed piecemeal after the new scene has settled.
            Node incoming = packed.Instantiate();
            if (outgoing != null && IsInstanceValid(outgoing))
            {
                if (outgoing.IsInsideTree()) outgoing.GetParent().RemoveChild(outgoing);
                _detached.Add(outgoing);
            }
            else outgoing = null;
            tree.Root.AddChild(incoming);
            tree.CurrentScene = incoming;
            Mark(timings, clock, "swapped");

            var entering = incoming as ITransitionScreen;
            if (entering != null) await entering.PrepareRevealAsync();
            // Two drawn frames: fonts, textures and pipelines are warm before anything is seen.
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Mark(timings, clock, "prepared");

            // Building the new scene counts towards the message's reading time.
            if (!string.IsNullOrEmpty(message))
            {
                double wait = MinMessageTime - (clock.Elapsed.TotalSeconds - covered);
                if (wait > 0) await ToSignal(tree.CreateTimer(wait), SceneTreeTimer.SignalName.Timeout);
            }

            await _overlay.HideAsync(() =>
            {
                if (LogTimings) GD.Print($"[transition] {path}:{timings} revealed {clock.Elapsed.TotalMilliseconds:0} ms, worst frame {_worstFrame * 1000:0} ms");
                if (run == _run) _busy = false;
                if (entering != null && IsInstanceValid(incoming) && incoming.IsInsideTree()) entering.PlayEntry();
            });

            if (outgoing != null)
            {
                await ToSignal(tree.CreateTimer(FreeDelay), SceneTreeTimer.SignalName.Timeout);
                await FreeGradually(outgoing);
            }
        }
        catch (Exception e)
        {
            GD.PushError($"Scene transition to {path} failed: {e}");
            _overlay.Visible = false;
        }
        finally
        {
            // A new transition may already have started while this cover was lifting.
            if (run == _run) _busy = false;
        }
    }

    private async Task FreeGradually(Node scene)
    {
        var nodes = new List<Node>();
        CollectPostOrder(scene, nodes);
        var clock = Stopwatch.StartNew();
        foreach (Node node in nodes)
        {
            if (IsInstanceValid(node)) node.Free();
            if (clock.Elapsed.TotalMilliseconds > FreeBudgetMs)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!IsInstanceValid(this) || !IsInsideTree()) return;
                clock.Restart();
            }
        }
        _detached.Remove(scene);
    }

    /// <summary>Children before parents, last child first, so each free is a cheap removal from the end.</summary>
    private static void CollectPostOrder(Node node, List<Node> nodes)
    {
        for (int i = node.GetChildCount(true) - 1; i >= 0; i--)
            CollectPostOrder(node.GetChild(i, true), nodes);
        nodes.Add(node);
    }

    private static void Mark(System.Text.StringBuilder timings, Stopwatch clock, string phase)
    {
        if (LogTimings) timings.Append($" {phase} {clock.Elapsed.TotalMilliseconds:0} ms,");
    }
}
