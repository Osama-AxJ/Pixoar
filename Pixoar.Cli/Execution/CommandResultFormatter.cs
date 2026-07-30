using Pixoar.Core.Models;

namespace Pixoar.Cli.Execution;

internal static class CommandResultFormatter
{
    public static string FormatBatchSummary(string operationName, BatchImageOperationResult result)
    {
        var lines = new List<string>
        {
            $"{operationName} summary:",
            $"  Successful: {result.SuccessCount}",
            $"  Failed:     {result.ErrorCount}"
        };

        foreach (var successful in result.SuccessfulResults)
        {
            lines.Add($"  OK: {successful.InputPath} -> {successful.OutputPath}");
        }

        foreach (var error in result.Errors)
        {
            lines.Add($"  ERROR: {error.InputPath}");
            lines.Add($"         {error.Message}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static int ExitCodeFor(BatchImageOperationResult result)
    {
        if (result.ErrorCount == 0)
        {
            return CliExitCodes.Success;
        }

        return result.SuccessCount > 0 ? CliExitCodes.PartialSuccess : CliExitCodes.Failure;
    }
}
