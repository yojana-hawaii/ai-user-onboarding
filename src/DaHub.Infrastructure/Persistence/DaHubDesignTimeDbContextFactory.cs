using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DaHub.Infrastructure.Persistence;

public sealed class DaHubDesignTimeDbContextFactory : IDesignTimeDbContextFactory<DaHubDbContext>
{
    public DaHubDbContext CreateDbContext(string[] args)
    {
        // The placeholder supports offline scaffolding and SQL script generation only.
        var path = Environment.GetEnvironmentVariable("DAHUB_MIGRATION_CONNECTION_FILE");
        var connection = string.IsNullOrWhiteSpace(path)
            ? "Server=localhost;Database=DaHub;Integrated Security=true;Encrypt=true"
            : File.ReadAllText(path).Trim();
        var options = new DbContextOptionsBuilder<DaHubDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", DaHubDbContext.Schema))
            .Options;
        return new DaHubDbContext(options);
    }
}
