using System.Collections.Concurrent;
using RTFreedom.Core.Interfaces;
using RTFreedom.Core.Models;

namespace RTFreedom.Adapters;

/// <summary>
/// Thread-safe registry for registering, finding, and enumerating game adapters.
/// </summary>
public class AdapterRegistry : IAdapterRegistry
{
    private readonly ConcurrentDictionary<string, IGameAdapter> _adapters = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a game adapter instance with the registry.
    /// </summary>
    /// <param name="adapter">The game adapter to register.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="adapter"/> is null.</exception>
    public void RegisterAdapter(IGameAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _adapters[adapter.GameId] = adapter;
    }

    /// <summary>
    /// Finds the game adapter capable of supporting the given game.
    /// Evaluates direct ID matches first, then checks <see cref="IGameAdapter.SupportsGame(GameInfo)"/> across registered adapters.
    /// </summary>
    /// <param name="game">The detected game metadata.</param>
    /// <returns>A matching <see cref="IGameAdapter"/> instance, or <c>null</c> if none matched.</returns>
    public IGameAdapter? GetAdapter(GameInfo game)
    {
        ArgumentNullException.ThrowIfNull(game);

        // First check by known game ID if present
        if (!string.IsNullOrWhiteSpace(game.Id) &&
            _adapters.TryGetValue(game.Id, out var adapterById) &&
            adapterById.SupportsGame(game))
        {
            return adapterById;
        }

        // Search through registered adapters via SupportsGame
        foreach (var adapter in _adapters.Values)
        {
            if (adapter.SupportsGame(game))
            {
                return adapter;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all registered game adapters.
    /// </summary>
    /// <returns>A read-only collection of all registered adapters.</returns>
    public IReadOnlyList<IGameAdapter> GetAllAdapters()
    {
        return _adapters.Values.ToList().AsReadOnly();
    }
}
