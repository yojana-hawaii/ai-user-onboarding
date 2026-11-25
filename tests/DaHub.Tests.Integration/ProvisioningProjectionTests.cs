using DaHub.Core.Entities;
using DaHub.Core.Jobs;
using DaHub.Infrastructure.Jobs;
using DaHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class ProvisioningProjectionTests
{
    [Theory]
    [InlineData("Queued", "TransientFailure", "Retry", "Pending")]
    [InlineData("Failed", "PermanentFailure", "Failed", "Failed")]
    [InlineData("Failed", "ReconciliationRequired", "ReconciliationRequired", "Failed")]
    [InlineData("Cancelled", "StaleExecution", "Failed", "Failed")]
    [InlineData("Completed", null, "Completed", "Completed")]
    public async Task ProjectsOwnedOutcomesWithoutChangingParentOrImpersonatingInitiator(
        string status, string? code, string actionState, string stepState)
    {
        await using var db = Database();
        var step = new ProvisioningStep { Id = 1, RequestId = 2, StepName = "Synthetic", TargetSystem = "Manual", RetryCount = 3 };
        var action = new ProvisioningAction
        {
            StepId = 1,
            RequestId = 2,
            InitiatedByUserId = 7,
            ActionKey = "synthetic",
            Operation = "MockHelpdesk",
            State = "InProgress",
            OwnerJobId = 3,
            OwnerAttempt = 2
        };
        db.AddRange(step, action);
        await db.SaveChangesAsync();
        var job = new BackgroundJob
        {
            Id = 3,
            JobType = "ProvisioningRetry",
            ProvisioningStepId = 1,
            AttemptCount = 2,
            StartedAt = DateTime.UtcNow
        };
        var now = DateTime.UtcNow;
        var store = new SqlProvisioningActions(db);
        var receipt = status == "Completed" ? new ProvisioningReceipt("synthetic-id", "synthetic-evidence") : null;
        await store.ProjectAsync(job, status, code, now, receipt, CancellationToken.None);
        await db.SaveChangesAsync();
        Assert.Equal(actionState, action.State);
        Assert.Equal(stepState, step.Status);
        Assert.Equal(3, step.RetryCount); // A reconciliation replacement cannot erase earlier retries.
        Assert.Equal(code, step.ResultDetail);
        Assert.Equal(status == "Queued" ? null : (DateTime?)now, step.CompletedAt);
        Assert.Equal(receipt?.ExternalId, action.ExternalId);
        var audit = Assert.Single(await db.AuditLog.ToListAsync());
        Assert.Null(audit.ActorUserId);
        Assert.Equal("DaHub.Worker", audit.ActorUPN);
        Assert.Contains("InitiatedByUserId=7;JobId=3;Attempt=2", audit.Detail);
        // A different job cannot overwrite the accepted result, even with purported evidence.
        await store.ProjectAsync(withId(4), "Failed", "PermanentFailure", now, null, CancellationToken.None);
        Assert.Equal(actionState, action.State);
        Assert.Single(db.AuditLog);
        BackgroundJob withId(long id) => new() { Id = id, JobType = job.JobType, ProvisioningStepId = 1, AttemptCount = 2 };
    }

    [Fact]
    public async Task RejectsCompletionWithoutEvidenceAndIgnoresOtherJobTypes()
    {
        await using var db = Database();
        db.ProvisioningActions.Add(new()
        {
            StepId = 1,
            OwnerJobId = 3,
            OwnerAttempt = 1,
            State = "InProgress",
            ActionKey = "synthetic",
            Operation = "MockDirectory"
        });
        await db.SaveChangesAsync();
        var store = new SqlProvisioningActions(db);
        var job = new BackgroundJob { Id = 3, JobType = "ProvisioningRetry", ProvisioningStepId = 1, AttemptCount = 1 };
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ProjectAsync(job, "Completed", null,
            DateTime.UtcNow, null, CancellationToken.None));
        job.JobType = "ApprovalReminder";
        await store.ProjectAsync(job, "Failed", null, DateTime.UtcNow, null, CancellationToken.None);
        Assert.Empty(db.AuditLog);
    }

    private static DaHubDbContext Database() => new(new DbContextOptionsBuilder<DaHubDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
