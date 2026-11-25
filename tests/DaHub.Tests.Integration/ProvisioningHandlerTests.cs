using DaHub.Core.Entities;
using DaHub.Core.Jobs;
using DaHub.Infrastructure.Jobs;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class ProvisioningHandlerTests
{
    [Theory]
    [InlineData(ExternalResultState.Confirmed, false, JobOutcome.Completed)]
    [InlineData(ExternalResultState.Absent, true, JobOutcome.Retry)]
    [InlineData(ExternalResultState.Absent, false, JobOutcome.Failed)]
    [InlineData(ExternalResultState.Uncertain, true, JobOutcome.Failed)]
    public async Task MapsOnlyKnownNoEffectFailuresToRetries(ExternalResultState state, bool retry, JobOutcome outcome)
    {
        var fixture = new Fixture { Result = new(state, new("synthetic-id", "synthetic-evidence"), retry) };
        var result = await fixture.RunAsync();
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(1, fixture.Executions);
        Assert.Equal(outcome == JobOutcome.Completed, result.ProvisioningReceipt is not null);
    }

    [Fact]
    public async Task RejectsMissingStaleAndCompetingActionsWithoutProviderCalls()
    {
        foreach (var scenario in new[] { "missing", "stale", "competing" })
        {
            var fixture = new Fixture { Missing = scenario == "missing", Current = scenario != "stale", Owned = scenario != "competing" };
            Assert.Equal(JobOutcome.Cancelled, (await fixture.RunAsync()).Outcome);
            Assert.Equal(0, fixture.Executions);
            Assert.Equal(0, fixture.Reconciliations);
        }
    }

    [Fact]
    public async Task RevocationDuringReconciliationPreventsTheEffect()
    {
        var fixture = new Fixture { RevokeDuringReconciliation = true };
        Assert.Equal(JobOutcome.Cancelled, (await fixture.RunAsync()).Outcome);
        Assert.Equal(0, fixture.Executions);
        Assert.Equal(2, fixture.AuthorityChecks);
    }

    [Theory]
    [InlineData(ExternalResultState.Confirmed, JobOutcome.Completed)]
    [InlineData(ExternalResultState.Absent, JobOutcome.Failed)]
    [InlineData(ExternalResultState.Uncertain, JobOutcome.Failed)]
    public async Task InterruptedEffectsAreReconciledAndNeverReplayed(ExternalResultState state, JobOutcome outcome)
    {
        var fixture = new Fixture { Ledger = new(state, new("accepted-id", "accepted-evidence")) };
        fixture.Action.State = "ReconciliationRequired";
        Assert.Equal(outcome, (await fixture.RunAsync()).Outcome);
        Assert.Equal(0, fixture.Executions);
    }

    [Theory]
    [InlineData("", "evidence")]
    [InlineData("raw response with spaces", "evidence")]
    [InlineData("id", "https://unapproved.example.test/raw")]
    [InlineData(null, null)]
    public async Task InvalidEvidenceRequiresReconciliation(string? id, string? evidence)
    {
        var fixture = new Fixture
        {
            Result = new(ExternalResultState.Confirmed,
            id is null ? null : new(id, evidence!))
        };
        var result = await fixture.RunAsync();
        Assert.Equal("ReconciliationRequired", result.Code);
        Assert.Null(result.ProvisioningReceipt);
    }

    [Fact]
    public async Task ProviderExceptionEscapesForTheWorkersUncertainOutcomeHandling()
    {
        var fixture = new Fixture { Throw = true };
        await Assert.ThrowsAsync<InvalidOperationException>(fixture.RunAsync);
    }

    private sealed class Fixture : IProvisioningActionStore, IJobExecutionAuthority, IMockProvisioningProvider
    {
        public ProvisioningAction Action { get; } = new()
        {
            ActionKey = "synthetic-key",
            Operation = "MockHelpdesk",
            RequestId = 1,
            Revision = 2,
            EmploymentPeriodId = 3,
            StepId = 4
        };
        public bool Missing { get; init; }
        public bool Current { get; set; } = true;
        public bool Owned { get; init; } = true;
        public bool RevokeDuringReconciliation { get; init; }
        public bool Throw { get; init; }
        public int Executions { get; private set; }
        public int Reconciliations { get; private set; }
        public int AuthorityChecks { get; private set; }
        public ProvisioningResult Ledger { get; init; } = new(ExternalResultState.Absent);
        public ProvisioningResult Result { get; init; } = new(ExternalResultState.Confirmed, new("id", "evidence"));
        public Task<JobResult> RunAsync() => new ProvisioningStepHandler(this, this, this)
            .ExecuteAsync(new() { JobType = "ProvisioningRetry" }, CancellationToken.None);
        public Task<ProvisioningAction?> FindAsync(BackgroundJob job, CancellationToken cancellationToken) => Task.FromResult(Missing ? null : Action);
        public Task<bool> TryOwnAsync(BackgroundJob job, CancellationToken cancellationToken) => Task.FromResult(Owned);
        public Task<bool> IsCurrentAsync(JobExecutionIdentity identity, CancellationToken cancellationToken)
        {
            Assert.Equal(Action.ActionKey, identity.ActionKey);
            Assert.Equal(Action.RequestId, identity.RequestId);
            Assert.Equal(Action.Revision, identity.Revision);
            Assert.Equal(Action.EmploymentPeriodId, identity.EmploymentPeriodId);
            AuthorityChecks++;
            return Task.FromResult(Current);
        }
        public Task<ProvisioningResult> ReconcileAsync(ProvisioningAction action, CancellationToken cancellationToken)
        {
            Reconciliations++;
            if (RevokeDuringReconciliation) Current = false;
            return Task.FromResult(Ledger);
        }
        public Task<ProvisioningResult> ExecuteAsync(ProvisioningAction action, CancellationToken cancellationToken)
        {
            Executions++;
            if (Throw) throw new InvalidOperationException("Synthetic unknown acceptance");
            return Task.FromResult(Result);
        }
    }
}
