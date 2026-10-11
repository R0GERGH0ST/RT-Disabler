using RTFreedom.Backup;
using Xunit;

namespace RTFreedom.Tests;

public class BackupAndRestoreTests : IDisposable
{
    private readonly string _testTempDir;
    private readonly string _testBackupRoot;

    public BackupAndRestoreTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "RTFreedom_Tests_" + Guid.NewGuid().ToString("N"));
        _testBackupRoot = Path.Combine(_testTempDir, "Backups");
        Directory.CreateDirectory(_testTempDir);
        Directory.CreateDirectory(_testBackupRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, true);
            }
        }
        catch
        {
            // Ignore cleanup exceptions
        }
    }

    [Fact]
    public async Task FileIntegrityVerifier_ComputesAndVerifiesCorrectSha256()
    {
        var testFile = Path.Combine(_testTempDir, "test_config.ini");
        var originalContent = "[Render]\nRayTracing=1\nShadowQuality=3\n";
        await File.WriteAllTextAsync(testFile, originalContent);

        var hash = await FileIntegrityVerifier.ComputeHashAsync(testFile);
        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length); // 64 hex characters for SHA-256

        var isValid = await FileIntegrityVerifier.VerifyFileAsync(testFile, hash);
        Assert.True(isValid);

        // Tamper with file
        await File.WriteAllTextAsync(testFile, "[Render]\nRayTracing=0\n");
        var isStillValid = await FileIntegrityVerifier.VerifyFileAsync(testFile, hash);
        Assert.False(isStillValid);
    }

    [Fact]
    public async Task BackupService_CreatesAndRestoresBackupAccurately()
    {
        var backupService = new BackupService(_testBackupRoot);
        var originalFile = Path.Combine(_testTempDir, "GameSettings.ini");
        var originalContent = "EnableRayTracing=true\nGIQuality=High";
        await File.WriteAllTextAsync(originalFile, originalContent);

        // 1. Create backup
        var manifest = await backupService.CreateBackupAsync("test-game", [originalFile]);
        Assert.NotNull(manifest);
        Assert.Single(manifest.Entries);
        Assert.Equal(originalFile, manifest.Entries[0].OriginalPath);

        // 2. Modify original file (simulating patch)
        await File.WriteAllTextAsync(originalFile, "EnableRayTracing=false\nGIQuality=Low");
        Assert.Equal("EnableRayTracing=false\nGIQuality=Low", await File.ReadAllTextAsync(originalFile));

        // 3. Verify backup integrity
        var isIntact = await backupService.VerifyBackupAsync(manifest.BackupId);
        Assert.True(isIntact);

        // 4. Restore original file
        var restored = await backupService.RestoreBackupAsync(manifest.BackupId);
        Assert.True(restored);

        var restoredContent = await File.ReadAllTextAsync(originalFile);
        Assert.Equal(originalContent, restoredContent);
    }
}
