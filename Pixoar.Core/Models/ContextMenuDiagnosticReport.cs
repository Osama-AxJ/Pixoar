using System.Text;

namespace Pixoar.Core.Models;

/// <summary>
/// Represents the current Windows Explorer context menu health report.
/// </summary>
public sealed class ContextMenuDiagnosticReport
{
    /// <summary>
    /// Gets or sets the resolved Pixoar.exe path.
    /// </summary>
    public string AppExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the app executable exists.
    /// </summary>
    public bool AppExecutableExists { get; set; }

    /// <summary>
    /// Gets or sets the resolved Pixoar.Cli.exe path.
    /// </summary>
    public string CliExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the CLI executable exists.
    /// </summary>
    public bool CliExecutableExists { get; set; }

    /// <summary>
    /// Gets or sets the context menu icon file path.
    /// </summary>
    public string ContextMenuIconPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the context menu icon file exists.
    /// </summary>
    public bool ContextMenuIconExists { get; set; }

    /// <summary>
    /// Gets or sets the resolved texconv.exe path.
    /// </summary>
    public string TexconvPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether texconv.exe exists.
    /// </summary>
    public bool TexconvExists { get; set; }

    /// <summary>
    /// Gets or sets the settings file path used by Pixoar.
    /// </summary>
    public string SettingsFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the process current directory.
    /// </summary>
    public string CurrentDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the executable base directory.
    /// </summary>
    public string AppContextBaseDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets installed registry command diagnostics.
    /// </summary>
    public List<ContextMenuCommandDiagnostic> Commands { get; } = [];

    /// <summary>
    /// Gets parser checks run by the CLI diagnostic command.
    /// </summary>
    public List<ContextMenuParseCheck> ParseChecks { get; } = [];

    /// <summary>
    /// Gets report-level issues.
    /// </summary>
    public List<string> Issues { get; } = [];

    /// <summary>
    /// Gets a value indicating whether all checks passed.
    /// </summary>
    public bool IsValid =>
        AppExecutableExists &&
        CliExecutableExists &&
        ContextMenuIconExists &&
        Commands.Count > 0 &&
        Commands.All(command => command.IsValid) &&
        ParseChecks.All(check => check.Success) &&
        Issues.Count == 0;

    /// <summary>
    /// Formats the report for internal logs and CLI output.
    /// </summary>
    /// <returns>A readable diagnostic report.</returns>
    public string ToDisplayText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Pixoar context menu diagnostic");
        builder.AppendLine();
        builder.AppendLine($"App:      {FormatPathStatus(AppExecutablePath, AppExecutableExists)}");
        builder.AppendLine($"CLI:      {FormatPathStatus(CliExecutablePath, CliExecutableExists)}");
        builder.AppendLine($"Icon:     {FormatPathStatus(ContextMenuIconPath, ContextMenuIconExists)}");
        builder.AppendLine($"texconv:  {FormatPathStatus(TexconvPath, TexconvExists)}");
        builder.AppendLine($"Settings: {SettingsFilePath}");
        builder.AppendLine($"Base dir: {AppContextBaseDirectory}");
        builder.AppendLine($"CWD:      {CurrentDirectory}");
        builder.AppendLine();

        if (Issues.Count > 0)
        {
            builder.AppendLine("Issues:");
            foreach (var issue in Issues)
            {
                builder.AppendLine($"  - {issue}");
            }

            builder.AppendLine();
        }

        if (ParseChecks.Count > 0)
        {
            builder.AppendLine("CLI parse checks:");
            foreach (var check in ParseChecks)
            {
                builder.AppendLine($"  - {check.Name}: {(check.Success ? "OK" : "Failed")} {check.Details}".TrimEnd());
            }

            builder.AppendLine();
        }

        builder.AppendLine("Registry commands:");
        if (Commands.Count == 0)
        {
            builder.AppendLine("  No Pixoar context menu commands were found.");
        }
        else
        {
            foreach (var command in Commands)
            {
                builder.AppendLine($"  - {command.Extension} {command.ActionPath}: {(command.IsValid ? "OK" : "Invalid")}");
                builder.AppendLine($"    Key: {command.RegistryKeyPath}");
                builder.AppendLine($"    Command: {command.Command}");
                builder.AppendLine($"    Executable: {FormatPathStatus(command.ExecutablePath, command.ExecutableExists)}");
                builder.AppendLine($"    Icon: {FormatPathStatus(command.IconPath, command.IconExists)}");
                builder.AppendLine($"    Args: {command.Arguments}");
                builder.AppendLine($"    Placeholder: {command.SelectedFilePlaceholder}");
                if (!string.IsNullOrWhiteSpace(command.Issue))
                {
                    builder.AppendLine($"    Issue: {command.Issue}");
                }
            }
        }

        return builder.ToString();
    }

    private static string FormatPathStatus(string path, bool exists)
    {
        var displayPath = string.IsNullOrWhiteSpace(path) ? "<not found>" : path;
        return $"{displayPath} ({(exists ? "exists" : "missing")})";
    }
}
