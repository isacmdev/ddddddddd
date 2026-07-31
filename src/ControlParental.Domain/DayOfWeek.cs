// <copyright file="DayOfWeek.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Days of the week used in schedules and allowed windows.
/// Values match the snake_case JSON format: MON, TUE, WED, THU, FRI, SAT, SUN.
/// </summary>
public enum DayOfWeek
{
    /// <summary>
    /// Monday.
    /// </summary>
    MON,

    /// <summary>
    /// Tuesday.
    /// </summary>
    TUE,

    /// <summary>
    /// Wednesday.
    /// </summary>
    WED,

    /// <summary>
    /// Thursday.
    /// </summary>
    THU,

    /// <summary>
    /// Friday.
    /// </summary>
    FRI,

    /// <summary>
    /// Saturday.
    /// </summary>
    SAT,

    /// <summary>
    /// Sunday.
    /// </summary>
    SUN
}
