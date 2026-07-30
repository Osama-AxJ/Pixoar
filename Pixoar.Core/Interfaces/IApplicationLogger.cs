namespace Pixoar.Core.Interfaces;

/// <summary>
/// Writes application log messages to a shared Pixoar logging destination.
/// </summary>
public interface IApplicationLogger
{
    /// <summary>
    /// Writes an informational message.
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <param name="cancellationToken">A token used to cancel the write.</param>
    /// <returns>A task that completes when the message has been written.</returns>
    Task LogInformationAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a warning message.
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <param name="cancellationToken">A token used to cancel the write.</param>
    /// <returns>A task that completes when the message has been written.</returns>
    Task LogWarningAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes an error message and optional exception details.
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <param name="exception">The exception associated with the error, if one exists.</param>
    /// <param name="cancellationToken">A token used to cancel the write.</param>
    /// <returns>A task that completes when the message has been written.</returns>
    Task LogErrorAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default);
}
