namespace Pixoar.Core.Interfaces;

/// <summary>
/// Provides canonical filesystem locations used by Pixoar.
/// </summary>
public interface IApplicationPathProvider
{
    /// <summary>
    /// Gets the root Pixoar application data directory.
    /// </summary>
    string AppDataDirectory { get; }

    /// <summary>
    /// Gets the full path to the shared settings file.
    /// </summary>
    string SettingsFilePath { get; }

    /// <summary>
    /// Gets the directory where Pixoar log files are stored.
    /// </summary>
    string LogsDirectory { get; }
}
