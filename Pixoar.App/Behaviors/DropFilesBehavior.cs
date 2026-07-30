using System.Windows;
using System.Windows.Input;

namespace Pixoar.App.Behaviors;

/// <summary>
/// Adds file-drop command support to WPF elements.
/// </summary>
public static class DropFilesBehavior
{
    /// <summary>
    /// Identifies the DropCommand attached property.
    /// </summary>
    public static readonly DependencyProperty DropCommandProperty =
        DependencyProperty.RegisterAttached(
            "DropCommand",
            typeof(ICommand),
            typeof(DropFilesBehavior),
            new PropertyMetadata(null, OnDropCommandChanged));

    /// <summary>
    /// Gets the command executed when files are dropped.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <returns>The configured command.</returns>
    public static ICommand? GetDropCommand(DependencyObject element)
    {
        return (ICommand?)element.GetValue(DropCommandProperty);
    }

    /// <summary>
    /// Sets the command executed when files are dropped.
    /// </summary>
    /// <param name="element">The target element.</param>
    /// <param name="value">The command to execute.</param>
    public static void SetDropCommand(DependencyObject element, ICommand? value)
    {
        element.SetValue(DropCommandProperty, value);
    }

    private static void OnDropCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not UIElement element)
        {
            return;
        }

        element.AllowDrop = e.NewValue is not null;
        element.PreviewDragOver -= OnPreviewDragOver;
        element.Drop -= OnDrop;

        if (e.NewValue is not null)
        {
            element.PreviewDragOver += OnPreviewDragOver;
            element.Drop += OnDrop;
        }
    }

    private static void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        if (sender is not DependencyObject target ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        var command = GetDropCommand(target);
        if (command?.CanExecute(paths) == true)
        {
            command.Execute(paths);
        }
    }
}
