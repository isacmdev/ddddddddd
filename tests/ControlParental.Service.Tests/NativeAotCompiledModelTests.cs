// <copyright file="NativeAotCompiledModelTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Service;
using ControlParental.Service.CompiledModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class NativeAotCompiledModelTests
{
    [Fact]
    public void NativeAotServiceOptions_UseTheGeneratedCompiledModel()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ControlParentalDbContext>();

        Program.ConfigureDbContextOptions(options, connection.ConnectionString, useCompiledModel: true);

        using var context = new ControlParentalDbContext(options.Options);

        Assert.Same(ControlParentalDbContextModel.Instance, context.Model);
    }
}
