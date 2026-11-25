using System.Collections.Concurrent;
using System.Text.Json;
using DaHub.Core.Entities;
using DaHub.Core.Jobs;
using DaHub.Infrastructure.Jobs;
using DaHub.Infrastructure.Notifications;
using DaHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace DaHub.Tests.Integration;

internal static class ProvisioningSqlAssertions
{
    public static async Task VerifyAsync(string connection)
    {
        DaHubDbContext Open(bool fail = false) => new(new DbContextOptionsBuilder<DaHubDbContext>()
            .UseSqlServer(connection).AddInterceptors(new RejectOutcome(fail)).Options);
        await using var db = Open();
        await db.BackgroundJobs.Where(x => x.Status == "Queued").ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Cancelled"));
        var employee = new Employee
        {
            LegalFirstName = "Synthetic",
            LegalLastName = "Provisioning",
            Username = "mock.provisioning",
            Email = "mock.provisioning@example.test",
            EmploymentType = "Student",
            StartDate = new(2026, 11, 1)
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        employee.VerificationStatus = "Verified";
        employee.VerifiedByUPN = "synthetic.hr@example.test";
        employee.VerifiedAt = DateTime.UtcNow;
        db.AddRange(new EmployeeDepartment { EmployeeId = employee.Id, DepartmentId = await db.Departments.Select(x => x.Id).FirstAsync(), IsPrimary = true },
            new EmployeeLocation { EmployeeId = employee.Id, LocationId = await db.Locations.Select(x => x.Id).FirstAsync(), IsPrimary = true },
            new EmployeeJobTitle { EmployeeId = employee.Id, JobTitleId = await db.JobTitles.Select(x => x.Id).FirstAsync(), IsPrimary = true, AssignedBy = "synthetic.hr@example.test" });
        await db.SaveChangesAsync();
        var period = new EmploymentPeriod { EmployeeId = employee.Id, EmploymentType = "Student" };
        db.EmploymentPeriods.Add(period);
        await db.SaveChangesAsync();
        var actor = await db.AppUsers.AsNoTracking().FirstAsync();
        var request = new WorkflowRequest
        {
            RequestType = "Onboarding",
            EmployeeId = period.EmployeeId,
            EmploymentPeriodId = period.Id,
            SubmittedByUPN = "synthetic@example.test",
            Status = "InProgress",
            ExecutionStatus = "InProgress",
            ApprovalStatus = "Pending",
            IsUrgent = true,
            EHRRequired = true
        };
        db.Requests.Add(request);
        await db.SaveChangesAsync();
        db.RequestRevisions.Add(new()
        {
            RequestId = request.Id,
            Revision = 1,
            SnapshotJson = "{}",
            CreatedByUPN = "synthetic@example.test",
            ChangeReason = "Synthetic mock boundary"
        });
        await db.SaveChangesAsync();
        var original = JsonSerializer.Serialize(await db.Requests.AsNoTracking().SingleAsync(x => x.Id == request.Id));
        var authority = new Authority(request.Id, period.Id);
        var provider = new Provider(); // External ledger survives application contexts and local rollback.
        var alerts = new WorkerFailureAlerts(new Dictionary<string, string> { ["Teams"] = "SyntheticIT" });

        async Task<BackgroundJob> EnqueueAsync(string operation = "MockDirectory", int? prerequisite = null, string? key = null)
        {
            await using var context = Open();
            var step = new ProvisioningStep { RequestId = request.Id, StepName = "Synthetic step", TargetSystem = "Manual" };
            return await new SqlProvisioningActions(context).EnqueueAsync(step,
                new()
                {
                    ActionKey = key ?? Guid.NewGuid().ToString("N"),
                    RequestId = request.Id,
                    Revision = 1,
                    EmploymentPeriodId = period.Id,
                    InitiatedByUserId = actor.Id,
                    Operation = operation,
                    PrerequisiteStepId = prerequisite
                }, authority, CancellationToken.None);
        }
        async Task<BackgroundJob> ClaimAsync(long expected)
        {
            await using var context = Open();
            // Advance only this job's eligibility, not the persisted retry interval assertions.
            await context.BackgroundJobs.Where(x => x.Id == expected).ExecuteUpdateAsync(s => s.SetProperty(x => x.ScheduledAt, DateTime.UtcNow.AddMinutes(-1)));
            var claim = await new SqlJobStore(context).ClaimAsync(CancellationToken.None);
            Assert.NotNull(claim);
            Assert.Equal(expected, claim.Id);
            return claim;
        }
        async Task<JobResult> ExecuteAsync(BackgroundJob claim)
        {
            await using var context = Open();
            return await new ProvisioningStepHandler(new SqlProvisioningActions(context), authority, provider)
                .ExecuteAsync(claim, CancellationToken.None);
        }
        async Task FinishAsync(BackgroundJob claim, JobResult result)
        {
            await using var context = Open();
            await new SqlJobStore(context, alerts).FinishAsync(claim, result, CancellationToken.None);
        }
        async Task CancelAlertsAsync() => await db.BackgroundJobs.Where(x => x.JobType == "NotificationRetry" && x.Status == "Queued")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Cancelled"));

        var failing = await EnqueueAsync();
        var dependent = await EnqueueAsync(prerequisite: failing.ProvisioningStepId);
        var independent = await EnqueueAsync("MockHelpdesk");
        var first = await ClaimAsync(failing.Id);
        Assert.Equal(JobOutcome.Retry, (await ExecuteAsync(first)).Outcome);
        await FinishAsync(first, new(JobOutcome.Retry, "TransientFailure"));
        var next = await ClaimAsync(independent.Id);
        await FinishAsync(next, await ExecuteAsync(next));
        Assert.Equal("Completed", (await db.ProvisioningSteps.AsNoTracking().SingleAsync(x => x.Id == independent.ProvisioningStepId)).Status);
        Assert.Equal("Pending", (await db.ProvisioningSteps.AsNoTracking().SingleAsync(x => x.Id == dependent.ProvisioningStepId)).Status);
        Assert.Equal(0, (await db.BackgroundJobs.AsNoTracking().SingleAsync(x => x.Id == dependent.Id)).AttemptCount);
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            if (attempt > 1)
            {
                var claim = await ClaimAsync(failing.Id);
                await FinishAsync(claim, await ExecuteAsync(claim));
            }
            var saved = await db.BackgroundJobs.AsNoTracking().SingleAsync(x => x.Id == failing.Id);
            var evidence = await db.BackgroundJobAttempts.AsNoTracking().SingleAsync(x => x.JobId == failing.Id && x.Attempt == attempt);
            Assert.Equal(attempt, saved.AttemptCount);
            Assert.Equal(attempt < 4 ? "Queued" : "Failed", saved.Status);
            if (attempt < 4) Assert.Equal(JobRetryPolicy.Delay(attempt), saved.ScheduledAt - evidence.CompletedAt!.Value);
        }
        Assert.Equal(4, provider.Calls.Values.Sum() - 1); // One independent effect, four known-no-effect attempts.
        Assert.Equal(3, (await db.ProvisioningSteps.AsNoTracking().SingleAsync(x => x.Id == failing.ProvisioningStepId)).RetryCount);
        Assert.Single(await db.BackgroundJobs.Where(x => x.JobType == "NotificationRetry" && x.Status == "Queued").ToListAsync());
        await CancelAlertsAsync();
        // A dependent failed prerequisite never consumes attempts or blocks unrelated work.
        await using (var empty = Open()) Assert.Null(await new SqlJobStore(empty).ClaimAsync(CancellationToken.None));
        await db.BackgroundJobs.Where(x => x.Id == dependent.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Cancelled"));

        // Enqueue authorization, action-key uniqueness and rollback leave no orphan step/job.
        var stepCount = await db.ProvisioningSteps.CountAsync();
        authority.Allowed = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => EnqueueAsync());
        authority.Allowed = true;
        var usedKey = await db.ProvisioningActions.Where(x => x.StepId == independent.ProvisioningStepId).Select(x => x.ActionKey).SingleAsync();
        await Assert.ThrowsAsync<DbUpdateException>(() => EnqueueAsync(key: usedKey));
        Assert.Equal(stepCount, await db.ProvisioningSteps.CountAsync());
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dahub_ita.ProvisioningActions SET Revision = 2 WHERE StepId = {independent.ProvisioningStepId}"));
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE dahub_ita.ProvisioningActions WHERE StepId = {independent.ProvisioningStepId}"));
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dahub_ita.ProvisioningActions SET ActionKey = ActionKey + N' ' WHERE StepId = {independent.ProvisioningStepId}"));
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dahub_ita.ProvisioningActions SET Operation = N'mockhelpdesk' WHERE StepId = {independent.ProvisioningStepId}"));

        var readyDependent = await EnqueueAsync("MockHelpdesk", independent.ProvisioningStepId);
        var readyClaim = await ClaimAsync(readyDependent.Id);
        await FinishAsync(readyClaim, await ExecuteAsync(readyClaim));
        var contested = await EnqueueAsync("MockHelpdesk");
        var contestedClaim = await ClaimAsync(contested.Id);
        var ownerships = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var context = Open();
            return await new SqlProvisioningActions(context).TryOwnAsync(contestedClaim, CancellationToken.None);
        }));
        Assert.Equal(1, ownerships.Count(owned => owned));
        await FinishAsync(contestedClaim, new(JobOutcome.Failed, "PermanentFailure"));
        await CancelAlertsAsync();

        // A revoked authority, changed revision or period cannot contact the provider.
        foreach (var stale in new[] { "permission", "revision", "period" })
        {
            var job = await EnqueueAsync("MockHelpdesk");
            var claim = await ClaimAsync(job.Id);
            var calls = provider.Calls.Values.Sum();
            authority.Allowed = stale != "permission";
            if (stale == "revision") await db.Requests.Where(x => x.Id == request.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Revision, 2));
            if (stale == "period") await db.Requests.Where(x => x.Id == request.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.EmploymentPeriodId, (int?)null));
            var result = await ExecuteAsync(claim);
            Assert.Equal(JobOutcome.Cancelled, result.Outcome);
            await FinishAsync(claim, result);
            Assert.Equal(calls, provider.Calls.Values.Sum());
            await db.Requests.Where(x => x.Id == request.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Revision, 1).SetProperty(x => x.EmploymentPeriodId, period.Id));
            authority.Allowed = true;
        }

        foreach (var recovery in new[] { false, true })
        {
            // Provider accepts, then local outcome/audit/alert persistence fails. A second job
            // cannot steal intent. Fresh-context recovery marks uncertainty without reissuing.
            var job = await EnqueueAsync("MockHelpdesk");
            var claim = await ClaimAsync(job.Id);
            var result = await ExecuteAsync(claim);
            Assert.Equal(JobOutcome.Completed, result.Outcome);
            var duplicate = new BackgroundJob
            {
                JobType = "ProvisioningRetry",
                RequestId = request.Id,
                ProvisioningStepId = job.ProvisioningStepId,
                ScheduledAt = DateTime.UtcNow.AddMinutes(-1)
            };
            db.BackgroundJobs.Add(duplicate);
            await db.SaveChangesAsync();
            var competing = await ClaimAsync(duplicate.Id);
            Assert.Equal(JobOutcome.Cancelled, (await ExecuteAsync(competing)).Outcome);
            await FinishAsync(competing, new(JobOutcome.Cancelled, "StaleExecution"));
            var audits = await db.AuditLog.CountAsync(x => x.ActionType == "MockProvisioningOutcome");
            var alertCount = await db.BackgroundJobs.CountAsync(x => x.JobType == "NotificationRetry");
            if (recovery) await AgeAsync();
            await using (var failingContext = Open(fail: true))
            {
                var store = new SqlJobStore(failingContext, alerts);
                if (recovery) await Assert.ThrowsAsync<DbUpdateException>(() => store.RecoverAsync(CancellationToken.None));
                else await Assert.ThrowsAsync<DbUpdateException>(() => store.FinishAsync(claim, new(JobOutcome.Failed, "ReconciliationRequired"), CancellationToken.None));
            }
            Assert.Equal("InProgress", (await db.BackgroundJobs.AsNoTracking().SingleAsync(x => x.Id == job.Id)).Status);
            Assert.Equal("InProgress", (await db.BackgroundJobAttempts.AsNoTracking().SingleAsync(x => x.JobId == job.Id)).Outcome);
            Assert.Equal("InProgress", (await db.ProvisioningActions.AsNoTracking().SingleAsync(x => x.StepId == job.ProvisioningStepId)).State);
            Assert.Equal("InProgress", (await db.ProvisioningSteps.AsNoTracking().SingleAsync(x => x.Id == job.ProvisioningStepId)).Status);
            Assert.Equal(audits, await db.AuditLog.CountAsync(x => x.ActionType == "MockProvisioningOutcome"));
            Assert.Equal(alertCount, await db.BackgroundJobs.CountAsync(x => x.JobType == "NotificationRetry"));
            await AgeAsync();
            await using (var fresh = Open())
            {
                var store = new SqlJobStore(fresh, alerts);
                Assert.Equal(1, await store.RecoverAsync(CancellationToken.None));
                Assert.Equal(0, await store.RecoverAsync(CancellationToken.None));
                await Assert.ThrowsAsync<InvalidOperationException>(() => store.FinishAsync(claim, result, CancellationToken.None));
            }
            Assert.Equal("ReconciliationRequired", (await db.ProvisioningActions.AsNoTracking().SingleAsync(x => x.StepId == job.ProvisioningStepId)).State);
            Assert.Equal(alertCount + 1, await db.BackgroundJobs.CountAsync(x => x.JobType == "NotificationRetry"));
            await CancelAlertsAsync();
            // Explicit replacement for reconciliation reuses the same durable action, and only
            // a confirmed external result can complete it. No manual retry endpoint is exposed.
            var replacement = new BackgroundJob
            {
                JobType = "ProvisioningRetry",
                RequestId = request.Id,
                ProvisioningStepId = job.ProvisioningStepId,
                ScheduledAt = DateTime.UtcNow.AddMinutes(-1)
            };
            db.BackgroundJobs.Add(replacement);
            await db.SaveChangesAsync();
            var reconciliation = await ClaimAsync(replacement.Id);
            await FinishAsync(reconciliation, await ExecuteAsync(reconciliation));
            var action = await db.ProvisioningActions.AsNoTracking().SingleAsync(x => x.StepId == job.ProvisioningStepId);
            Assert.Equal("Completed", action.State);
            Assert.Equal(1, provider.Calls[action.ActionKey]);
            Assert.Equal("synthetic-id", action.ExternalId);
            Assert.Equal("synthetic-evidence", action.EvidenceReference);
            async Task AgeAsync() => await db.BackgroundJobs.Where(x => x.Id == job.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StartedAt, DateTime.UtcNow.AddMinutes(-6)));
        }
        Assert.Equal(original, JsonSerializer.Serialize(await db.Requests.AsNoTracking().SingleAsync(x => x.Id == request.Id)));
    }

    private sealed class Authority(int request, int period) : IJobExecutionAuthority
    {
        public bool Allowed { get; set; } = true;
        public Task<bool> IsCurrentAsync(JobExecutionIdentity identity, CancellationToken cancellationToken) =>
            Task.FromResult(Allowed && identity.RequestId == request && identity.Revision == 1 && identity.EmploymentPeriodId == period);
    }

    private sealed class Provider : IMockProvisioningProvider
    {
        public ConcurrentDictionary<string, int> Calls { get; } = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, ProvisioningResult> ledger = new(StringComparer.Ordinal);
        public Task<ProvisioningResult> ReconcileAsync(ProvisioningAction action, CancellationToken cancellationToken) =>
            Task.FromResult(ledger.GetValueOrDefault(action.ActionKey) ?? new(ExternalResultState.Absent));
        public Task<ProvisioningResult> ExecuteAsync(ProvisioningAction action, CancellationToken cancellationToken)
        {
            Calls.AddOrUpdate(action.ActionKey, 1, (_, count) => count + 1);
            if (action.Operation == "MockDirectory") return Task.FromResult(new ProvisioningResult(ExternalResultState.Absent, Retryable: true));
            var result = new ProvisioningResult(ExternalResultState.Confirmed, new("synthetic-id", "synthetic-evidence"));
            ledger[action.ActionKey] = result;
            return Task.FromResult(result);
        }
    }

    private sealed class RejectOutcome(bool fail) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (fail && eventData.Context!.ChangeTracker.Entries<AuditLogEntry>()
                .Any(x => x.State == EntityState.Added && x.Entity.ActionType == "MockProvisioningOutcome"))
                throw new DbUpdateException("Synthetic atomic outcome failure");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
