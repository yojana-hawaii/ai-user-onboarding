using System.Data;
using DaHub.API.Authentication;
using DaHub.API.Authorization;
using DaHub.Core.Entities;
using DaHub.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace DaHub.Tests.Integration;

internal static class EnrollmentSqlAssertions
{
    public static async Task BootstrapAsync(string connectionString)
    {
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "bootstrap-admin.sql"));
        async Task RunAsync()
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(script, connection);
            command.Parameters.Add("@Issuer", SqlDbType.NVarChar, 400).Value = EnrollmentTests.Settings.Issuer;
            command.Parameters.Add("@Subject", SqlDbType.NVarChar, 255).Value = "bootstrap-subject";
            command.Parameters.Add("@DisplayName", SqlDbType.NVarChar, 200).Value = "Synthetic first administrator";
            command.Parameters.Add("@VerificationReference", SqlDbType.NVarChar, 200).Value = "verification:test-bootstrap";
            await command.ExecuteNonQueryAsync();
        }
        await using var control = new SqlConnection(connectionString);
        await control.OpenAsync();
        async Task Execute(string sql)
        {
            await using var command = new SqlCommand(sql, control);
            await command.ExecuteNonQueryAsync();
        }
        await Execute("CREATE TRIGGER dahub_ita.TestRejectBootstrapAudit ON dahub_ita.AuditLog AFTER INSERT AS THROW 51001, 'Synthetic audit failure', 1;");
        try { await Assert.ThrowsAsync<SqlException>(RunAsync); }
        finally { await Execute("DROP TRIGGER dahub_ita.TestRejectBootstrapAudit"); }
        await using (var empty = new SqlCommand("SELECT COUNT(*) FROM dahub_ita.AppUsers", control))
            Assert.Equal(0, Convert.ToInt32(await empty.ExecuteScalarAsync()));
        async Task<bool> Attempt()
        {
            try { await RunAsync(); return true; }
            catch (SqlException exception) when (exception.Number == 51000) { return false; }
        }
        Assert.Single(await Task.WhenAll(Attempt(), Attempt()), x => x);
        await using (var audit = new SqlCommand("SELECT COUNT(*) FROM dahub_ita.AuditLog WHERE ActionType='AdminBootstrapped' AND ActorUserId IS NULL AND ISJSON(NewValueJson)=1", control))
            Assert.Equal(1, Convert.ToInt32(await audit.ExecuteScalarAsync()));
        await Execute("UPDATE dahub_ita.AppUsers SET IsActive=0");
        Assert.Equal(51000, (await Assert.ThrowsAsync<SqlException>(RunAsync)).Number);
        // Simulate loss of every bootstrap account in this disposable database: audit still retires setup.
        await Execute("DELETE dahub_ita.AppUserRoles; DELETE dahub_ita.AppExternalIdentities; DELETE dahub_ita.AppUsers;");
        Assert.Equal(51000, (await Assert.ThrowsAsync<SqlException>(RunAsync)).Number);
        // Owner-only cleanup of disposable synthetic evidence before unrelated schema assertions.
        await Execute("DELETE dahub_ita.AuditLog WHERE ActionType='AdminBootstrapped'");
    }

    public static async Task VerifyAsync(string connection)
    {
        DaHubDbContext Open(IInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<DaHubDbContext>().UseSqlServer(connection);
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return new DaHubDbContext(options.Options);
        }
        EnrollmentService Service(DaHubDbContext db) => new(db, new LocalIdentity(db, TimeProvider.System, new PermissionResolver(db)), EnrollmentTests.Settings, TimeProvider.System);
        await using var database = Open();
        var actor = new AppUser { DisplayName = "Synthetic enrollment administrator" };
        database.AppUsers.Add(actor);
        await database.SaveChangesAsync();
        database.AppExternalIdentities.Add(new AppExternalIdentity
        {
            AppUserId = actor.Id,
            ProviderKey = "Okta",
            Issuer = EnrollmentTests.Settings.Issuer,
            Subject = "enrollment-admin",
            LinkMethod = "Bootstrap"
        });
        database.AppUserRoles.Add(new AppUserRole { UserId = actor.Id, RoleId = 5, AssignedBy = "system:test" });
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        var identities = new LocalIdentity(database, TimeProvider.System, new PermissionResolver(database));
        var principal = (await identities.SignInAsync(EnrollmentTests.Settings.Issuer, "enrollment-admin", default))!;
        var userId = await Service(database).EnrollAsync(EnrollmentTests.Input("sql-enrolled"), principal, default);
        await Service(database).EnrollAsync(EnrollmentTests.Input("sql-second", userId), principal, default);
        await Service(database).EnrollAsync(EnrollmentTests.Input("SQL-ENROLLED"), principal, default);
        Assert.Equal(2, await database.AppExternalIdentities.CountAsync(x => x.AppUserId == userId));
        Assert.Empty(await database.AppUserRoles.Where(x => x.UserId == userId).ToArrayAsync());
        database.AppUserRoles.Add(new AppUserRole { UserId = userId, RoleId = 1, AssignedBy = "system:test" });
        database.AppExternalIdentities.Add(new AppExternalIdentity
        {
            AppUserId = userId,
            ProviderKey = "FutureProvider",
            Issuer = "https://future.example.invalid",
            Subject = "future-schema-only",
            LinkedByUserId = actor.Id,
            LinkMethod = "Admin"
        });
        await database.SaveChangesAsync();
        var user = await database.AppUsers.SingleAsync(x => x.Id == userId);
        user.UPN = "changed@example.invalid";
        // Existing verified schema fixture employee: linking must be explicit and unique.
        var employeeId = await database.Employees.Where(x => x.VerificationStatus == "Verified"
            && !database.AppUsers.Any(u => u.EmployeeId == x.Id)).Select(x => x.Id).FirstAsync();
        await database.SaveChangesAsync();
        await Service(database).LinkEmployeeAsync(userId, new(employeeId, "verification:sql"), principal, default);
        await Assert.ThrowsAsync<EnrollmentConflictException>(() => Service(database).LinkEmployeeAsync(actor.Id, new(employeeId, "verification:sql"), principal, default));
        var first = await identities.SignInAsync(EnrollmentTests.Settings.Issuer, "sql-enrolled", default);
        var second = await identities.SignInAsync(EnrollmentTests.Settings.Issuer, "sql-second", default);
        Assert.Equal(first!.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value,
            second!.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await database.Entry(user).ReloadAsync();
        Assert.Equal(employeeId, user.EmployeeId);
        Assert.Equal("changed@example.invalid", user.UPN);
        Assert.NotNull(user.LastLoginAt);
        Assert.Equal(1, await database.AppUserRoles.CountAsync(x => x.UserId == userId));
        Assert.Null(await identities.SignInAsync("https://future.example.invalid", "future-schema-only", default));
        var before = await database.AppUsers.CountAsync();
        await using (var failing = Open(new RejectAudit()))
            await Assert.ThrowsAsync<DbUpdateException>(() => Service(failing).EnrollAsync(EnrollmentTests.Input("sql-rollback"), principal, default));
        Assert.Equal(before, await database.AppUsers.CountAsync());
        Assert.False(await database.AppExternalIdentities.AnyAsync(x => x.Subject == "sql-rollback"));
        async Task<bool> Attempt()
        {
            await using var db = Open();
            try { await Service(db).EnrollAsync(EnrollmentTests.Input("sql-concurrent"), principal, default); return true; }
            catch (EnrollmentConflictException) { return false; }
            catch (DbUpdateException exception) when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 or 1205 }) { return false; }
            catch (Exception exception) when (exception.GetBaseException() is SqlException { Number: 1205 }) { return false; }
        }
        Assert.Single(await Task.WhenAll(Attempt(), Attempt()), x => x);
        Assert.Equal(1, await database.AppExternalIdentities.CountAsync(x => x.Subject == "sql-concurrent"));
        var link = await database.AppExternalIdentities.SingleAsync(x => x.Subject == "sql-enrolled");
        var old = (await identities.SignInAsync(EnrollmentTests.Settings.Issuer, "sql-enrolled", default))!;
        await Service(database).RevokeAsync(link.Id, principal, default);
        Assert.False(await identities.IsCurrentAsync(old, default));
        await database.Database.ExecuteSqlRawAsync("UPDATE dahub_ita.AppUsers SET IsActive=0 WHERE Id={0}", actor.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(database).EnrollAsync(EnrollmentTests.Input("sql-stale"), principal, default));
        Assert.False(await database.AppExternalIdentities.AnyAsync(x => x.Subject == "sql-stale"));
    }

    private sealed class RejectAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AuditLogEntry>().Any(x => x.State == EntityState.Added))
                throw new DbUpdateException("Synthetic audit storage failure");
            return ValueTask.FromResult(result);
        }
    }
}
