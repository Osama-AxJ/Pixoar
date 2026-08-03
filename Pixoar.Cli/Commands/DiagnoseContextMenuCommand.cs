using Pixoar.Cli.Arguments;
using Pixoar.Cli.Execution;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Cli.Commands;

internal sealed class DiagnoseContextMenuCommand(IContextMenuService contextMenuService) : ICommand
{
    public string Name => "diagnose-context-menu";

    public string Description => "Diagnoses installed Windows Explorer context menu entries.";

    public bool CanHandle(CommandLineArguments arguments)
    {
        return string.Equals(arguments.CommandName, Name, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var report = await contextMenuService.DiagnoseAsync(cancellationToken).ConfigureAwait(false);
        report.ParseChecks.AddRange(CreateParseChecks());

        return new CommandResult(
            report.IsValid ? CliExitCodes.Success : CliExitCodes.Failure,
            report.ToDisplayText());
    }

    private static IEnumerable<ContextMenuParseCheck> CreateParseChecks()
    {
        yield return CheckConvert("convert jpg", "jpg");
        yield return CheckConvert("convert dds bc7", "dds-bc7");
        yield return CheckResize("resize 50", "50");
        yield return CheckResize("resize 75", "75");
    }

    private static ContextMenuParseCheck CheckConvert(string name, string format)
    {
        const string samplePath = @".\Images\image.png";
        var options = CommandLineParser.Parse(["--format", format, samplePath]);
        var success =
            string.Equals(options.GetOption("format"), format, StringComparison.OrdinalIgnoreCase) &&
            options.Values.SequenceEqual([samplePath], StringComparer.OrdinalIgnoreCase);

        return new ContextMenuParseCheck
        {
            Name = name,
            Success = success,
            Details = success ? $"--format {format}" : "Could not parse convert arguments."
        };
    }

    private static ContextMenuParseCheck CheckResize(string name, string percentage)
    {
        const string samplePath = @".\Images\image.png";
        var options = CommandLineParser.Parse(["--percentage", percentage, samplePath]);
        var success =
            string.Equals(options.GetOption("percentage"), percentage, StringComparison.OrdinalIgnoreCase) &&
            options.Values.SequenceEqual([samplePath], StringComparer.OrdinalIgnoreCase);

        return new ContextMenuParseCheck
        {
            Name = name,
            Success = success,
            Details = success ? $"--percentage {percentage}" : "Could not parse resize arguments."
        };
    }
}
