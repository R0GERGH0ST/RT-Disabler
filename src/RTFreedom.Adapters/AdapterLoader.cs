using System.Reflection;
using System.Runtime.Loader;
using RTFreedom.Core.Interfaces;

namespace RTFreedom.Adapters;

/// <summary>
/// Provides discovery, registration, and dynamic loading of game adapters.
/// </summary>
public static class AdapterLoader
{
    /// <summary>
    /// Registers all built-in game adapters into the specified registry.
    /// </summary>
    /// <param name="registry">The registry to register built-in adapters with.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="registry"/> is null.</exception>
    public static void RegisterBuiltInAdapters(IAdapterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.RegisterAdapter(new IndianaJonesAdapter());
        registry.RegisterAdapter(new GearsOfWarAdapter());
        registry.RegisterAdapter(new DoomDarkAgesAdapter());
    }

    /// <summary>
    /// Scans a plugin directory for external .NET assemblies and discovers any classes implementing <see cref="IGameAdapter"/>.
    /// </summary>
    /// <param name="directoryPath">Path to the directory containing adapter plugin assemblies.</param>
    /// <returns>A list of discovered and instantiated game adapters.</returns>
    public static IReadOnlyList<IGameAdapter> ScanPluginDirectory(string directoryPath)
    {
        var discoveredAdapters = new List<IGameAdapter>();

        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return discoveredAdapters;
        }

        var dllFiles = Directory.GetFiles(directoryPath, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                // Load into a custom collectible load context or current context
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);

                var adapterTypes = assembly.GetTypes()
                    .Where(t => typeof(IGameAdapter).IsAssignableFrom(t) &&
                                t is { IsAbstract: false, IsInterface: false });

                foreach (var type in adapterTypes)
                {
                    try
                    {
                        if (Activator.CreateInstance(type) is IGameAdapter adapter)
                        {
                            discoveredAdapters.Add(adapter);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"Failed to instantiate adapter '{type.FullName}' from '{dllPath}': {ex.Message}");
                    }
                }
            }
            catch (BadImageFormatException)
            {
                // Native DLL or non-managed assembly, ignore
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"Failed to load plugin assembly from '{dllPath}': {ex.Message}");
            }
        }

        return discoveredAdapters;
    }
}
