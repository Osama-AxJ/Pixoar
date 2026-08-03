using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Pixoar.App.Behaviors;

/// <summary>
/// Synchronizes a ListView's selected items with a view-model collection.
/// </summary>
public static class ListViewSelectionBehavior
{
    /// <summary>
    /// Identifies the SelectedItems attached property.
    /// </summary>
    public static readonly DependencyProperty SelectedItemsProperty =
        DependencyProperty.RegisterAttached(
            "SelectedItems",
            typeof(IList),
            typeof(ListViewSelectionBehavior),
            new PropertyMetadata(null, OnSelectedItemsChanged));

    /// <summary>
    /// Gets the synchronized selected items collection.
    /// </summary>
    /// <param name="element">The target ListView.</param>
    /// <returns>The bound selected items collection.</returns>
    public static IList? GetSelectedItems(DependencyObject element)
    {
        return (IList?)element.GetValue(SelectedItemsProperty);
    }

    /// <summary>
    /// Sets the synchronized selected items collection.
    /// </summary>
    /// <param name="element">The target ListView.</param>
    /// <param name="value">The selected items collection.</param>
    public static void SetSelectedItems(DependencyObject element, IList? value)
    {
        element.SetValue(SelectedItemsProperty, value);
    }

    private static void OnSelectedItemsChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not ListView listView)
        {
            return;
        }

        listView.SelectionChanged -= OnSelectionChanged;
        listView.PreviewKeyDown -= OnPreviewKeyDown;

        if (e.NewValue is not null)
        {
            listView.SelectionChanged += OnSelectionChanged;
            listView.PreviewKeyDown += OnPreviewKeyDown;
        }
    }

    private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        var selectedItems = GetSelectedItems(listView);
        if (selectedItems is null)
        {
            return;
        }

        foreach (var item in e.AddedItems)
        {
            if (!selectedItems.Contains(item))
            {
                selectedItems.Add(item);
            }
        }

        foreach (var item in e.RemovedItems)
        {
            selectedItems.Remove(item);
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            listView.SelectAll();
            e.Handled = true;
        }
    }

}
