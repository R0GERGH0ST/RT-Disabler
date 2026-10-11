using CommunityToolkit.Mvvm.ComponentModel;

namespace RTFreedom.App.ViewModels;

/// <summary>
/// Abstract base ViewModel implementing observable change tracking and busy status state.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Sets the busy indicator along with an optional status message.
    /// </summary>
    /// <param name="message">Status message to display.</param>
    public void SetBusy(string message = "")
    {
        IsBusy = true;
        StatusMessage = message;
    }

    /// <summary>
    /// Clears the busy indicator and resets the status message.
    /// </summary>
    public void ClearBusy()
    {
        IsBusy = false;
        StatusMessage = string.Empty;
    }
}
