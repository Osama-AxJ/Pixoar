using Pixoar.Core.Models;

namespace Pixoar.Core.Interfaces;

/// <summary>
/// Creates and normalizes Pixoar settings documents.
/// </summary>
public interface ISettingsFactory
{
    /// <summary>
    /// Creates a new settings document populated with default values.
    /// </summary>
    /// <returns>A new settings document.</returns>
    PixoarSettings CreateDefault();

    /// <summary>
    /// Ensures a deserialized settings document contains all required sections and defaults.
    /// </summary>
    /// <param name="settings">The settings document to normalize.</param>
    /// <returns>The normalized settings document.</returns>
    PixoarSettings Normalize(PixoarSettings? settings);
}
