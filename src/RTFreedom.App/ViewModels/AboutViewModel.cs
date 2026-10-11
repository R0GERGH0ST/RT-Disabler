using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;

namespace RTFreedom.App.ViewModels;

/// <summary>
/// ViewModel for the About and Disclaimers screen.
/// </summary>
public partial class AboutViewModel : ViewModelBase
{
    /// <summary>
    /// Gets the application display title.
    /// </summary>
    public string AppName => "RT Freedom";

    /// <summary>
    /// Gets the application version.
    /// </summary>
    public string AppVersion => "1.0.0 (Release)";

    /// <summary>
    /// Gets the project description text.
    /// </summary>
    public string Description => "A Windows gaming utility designed to analyze graphics pipelines, detect ray-tracing dependencies, and safely apply fallback rasterization configurations for games with mandatory RT requirements.";

    /// <summary>
    /// Gets the safety principles.
    /// </summary>
    public string CorePrinciples => "• Safety First: All original files are verified and backed up prior to any patch.\n• Honest Reporting: If an engine strictly requires ray tracing with no raster fallback, it is clearly reported.\n• Clean Restoration: Restore original unmodified game files with a single click.\n• Zero Anti-Cheat Modification: RT Freedom never modifies anti-cheat components or multiplayer binaries.";

    /// <summary>
    /// Opens the GitHub project repository in default browser.
    /// </summary>
    [RelayCommand]
    private void OpenGitHub()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com",
                UseShellExecute = true
            });
        }
        catch { }
    }
}
