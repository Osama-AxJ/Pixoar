namespace Pixoar.Cli.Arguments;

internal sealed class ParsedCommandOptions
{
    public Dictionary<string, string?> Options { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Values { get; } = [];

    public bool HasOption(string name)
    {
        return Options.ContainsKey(name);
    }

    public string? GetOption(string name)
    {
        return Options.TryGetValue(name, out var value) ? value : null;
    }
}
