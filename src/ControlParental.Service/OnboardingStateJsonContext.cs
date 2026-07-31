// <copyright file="OnboardingStateJsonContext.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Text.Json.Serialization;
using ControlParental.Domain;

/// <summary>
/// Source-generated JSON metadata for persisted onboarding state.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(OnboardingState))]
public sealed partial class OnboardingStateJsonContext : JsonSerializerContext
{
}
