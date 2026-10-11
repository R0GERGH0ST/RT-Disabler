using System.Security.Cryptography;

namespace RTFreedom.Backup;

/// <summary>
/// Provides cryptographic SHA-256 integrity verification, hashing, and file comparison services.
/// </summary>
public static class FileIntegrityVerifier
{
    /// <summary>
    /// Computes the SHA-256 hash of a file as a hexadecimal string.
    /// </summary>
    /// <param name="filePath">Absolute path to the target file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Lowercase hexadecimal representation of the SHA-256 hash.</returns>
    /// <exception cref="FileNotFoundException">Thrown when <paramref name="filePath"/> does not exist.</exception>
    public static async Task<string> ComputeHashAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found for hash calculation: {filePath}", filePath);
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);

        var hashBytes = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexStringLower(hashBytes);
    }

    /// <summary>
    /// Verifies whether the specified file matches an expected SHA-256 hash.
    /// </summary>
    /// <param name="filePath">Absolute path to the file to verify.</param>
    /// <param name="expectedHash">Expected SHA-256 hexadecimal string.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the computed hash matches the expected hash; otherwise, <c>false</c>.</returns>
    public static async Task<bool> VerifyFileAsync(string filePath, string expectedHash, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHash);

        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var computedHash = await ComputeHashAsync(filePath, ct).ConfigureAwait(false);
            return string.Equals(computedHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Compares two files for binary equality by verifying size and computing cryptographic hashes.
    /// </summary>
    /// <param name="path1">Path to the first file.</param>
    /// <param name="path2">Path to the second file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if both files exist and possess identical contents; otherwise, <c>false</c>.</returns>
    public static async Task<bool> CompareFilesAsync(string path1, string path2, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path1);
        ArgumentException.ThrowIfNullOrWhiteSpace(path2);

        var fileInfo1 = new FileInfo(path1);
        var fileInfo2 = new FileInfo(path2);

        if (!fileInfo1.Exists || !fileInfo2.Exists)
        {
            return false;
        }

        if (fileInfo1.Length != fileInfo2.Length)
        {
            return false;
        }

        var hash1 = await ComputeHashAsync(path1, ct).ConfigureAwait(false);
        var hash2 = await ComputeHashAsync(path2, ct).ConfigureAwait(false);

        return string.Equals(hash1, hash2, StringComparison.OrdinalIgnoreCase);
    }
}
