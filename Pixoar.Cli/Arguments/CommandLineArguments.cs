namespace Pixoar.Cli.Arguments;

internal sealed class CommandLineArguments
{
    public CommandLineArguments(IEnumerable<string> values)
    {
        Values = values.ToArray();
    }

    public IReadOnlyList<string> Values { get; }

    public string? CommandName => Values.Count > 0 ? Values[0] : null;
}
