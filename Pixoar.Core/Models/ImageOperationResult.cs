namespace Pixoar.Core.Models;

/// <summary>
/// Represents the result of a single image operation.
/// </summary>
public sealed class ImageOperationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the operation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the input file path.
    /// </summary>
    public string InputPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the output file path.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Gets or sets the error when the operation failed.
    /// </summary>
    public ImageOperationError? Error { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="inputPath">The input path.</param>
    /// <param name="outputPath">The output path.</param>
    /// <returns>The successful result.</returns>
    public static ImageOperationResult Succeeded(string inputPath, string outputPath)
    {
        return new ImageOperationResult
        {
            Success = true,
            InputPath = inputPath,
            OutputPath = outputPath
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="inputPath">The input path.</param>
    /// <param name="outputPath">The output path when known.</param>
    /// <param name="message">The error message.</param>
    /// <returns>The failed result.</returns>
    public static ImageOperationResult Failed(
        string operation,
        string inputPath,
        string? outputPath,
        string message)
    {
        return new ImageOperationResult
        {
            Success = false,
            InputPath = inputPath,
            OutputPath = outputPath,
            Error = new ImageOperationError
            {
                Operation = operation,
                InputPath = inputPath,
                OutputPath = outputPath,
                Message = message,
                Timestamp = DateTimeOffset.Now
            }
        };
    }
}
