using System.Text;
using RTFreedom.Core.Enums;
using RTFreedom.Core.Interfaces;
using RTFreedom.Core.Models;

namespace RTFreedom.Adapters;

/// <summary>
/// Abstract base class providing common utility functions, file operations,
/// and baseline workflows for game-specific adapters.
/// </summary>
public abstract class BaseGameAdapter : IGameAdapter
{
    /// <inheritdoc />
    public abstract string GameId { get; }

    /// <inheritdoc />
    public abstract string GameTitle { get; }

    /// <inheritdoc />
    public abstract string[] SupportedVersions { get; }

    /// <inheritdoc />
    public abstract Task<RayTracingAnalysis> AnalyzeAsync(GameInfo game, GpuCapabilities gpu, CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<PatchAction>> GetAvailablePatchesAsync(GameInfo game, RayTracingAnalysis analysis);

    /// <inheritdoc />
    public abstract Task<bool> ApplyPatchAsync(GameInfo game, PatchAction patch, IBackupManager backup, CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<bool> ValidatePatchAsync(GameInfo game, PatchAction patch);

    /// <inheritdoc />
    public abstract bool SupportsGame(GameInfo game);

    /// <summary>
    /// Checks whether a given version string is compatible with <see cref="SupportedVersions"/>.
    /// </summary>
    /// <param name="version">The detected game version string.</param>
    /// <returns><c>true</c> if supported; otherwise, <c>false</c>.</returns>
    protected virtual bool IsVersionSupported(string? version)
    {
        if (SupportedVersions.Length == 0 || SupportedVersions.Contains("*"))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        return SupportedVersions.Any(v => string.Equals(v, version, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Reads key-value pairs from an INI or config file.
    /// Supports '=' and ':' separators, and ignores empty lines or comments starting with '#', ';', or '//'.
    /// </summary>
    /// <param name="path">Absolute path to the configuration file.</param>
    /// <returns>A dictionary containing config keys and values.</returns>
    protected Dictionary<string, string> ReadConfigFile(string path)
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(path))
        {
            return settings;
        }

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) ||
                line.StartsWith('#') ||
                line.StartsWith(';') ||
                line.StartsWith("//") ||
                (line.StartsWith('[') && line.EndsWith(']')))
            {
                continue;
            }

            var separatorIndex = line.IndexOfAny(['=', ':']);
            if (separatorIndex > 0)
            {
                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim();
                settings[key] = value;
            }
        }

        return settings;
    }

    /// <summary>
    /// Writes or updates key-value pairs in a configuration file atomically using a temporary file.
    /// Preserves existing comments and unknown keys where possible.
    /// </summary>
    /// <param name="path">Absolute path to the target config file.</param>
    /// <param name="values">Dictionary containing keys and their desired values.</param>
    protected void WriteConfigFile(string path, Dictionary<string, string> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(values);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var remainingKeys = new HashSet<string>(values.Keys, StringComparer.OrdinalIgnoreCase);
        var outputLines = new List<string>();

        if (File.Exists(path))
        {
            foreach (var rawLine in File.ReadAllLines(path))
            {
                var trimmed = rawLine.Trim();
                if (string.IsNullOrEmpty(trimmed) ||
                    trimmed.StartsWith('#') ||
                    trimmed.StartsWith(';') ||
                    trimmed.StartsWith("//") ||
                    (trimmed.StartsWith('[') && trimmed.EndsWith(']')))
                {
                    outputLines.Add(rawLine);
                    continue;
                }

                var separatorIndex = trimmed.IndexOfAny(['=', ':']);
                if (separatorIndex > 0)
                {
                    var key = trimmed[..separatorIndex].Trim();
                    if (values.TryGetValue(key, out var newValue))
                    {
                        var separator = trimmed[separatorIndex];
                        outputLines.Add($"{key}{separator}{newValue}");
                        remainingKeys.Remove(key);
                        continue;
                    }
                }

                outputLines.Add(rawLine);
            }
        }

        // Append any remaining keys that were not already in the file
        foreach (var key in remainingKeys)
        {
            outputLines.Add($"{key}={values[key]}");
        }

        // Safe atomic write
        var tempFilePath = Path.Combine(
            directory ?? AppContext.BaseDirectory,
            $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllLines(tempFilePath, outputLines, Encoding.UTF8);

            if (File.Exists(path))
            {
                File.Replace(tempFilePath, path, null);
            }
            else
            {
                File.Move(tempFilePath, path);
            }
        }
        catch
        {
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { /* best effort cleanup */ }
            }
            throw;
        }
    }

    /// <summary>
    /// Scans a binary or text file for the presence of specific search strings.
    /// </summary>
    /// <param name="filePath">Absolute path to the file to inspect.</param>
    /// <param name="searchTerms">Array of text terms to search for.</param>
    /// <returns>A list of search terms found in the file.</returns>
    protected List<string> ScanForStrings(string filePath, string[] searchTerms)
    {
        var matched = new List<string>();
        if (!File.Exists(filePath) || searchTerms.Length == 0)
        {
            return matched;
        }

        try
        {
            // For smaller files, reading as bytes and checking byte representations or string conversion
            using var stream = File.OpenRead(filePath);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            // Read in chunks to prevent loading multi-gigabyte binaries entirely into memory
            var buffer = new char[64 * 1024];
            var overlap = 512;
            var tail = string.Empty;
            var searchSet = new HashSet<string>(searchTerms, StringComparer.OrdinalIgnoreCase);

            int charsRead;
            while ((charsRead = reader.Read(buffer, 0, buffer.Length)) > 0 && searchSet.Count > 0)
            {
                var chunk = tail + new string(buffer, 0, charsRead);
                foreach (var term in searchSet.ToList())
                {
                    if (chunk.Contains(term, StringComparison.OrdinalIgnoreCase))
                    {
                        matched.Add(term);
                        searchSet.Remove(term);
                    }
                }

                tail = chunk.Length > overlap ? chunk[^overlap..] : chunk;
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to scan file '{filePath}' for strings: {ex.Message}", LogLevel.Warning);
        }

        return matched;
    }

    /// <summary>
    /// Checks whether a relative path exists inside a specified base directory.
    /// </summary>
    /// <param name="basePath">Base root path.</param>
    /// <param name="relativePath">Relative file or directory path.</param>
    /// <returns><c>true</c> if the file exists; otherwise, <c>false</c>.</returns>
    protected bool CheckFileExists(string basePath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(basePath) || string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var fullPath = Path.Combine(basePath, relativePath);
        return File.Exists(fullPath);
    }

    /// <summary>
    /// Logs adapter diagnostic messages.
    /// </summary>
    /// <param name="message">Log message content.</param>
    /// <param name="level">Severity level.</param>
    protected virtual void Log(string message, LogLevel level = LogLevel.Info)
    {
        // Internal diagnostic log hook
        System.Diagnostics.Trace.WriteLine($"[{level}] [{GameId}] {message}");
    }
}
