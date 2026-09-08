namespace Pixoar.App.Models;

/// <summary>
/// Provides deterministic, metadata-based ordering for desktop image-list entries.
/// </summary>
public static class ImageListSorter
{
    /// <summary>
    /// Sorts image entries by the selected column without recreating the entries.
    /// </summary>
    /// <param name="images">The entries to order.</param>
    /// <param name="column">The column to sort by.</param>
    /// <param name="ascending">Whether to use ascending order.</param>
    /// <returns>The ordered entries.</returns>
    public static IReadOnlyList<ImageFileItem> Sort(
        IEnumerable<ImageFileItem> images,
        ImageListSortColumn column,
        bool ascending)
    {
        ArgumentNullException.ThrowIfNull(images);

        return column switch
        {
            ImageListSortColumn.FileName => Order(images, image => image.FileName, ascending),
            ImageListSortColumn.Resolution => OrderByResolution(images, ascending),
            ImageListSortColumn.Format => Order(images, image => image.NormalizedFormat, ascending),
            ImageListSortColumn.Size => Order(images, image => image.FileSizeBytes, ascending),
            _ => images.ToArray()
        };
    }

    private static IReadOnlyList<ImageFileItem> Order<TKey>(
        IEnumerable<ImageFileItem> images,
        Func<ImageFileItem, TKey> primaryKey,
        bool ascending)
    {
        var ordered = ascending
            ? images.OrderBy(primaryKey)
            : images.OrderByDescending(primaryKey);

        return (ascending
            ? ordered.ThenBy(image => image.FileName, StringComparer.OrdinalIgnoreCase)
            : ordered.ThenByDescending(image => image.FileName, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    private static IReadOnlyList<ImageFileItem> OrderByResolution(
        IEnumerable<ImageFileItem> images,
        bool ascending)
    {
        return (ascending
            ? images.OrderBy(image => image.PixelArea)
                .ThenBy(image => image.PixelWidth)
                .ThenBy(image => image.PixelHeight)
                .ThenBy(image => image.FileName, StringComparer.OrdinalIgnoreCase)
            : images.OrderByDescending(image => image.PixelArea)
                .ThenByDescending(image => image.PixelWidth)
                .ThenByDescending(image => image.PixelHeight)
                .ThenByDescending(image => image.FileName, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }
}
