// <copyright file="ServiceRunningToVisibilityConverter.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Converters;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

/// <inheritdoc/>
public sealed class ServiceRunningToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        return value is string status && status == "Running" ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
    {
        throw new NotImplementedException();
    }
}
