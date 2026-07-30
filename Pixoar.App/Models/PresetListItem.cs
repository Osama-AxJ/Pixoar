using Pixoar.App.ViewModels;

namespace Pixoar.App.Models;

/// <summary>
/// Represents an editable preset row in settings.
/// </summary>
public sealed class PresetListItem : ViewModelBase
{
    private string _name = string.Empty;
    private bool _isEnabled = true;

    /// <summary>
    /// Gets or sets the preset display name.
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the preset is enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}
