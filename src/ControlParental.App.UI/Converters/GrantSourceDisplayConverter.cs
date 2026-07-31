// <copyright file="GrantSourceDisplayConverter.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Converters;

using ControlParental.Domain;
using Microsoft.UI.Xaml.Data;

/// <summary>
/// Converts a <see cref="GrantSource"/> to a Spanish display string for the UI.
/// </summary>
public sealed class GrantSourceDisplayConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            GrantSource.ExtraTime => "Tiempo extra pedido",
            GrantSource.Reward => "Recompensa",
            GrantSource.Manual => "Manual",
            _ => "Desconocido",
        };
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
