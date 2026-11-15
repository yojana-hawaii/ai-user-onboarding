using System.ComponentModel.DataAnnotations;
using System.Data;
using DaHub.Core.Employees;
using DaHub.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DaHub.Infrastructure.Persistence;

public sealed partial class DaHubDbContext
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        var changes = ChangeTracker.Entries().Where(x =>
            (x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted) &&
            (x.Entity is Employee or EmployeeDepartment or EmployeeLocation or EmployeeJobTitle or EmployeeSpecialty or WorkflowRequest)).ToArray();
        if (changes.Length == 0)
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);

        // SQL datetime2 has no Kind; reject local input before the round trip loses that information.
        if (changes.Any(x => x.Entity is Employee { VerificationStatus: "Verified", VerifiedAt.Kind: DateTimeKind.Local }))
            throw new ValidationException("HR verification timestamps must use UTC.");

        // Validate the resulting persisted aggregate, including untracked assignments, before committing.
        // Serializable range locks prevent concurrent verification and assignment removal from both succeeding.
        if (System.Transactions.Transaction.Current is not null)
            throw new InvalidOperationException("Employee profile writes require an explicit EF transaction.");
        var existing = Database.CurrentTransaction;
        if (existing is not null && (existing.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable || !existing.SupportsSavepoints))
            throw new InvalidOperationException("Employee profile writes require a serializable transaction with savepoints.");

        await using var owned = existing is null
            ? await Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false) : null;
        const string savepoint = "EmployeeProfileGuard";
        if (existing is not null) await existing.CreateSavepointAsync(savepoint, cancellationToken).ConfigureAwait(false);
        try
        {
            var count = await base.SaveChangesAsync(false, cancellationToken).ConfigureAwait(false);
            var employeeIds = new HashSet<int>();
            var workflowEmployees = new HashSet<int>();
            foreach (var entry in changes)
            {
                if (entry.Entity is Employee employee) employeeIds.Add(employee.Id);
                else
                {
                    var property = entry.Property("EmployeeId");
                    employeeIds.Add((int)property.CurrentValue!);
                    if (entry.State != EntityState.Added) employeeIds.Add((int)property.OriginalValue!);
                    if (entry.Entity is WorkflowRequest && entry.State != EntityState.Deleted)
                        workflowEmployees.Add((int)property.CurrentValue!);
                }
            }

            foreach (var id in employeeIds.Order())
            {
                var employee = await Employees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
                if (workflowEmployees.Contains(id) && employee?.VerificationStatus != "Verified")
                    throw new ValidationException("Only HR-verified workers may enter lifecycle workflows.");
                if (employee?.VerificationStatus != "Verified") continue;
                EmployeeProfile.ValidateVerified(employee,
                    await EmployeeDepartments.Where(x => x.EmployeeId == id).Select(x => x.IsPrimary).ToArrayAsync(cancellationToken).ConfigureAwait(false),
                    await EmployeeLocations.Where(x => x.EmployeeId == id).Select(x => x.IsPrimary).ToArrayAsync(cancellationToken).ConfigureAwait(false),
                    await EmployeeJobTitles.Where(x => x.EmployeeId == id).Select(x => x.IsPrimary).ToArrayAsync(cancellationToken).ConfigureAwait(false),
                    await EmployeeSpecialties.Where(x => x.EmployeeId == id).Select(x => x.IsPrimary).ToArrayAsync(cancellationToken).ConfigureAwait(false));
            }

            if (owned is not null) await owned.CommitAsync(cancellationToken).ConfigureAwait(false);
            else await existing!.ReleaseSavepointAsync(savepoint, cancellationToken).ConfigureAwait(false);
            if (acceptAllChangesOnSuccess) ChangeTracker.AcceptAllChanges();
            return count;
        }
        catch
        {
            if (existing is not null) await existing.RollbackToSavepointAsync(savepoint, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}
