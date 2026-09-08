namespace Pixoar.App.Models;

/// <summary>
/// Identifies a sortable column in the desktop image list.
/// </summary>
public enum ImageListSortColumn
{
    /// <summary>
    /// Sorts by the displayed file name.
    /// </summary>
    FileName,

    /// <summary>
    /// Sorts by pixel area, then width and height.
    /// </summary>
    Resolution,

    /// <summary>
    /// Sorts by normalized image format.
    /// </summary>
    Format,

    /// <summary>
    /// Sorts by the source file size in bytes.
    /// </summary>
    Size
}
