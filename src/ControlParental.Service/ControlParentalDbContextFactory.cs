// <copyright file="ControlParentalDbContextFactory.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

internal sealed class ControlParentalDbContextFactory : IDesignTimeDbContextFactory<ControlParentalDbContext>
{
    public ControlParentalDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ControlParentalDbContext>()
            .UseSqlite("Data Source=controlparental-design-time.db")
            .Options;

        return new ControlParentalDbContext(options);
    }
}
