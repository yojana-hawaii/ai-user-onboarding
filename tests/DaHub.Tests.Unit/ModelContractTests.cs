using DaHub.Core.Entities;
using DaHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using Xunit;

namespace DaHub.Tests.Unit;

public sealed class ModelContractTests
{
    [Fact]
    public void SqlModelHasExpectedTablesKeysAndNoMigrationDrift()
    {
        using var context = new DaHubDesignTimeDbContextFactory().CreateDbContext([]);
        var model = context.Model;
        var entities = model.GetEntityTypes().ToArray();
        Assert.Equal(31, entities.Length);
        Assert.All(entities, entity =>
        {
            Assert.Equal(DaHubDbContext.Schema, entity.GetSchema());
            Assert.NotNull(entity.FindPrimaryKey());
        });
        var employee = model.FindEntityType(typeof(Employee))!;
        Assert.Contains(employee.GetIndexes(), index => index.IsUnique);
        Assert.All(employee.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.NoAction, key.DeleteBehavior));
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
