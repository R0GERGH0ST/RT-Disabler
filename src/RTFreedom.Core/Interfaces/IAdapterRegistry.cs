using RTFreedom.Core.Models;

namespace RTFreedom.Core.Interfaces;

/// <summary>
/// Registry for discovering, registering, and retrieving game adapters.
/// </summary>
public interface IAdapterRegistry
{
    /// <summary>
    /// Registers a game adapter instance with the registry.
    /// </summary>
    /// <param name="adapter">The game adapter to register.</param>
    void RegisterAdapter(IGameAdapter adapter);

    /// <summary>
    /// Finds the game adapter capable of supporting the given game, if any.
    /// </summary>
    /// <param name="game">The game metadata.</param>
    /// <returns>A matching <see cref="IGameAdapter"/> instance, or <c>null</c> if none matched.</returns>
    IGameAdapter? GetAdapter(GameInfo game);

    /// <summary>
    /// Gets all registered game adapters.
    /// </summary>
    /// <returns>A collection of all registered adapters.</returns>
    IReadOnlyList<IGameAdapter> GetAllAdapters();
}
