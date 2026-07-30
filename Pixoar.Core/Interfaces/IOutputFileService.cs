using Pixoar.Core.Models;

namespace Pixoar.Core.Interfaces;

/// <summary>
/// Creates output file paths for image operations.
/// </summary>
public interface IOutputFileService
{
    /// <summary>
    /// Creates a safe output path according to user settings.
    /// </summary>
    /// <param name="request">The output file request.</param>
    /// <returns>The resolved output path.</returns>
    string CreateOutputPath(OutputFileRequest request);
}
