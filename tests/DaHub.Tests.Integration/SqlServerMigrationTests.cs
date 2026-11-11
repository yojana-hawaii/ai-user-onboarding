using System.Net;
using DaHub.Core.Lookups;
using DaHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD")))
        {
            Skip = "Run scripts/test-dev-database.ps1 for isolated SQL Server checks.";
        }
    }
}

public sealed class SqlServerMigrationTests
{
    [SqlServerFact]
    public async Task MigrationsConstraintsRollbackAndRestrictedApiIdentityWorkOnRealSqlServer()
    {
        // The target is fixed to the local Compose service; never use a caller-supplied database.
        var suffix = Guid.NewGuid().ToString("N");
        var databaseName = "DaHub_Test_" + suffix;
        var login = "dahub_test_" + suffix;
        var password = "Aa1!" + Guid.NewGuid().ToString("N");
        var admin = new SqlConnectionStringBuilder
        {
            DataSource = "sql,1433",
            InitialCatalog = "master",
            UserID = "sa",
            Password = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD"),
            Encrypt = true,
            TrustServerCertificate = true
        };
        await using var server = new SqlConnection(admin.ConnectionString);
        await server.OpenAsync();
        try
        {
            await ExecuteAsync(server, $"CREATE DATABASE [{databaseName}]");
            admin.InitialCatalog = databaseName;
            await using var context = CreateContext(admin.ConnectionString);
            var baseline = context.Database.GetMigrations().First();
            await context.GetService<IMigrator>().MigrateAsync(baseline);
            var location = new Location { Name = "Synthetic clinic", SiteCode = "TEST" };
            context.Locations.Add(location);
            await context.SaveChangesAsync();
            await context.Database.MigrateAsync();
            await context.Database.MigrateAsync();
            Assert.False(context.Database.HasPendingModelChanges());
            Assert.Equal(2, (await context.Database.GetAppliedMigrationsAsync()).Count());

            await using var connection = new SqlConnection(admin.ConnectionString);
            await connection.OpenAsync();
            await using (var tables = new SqlCommand(
                "SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dahub_ita') AND name<>'__EFMigrationsHistory'", connection))
            {
                Assert.Equal(31, Convert.ToInt32(await tables.ExecuteScalarAsync()));
            }

            Assert.Equal("Synthetic clinic", (await context.Locations.SingleAsync(x => x.SiteCode == "TEST")).Name);
            await FirstReleaseSchemaAssertions.VerifyAsync(connection);
            Assert.True(location.Id > 0);
            Assert.True(location.IsActive);
            Assert.True(location.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
            context.Locations.Add(new Location { Name = "Duplicate", SiteCode = "test" });
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.IsType<SqlException>(duplicate.InnerException);
            context.ChangeTracker.Clear();

            await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(connection,
                "INSERT dahub_ita.Departments (Name,OUPath) VALUES (N'Synthetic',NULL)"));
            await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(connection,
                "INSERT dahub_ita.Locations (Name,SiteCode) VALUES (REPLICATE(N'x',151),N'LONG')"));
            await ExecuteAsync(connection, "INSERT dahub_ita.JobTitles (Title) VALUES (N'Synthetic job')");
            await ExecuteAsync(connection, "INSERT dahub_ita.Specialties (Name) VALUES (N'Synthetic specialty')");
            await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(connection,
                "INSERT dahub_ita.JobTitles (Title) VALUES (N'Synthetic job')"));
            await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(connection,
                "INSERT dahub_ita.Specialties (Name) VALUES (N'Synthetic specialty')"));

            await ExecuteAsync(server, $"CREATE LOGIN [{login}] WITH PASSWORD=N'{password}', CHECK_POLICY=ON");
            await ExecuteAsync(connection, $"CREATE USER [{login}] FOR LOGIN [{login}]; GRANT CONNECT TO [{login}];");
            await ExecuteAsync(connection, $"ALTER ROLE [dahub_app_runtime] ADD MEMBER [{login}]");
            var runtime = new SqlConnectionStringBuilder(admin.ConnectionString) { UserID = login, Password = password };
            await using (var runtimeContext = CreateContext(DatabaseConnection.Validate(runtime.ConnectionString, true)))
            {
                Assert.Equal(9, await runtimeContext.Locations.CountAsync());
                await FirstReleaseSchemaAssertions.VerifyRuntimePermissionsAsync(runtimeContext);
                runtimeContext.Departments.Add(new Department { Name = "Synthetic IT", OUPath = "OU=Synthetic,DC=example,DC=test" });
                await runtimeContext.SaveChangesAsync();
                await Assert.ThrowsAsync<SqlException>(() => runtimeContext.Database.ExecuteSqlRawAsync(
                    "CREATE TABLE dahub_ita.Forbidden (Id int NOT NULL)"));
            }

            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DaHub"] = runtime.ConnectionString,
                        ["Database:ConnectionStringFile"] = ""
                    }));
            });
            using var client = factory.CreateClient();
            using (var ready = await client.GetAsync("/api/health/ready"))
            {
                Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
            }

            // Partial rollback must preserve the original lookup row and avoid duplicate site seeds on upgrade.
            await context.GetService<IMigrator>().MigrateAsync(baseline);
            Assert.Equal("Synthetic clinic", (await context.Locations.AsNoTracking().SingleAsync(x => x.SiteCode == "TEST")).Name);
            await context.Database.MigrateAsync();
            Assert.Equal(9, await context.Locations.CountAsync());

            // Exercise Down on this disposable database, never the developer's DaHub database.
            await context.GetService<IMigrator>().MigrateAsync("0");
            using (var ready = await client.GetAsync("/api/health/ready"))
            {
                Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
            }
            using (var live = await client.GetAsync("/api/health/live"))
            {
                Assert.Equal(HttpStatusCode.OK, live.StatusCode);
            }
            await context.Database.MigrateAsync();
            Assert.Equal(8, await context.Locations.CountAsync());
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteAsync(server, $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]");
            await ExecuteAsync(server, $"IF SUSER_ID(N'{login}') IS NOT NULL DROP LOGIN [{login}]");
        }
    }

    private static DaHubDbContext CreateContext(string connection) => new(
        new DbContextOptionsBuilder<DaHubDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", DaHubDbContext.Schema)).Options);

    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
