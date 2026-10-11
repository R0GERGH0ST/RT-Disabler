using System.Runtime.InteropServices;
using System.Text;
using RTFreedom.Core.Models;

namespace RTFreedom.Diagnostics;

/// <summary>
/// Generates comprehensive, structured diagnostic and compatibility reports for support, auditing, and export.
/// </summary>
public class DiagnosticReportGenerator
{
    private readonly string _reportsDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticReportGenerator"/> class.
    /// </summary>
    /// <param name="customReportsDirectory">Optional custom directory path for saved reports.</param>
    public DiagnosticReportGenerator(string? customReportsDirectory = null)
    {
        _reportsDirectory = customReportsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RTFreedom",
            "Reports");
    }

    /// <summary>
    /// Generates a structured compatibility report for the given game and analysis, and writes it to disk.
    /// </summary>
    /// <param name="game">Detected game metadata.</param>
    /// <param name="analysis">Ray tracing analysis results, if performed.</param>
    /// <returns>Formatted report text string.</returns>
    public async Task<string> GenerateReportAsync(GameInfo game, RayTracingAnalysis? analysis)
    {
        ArgumentNullException.ThrowIfNull(game);

        var sb = new StringBuilder();
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

        sb.AppendLine("================================================================================");
        sb.AppendLine("                      RT FREEDOM DIAGNOSTIC COMPATIBILITY REPORT                ");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Generated At: {timestamp}");
        sb.AppendLine();

        // 1. System Information
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("1. SYSTEM INFORMATION");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"OS Description:     {RuntimeInformation.OSDescription}");
        sb.AppendLine($"OS Architecture:    {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($"Framework:          {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Machine Name:       {Environment.MachineName}");
        sb.AppendLine($"Logical Processors: {Environment.ProcessorCount}");
        sb.AppendLine();

        // 2. Game Information
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("2. GAME INFORMATION");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"Game ID:            {game.Id}");
        sb.AppendLine($"Title:              {game.Title}");
        sb.AppendLine($"Version:            {game.Version ?? "Unknown"}");
        sb.AppendLine($"Publisher:          {game.Publisher ?? "Unknown"}");
        sb.AppendLine($"Platform:           {game.Platform}");
        sb.AppendLine($"Graphics API:       {game.GraphicsApi}");
        sb.AppendLine($"Executable Path:    {game.ExecutablePath}");
        sb.AppendLine($"Executable Exists:  {File.Exists(game.ExecutablePath)}");
        sb.AppendLine($"Install Path:       {game.InstallPath}");
        sb.AppendLine($"Config Files Found: {game.ConfigurationFiles.Count}");
        foreach (var cfg in game.ConfigurationFiles)
        {
            sb.AppendLine($"  - {cfg}");
        }
        sb.AppendLine($"Shader Files Found: {game.ShaderFiles.Count}");
        foreach (var sh in game.ShaderFiles)
        {
            sb.AppendLine($"  - {sh}");
        }
        sb.AppendLine();

        // 3. Ray Tracing Analysis
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("3. RAY TRACING ANALYSIS RESULTS");
        sb.AppendLine("--------------------------------------------------------------------------------");
        if (analysis == null)
        {
            sb.AppendLine("Status: No Ray Tracing Analysis has been performed for this game.");
        }
        else
        {
            sb.AppendLine($"Analyzed At:            {analysis.AnalyzedAt:yyyy-MM-dd HH:mm:ss UTC}");
            sb.AppendLine($"Recommended Level:      {analysis.RecommendedLevel}");
            sb.AppendLine($"Has Non-RT Fallback:    {analysis.HasNonRTFallback}");
            sb.AppendLine($"Requires Hardware RT:   {analysis.RequiresHardwareRT}");
            sb.AppendLine();

            sb.AppendLine("Detected DXR / Vulkan RT API Calls:");
            if (analysis.DetectedDxrCalls.Count == 0)
            {
                sb.AppendLine("  (None detected)");
            }
            else
            {
                foreach (var call in analysis.DetectedDxrCalls)
                {
                    sb.AppendLine($"  - {call}");
                }
            }
            sb.AppendLine();

            sb.AppendLine("Detected RT Shader Stages / Bytecode Features:");
            if (analysis.DetectedShaderFeatures.Count == 0)
            {
                sb.AppendLine("  (None detected)");
            }
            else
            {
                foreach (var feat in analysis.DetectedShaderFeatures)
                {
                    sb.AppendLine($"  - {feat}");
                }
            }
            sb.AppendLine();

            sb.AppendLine("Ray Tracing Dependencies:");
            if (analysis.Dependencies.Count == 0)
            {
                sb.AppendLine("  (No specific RT dependencies discovered)");
            }
            else
            {
                foreach (var dep in analysis.Dependencies)
                {
                    sb.AppendLine($"  * Feature:     {dep.Feature} [{dep.Type}]");
                    sb.AppendLine($"    Mandatory:   {dep.IsMandatory}");
                    sb.AppendLine($"    Fallback:    {(dep.HasFallback ? "Available" : "Not Available")}");
                    sb.AppendLine($"    Details:     {dep.Description}");
                }
            }
            sb.AppendLine();

            sb.AppendLine("Feasibility Assessment:");
            sb.AppendLine($"  {analysis.FeasibilityAssessment}");
        }
        sb.AppendLine();

        // 4. Applied Patches and Status
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("4. APPLIED PATCHES & STATUS");
        sb.AppendLine("--------------------------------------------------------------------------------");
        string backupDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RTFreedom",
            "Backups",
            game.Id);

        if (Directory.Exists(backupDir))
        {
            var manifestFiles = Directory.GetFiles(backupDir, "manifest*.json");
            sb.AppendLine($"Backup Snapshots Available: {manifestFiles.Length}");
            foreach (var mf in manifestFiles)
            {
                sb.AppendLine($"  - Manifest: {Path.GetFileName(mf)}");
            }
        }
        else
        {
            sb.AppendLine("No active backups or applied patches recorded for this game ID.");
        }

        sb.AppendLine();
        sb.AppendLine("================================================================================");
        sb.AppendLine("                                END OF REPORT                                   ");
        sb.AppendLine("================================================================================");

        string reportContent = sb.ToString();

        // Save report to disk
        try
        {
            Directory.CreateDirectory(_reportsDirectory);
            string safeGameId = string.Join("_", game.Id.Split(Path.GetInvalidFileNameChars()));
            string fileName = $"report_{safeGameId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
            string filePath = Path.Combine(_reportsDirectory, fileName);
            await File.WriteAllTextAsync(filePath, reportContent).ConfigureAwait(false);
        }
        catch
        {
            // Report writing failure should not prevent returning the report text
        }

        return reportContent;
    }
}
