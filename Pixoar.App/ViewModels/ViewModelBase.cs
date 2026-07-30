using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Pixoar.App.ViewModels;

/// <summary>
/// Base class for WPF view models that need property change notification.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Sets a property backing field and raises change notification when the value changed.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="field">The backing field to update.</param>
    /// <param name="value">The new value.</param>
    /// <param name="propertyName">The property name supplied by the compiler.</param>
    /// <returns>True when the value changed; otherwise false.</returns>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Raises property change notification.
    /// </summary>
    /// <param name="propertyName">The property that changed.</param>
    protected void OnPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
