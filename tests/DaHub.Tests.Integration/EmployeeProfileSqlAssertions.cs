using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using DaHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace DaHub.Tests.Integration;

internal static class EmployeeProfileSqlAssertions
{
    public static async Task VerifyAsync(string connectionString)
    {
        await using var db = new DaHubDbContext(new DbContextOptionsBuilder<DaHubDbContext>()
            .UseSqlServer(connectionString).Options);
        // Caller supplies only the disposable SQL test database and its restricted runtime identity.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var department = new Department { Name = "Profile test", OUPath = "OU=Test,DC=example,DC=test" };
        var title = new JobTitle { Title = "Profile test" };
        var specialty = new Specialty { Name = "Profile test" };
        db.AddRange(department, title, specialty);
        await db.SaveChangesAsync();
        var employee = new Employee
        {
            LegalFirstName = "Synthetic",
            LegalLastName = "Profile",
            Username = "profile.test",
            Email = "profile@example.test",
            EmploymentType = "Student"
        };
        db.Add(employee);
        await db.SaveChangesAsync();
        var id = employee.Id;
        Assert.Null(employee.StartDate);

        var request = new WorkflowRequest { EmployeeId = id, RequestType = "Onboarding", SubmittedByUPN = "hr@example.test" };
        db.Add(request);
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.False(await db.Requests.AnyAsync(x => x.EmployeeId == id));

        employee = await db.Employees.SingleAsync(x => x.Id == id);
        employee.VerificationStatus = "Verified";
        employee.VerifiedByUPN = "hr@example.test";
        employee.VerifiedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local);
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
        employee.VerifiedAt = DateTime.UtcNow;
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
        employee.StartDate = new DateOnly(2026, 11, 1);
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
        Assert.Equal("PendingHR", (await db.Employees.AsNoTracking().SingleAsync(x => x.Id == id)).VerificationStatus);

        var locationId = await db.Locations.Select(x => x.Id).FirstAsync();
        db.AddRange(new EmployeeDepartment { EmployeeId = id, DepartmentId = department.Id, IsPrimary = true },
            new EmployeeLocation { EmployeeId = id, LocationId = locationId, IsPrimary = true },
            new EmployeeJobTitle { EmployeeId = id, JobTitleId = title.Id, IsPrimary = true, AssignedBy = "hr@example.test" });
        // Verification and assignments can be saved atomically. Null manager is HR-confirmed by this attestation.
        db.SaveChanges();
        Assert.Equal("Verified", (await db.Employees.AsNoTracking().SingleAsync(x => x.Id == id)).VerificationStatus);
        db.ChangeTracker.Clear();

        var primary = await db.EmployeeDepartments.SingleAsync(x => x.EmployeeId == id);
        db.Remove(primary);
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.True(await db.EmployeeDepartments.AnyAsync(x => x.EmployeeId == id && x.IsPrimary));

        var job = await db.EmployeeJobTitles.SingleAsync(x => x.EmployeeId == id);
        job.IsPrimary = false;
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        var extra = new EmployeeSpecialty { EmployeeId = id, SpecialtyId = specialty.Id, AssignedBy = "hr@example.test" };
        db.Add(extra);
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
        // Rollback leaves the context's attempted changes unaccepted; discard/reload after a failed write.
        db.ChangeTracker.Clear();
        Assert.False(await db.EmployeeSpecialties.AnyAsync(x => x.EmployeeId == id));
        db.Add(new EmployeeSpecialty { EmployeeId = id, SpecialtyId = specialty.Id, IsPrimary = true, AssignedBy = "hr@example.test" });
        await db.SaveChangesAsync(false);
        db.ChangeTracker.AcceptAllChanges();

        db.Add(new WorkflowRequest { EmployeeId = id, RequestType = "Onboarding", SubmittedByUPN = "hr@example.test" });
        await db.SaveChangesAsync();
        Assert.True(await db.Requests.AnyAsync(x => x.EmployeeId == id));

