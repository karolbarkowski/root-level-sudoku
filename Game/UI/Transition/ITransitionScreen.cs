using System.Threading.Tasks;

namespace SudokuEndless;

/// <summary>
/// Optional hooks a scene implements to take part in <see cref="SceneTransition"/>. Scenes without
/// it still change behind the cover, they just have no animation of their own.
/// </summary>
public interface ITransitionScreen
{
    /// <summary>Outgoing: animate away. The cover fades in over the tail of this.</summary>
    Task PlayExitAsync();

    /// <summary>
    /// Incoming, still hidden by the cover: finish any deferred construction and park content in its
    /// pre-entrance state. The cover stays up until this completes.
    /// </summary>
    Task PrepareRevealAsync();

    /// <summary>Incoming: the cover has started to lift. Run the entrance.</summary>
    void PlayEntry();
}
