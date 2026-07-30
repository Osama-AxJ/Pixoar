namespace Pixoar.Core.Configuration;

/// <summary>
/// Defines infrastructure-level options used by Pixoar core services.
/// </summary>
public sealed class PixoarCoreOptions
{
    /// <summary>
    /// Gets or sets the application folder name under the user's AppData directory.
    /// </summary>
    public string ApplicationFolderName { get; set; } = "Pixoar";

    /// <summary>
    /// Gets or sets the settings file name stored in the application folder.
    /// </summary>
    public string SettingsFileName { get; set; } = "settings.json";

    /// <summary>
    /// Gets or sets the folder name used for application log files.
    /// </summary>
    public string LogsFolderName { get; set; } = "Logs";
}
