// <copyright file="InverseBoolToVisibilityConverter.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Converters;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

/// <inheritdoc/>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        return value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
    {
        return value is Visibility v && v != Visibility.Visible;
    }
}
