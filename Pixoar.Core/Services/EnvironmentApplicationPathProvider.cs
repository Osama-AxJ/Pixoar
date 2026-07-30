using Pixoar.Core.Configuration;
using Pixoar.Core.Interfaces;

namespace Pixoar.Core.Services;

internal sealed class EnvironmentApplicationPathProvider(PixoarCoreOptions options) : IApplicationPathProvider
{
    public string AppDataDirectory
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, options.ApplicationFolderName);
        }
    }

    public string SettingsFilePath => Path.Combine(AppDataDirectory, options.SettingsFileName);

    public string LogsDirectory => Path.Combine(AppDataDirectory, options.LogsFolderName);
}