        employee = await db.Employees.SingleAsync(x => x.Id == id);
        employee.StartDate = null;
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        employee = await db.Employees.SingleAsync(x => x.Id == id);
        employee.ManagerId = id;
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // Reassigning a tracked junction must validate the original worker as well as the destination.
        var pending = new Employee
        {
            LegalFirstName = "Synthetic",
            LegalLastName = "Other",
            Username = "profile.other",
            Email = "other@example.test",
            EmploymentType = "Temporary"
        };
        db.Add(pending);
        await db.SaveChangesAsync();
        var oldPrimary = await db.EmployeeLocations.SingleAsync(x => x.EmployeeId == id);
        db.Remove(oldPrimary);
        db.Add(new EmployeeLocation { EmployeeId = pending.Id, LocationId = locationId, IsPrimary = true });
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        await transaction.RollbackAsync();
        await transaction.DisposeAsync();
        await using var weak = await db.Database.BeginTransactionAsync();
        db.Add(new Employee
        {
            LegalFirstName = "Synthetic",
            LegalLastName = "Weak",
            Username = "weak",
            Email = "weak@example.test",
            EmploymentType = "FT"
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await weak.DisposeAsync();
        await VerifyConcurrentRemovalAsync(connectionString);
    }

    private sealed class RemovalSignal : DbCommandInterceptor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("DELETE FROM [dahub_ita].[EmployeeDepartments]", StringComparison.Ordinal))
                Started.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }

    private static async Task VerifyConcurrentRemovalAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<DaHubDbContext>().UseSqlServer(connectionString).Options;
        await using var verify = new DaHubDbContext(options);
        var department = new Department { Name = "Concurrent profile", OUPath = "OU=Test,DC=example,DC=test" };
        var title = new JobTitle { Title = "Concurrent profile" };
        var employee = new Employee
        {
            LegalFirstName = "Synthetic",
            LegalLastName = "Concurrent",
            Username = "profile.concurrent",
            Email = "concurrent@example.test",
            EmploymentType = "Temporary",
            StartDate = new DateOnly(2026, 11, 1)
        };
        verify.AddRange(department, title, employee);
        await verify.SaveChangesAsync();
        var id = employee.Id;
        verify.AddRange(new EmployeeDepartment { EmployeeId = id, DepartmentId = department.Id, IsPrimary = true },
            new EmployeeLocation { EmployeeId = id, LocationId = await verify.Locations.Select(x => x.Id).FirstAsync(), IsPrimary = true },
            new EmployeeJobTitle { EmployeeId = id, JobTitleId = title.Id, IsPrimary = true, AssignedBy = "hr@example.test" });
        await verify.SaveChangesAsync();

        var signal = new RemovalSignal();
        await using var remove = new DaHubDbContext(new DbContextOptionsBuilder<DaHubDbContext>()
            .UseSqlServer(connectionString).AddInterceptors(signal).Options);
        remove.Remove(await remove.EmployeeDepartments.SingleAsync(x => x.EmployeeId == id));
        await using var transaction = await verify.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        employee.VerificationStatus = "Verified";
        employee.VerifiedByUPN = "hr@example.test";
        employee.VerifiedAt = DateTime.UtcNow;
        await verify.SaveChangesAsync();
        var removal = remove.SaveChangesAsync();
        try
        {
            await signal.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(removal.IsCompleted);
            await transaction.CommitAsync();
            await Assert.ThrowsAsync<ValidationException>(() => removal);
        }
        finally
        {
            await transaction.DisposeAsync();
            // Observe any in-flight operation before disposing its context, including assertion failures.
            try { await removal; }
            catch (ValidationException) { }
        }
        Assert.True(await verify.EmployeeDepartments.AsNoTracking().AnyAsync(x => x.EmployeeId == id && x.IsPrimary));
        Assert.Equal("Verified", (await verify.Employees.AsNoTracking().SingleAsync(x => x.Id == id)).VerificationStatus);
    }
}
