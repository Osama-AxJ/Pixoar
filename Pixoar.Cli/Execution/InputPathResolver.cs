using Pixoar.Core.Interfaces;

namespace Pixoar.Cli.Execution;

internal sealed class InputPathResolver(IImageFormatDetector formatDetector)
{
    public IReadOnlyList<string> Resolve(IEnumerable<string> paths, bool recursive)
    {
        var results = new List<string>();

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                if (formatDetector.IsSupported(path))
                {
                    results.Add(Path.GetFullPath(path));
                }

                continue;
            }

            if (!Directory.Exists(path))
            {
                throw new FileNotFoundException($"Input path was not found: {path}", path);
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            results.AddRange(Directory
                .EnumerateFiles(path, "*.*", searchOption)
                .Where(formatDetector.IsSupported)
                .Select(Path.GetFullPath));
        }

        return results.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
