namespace Pixoar.Cli.Arguments;

internal static class CommandLineParser
{
    public static ParsedCommandOptions Parse(IReadOnlyList<string> values)
    {
        var result = new ParsedCommandOptions();

        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            if (!value.StartsWith("--", StringComparison.Ordinal))
            {
                result.Values.Add(value);
                continue;
            }

            var optionName = value[2..];
            if (string.IsNullOrWhiteSpace(optionName))
            {
                continue;
            }

            if (IsSwitchOption(optionName))
            {
                result.Options[optionName] = "true";
                continue;
            }

            if (index + 1 >= values.Count || values[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                result.Options[optionName] = null;
                continue;
            }

            result.Options[optionName] = values[index + 1];
            index++;
        }

        return result;
    }

    private static bool IsSwitchOption(string optionName)
    {
        return string.Equals(optionName, "recursive", StringComparison.OrdinalIgnoreCase)
            || string.Equals(optionName, "json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(optionName, "quiet", StringComparison.OrdinalIgnoreCase)
            || string.Equals(optionName, "remove-user-data", StringComparison.OrdinalIgnoreCase);
    }
}
