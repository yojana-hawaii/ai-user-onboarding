using System.Security.Claims;
using DaHub.API.Authentication;
using DaHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace DaHub.API.Authorization;

public sealed record UrgentAction(int RequestId, int Revision, string Justification);
public sealed record DelegateAssignment(int ManagerEmployeeId);
public sealed record AuthorityRequirement(string Operation) : IAuthorizationRequirement;

public sealed class AuthorityHandler(IServiceProvider services)
    : AuthorizationHandler<AuthorityRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AuthorityRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true || !await services.GetRequiredService<LocalIdentity>().IsCurrentAsync(context.User, CancellationToken.None))
            return;
        var database = services.GetRequiredService<DaHubDbContext>();
        bool Has(string key) => context.User.HasClaim(PermissionResolver.PermissionClaim, key);
        var allowed = false;
        if (requirement.Operation == "Urgent" && context.Resource is UrgentAction urgent
            && !string.IsNullOrWhiteSpace(urgent.Justification)
            && Has("provisioning.execute") && Has("provisioning.urgent"))
        {
            allowed = await (from request in database.Requests.AsNoTracking()
                             join revision in database.RequestRevisions on new { RequestId = request.Id, request.Revision }
                                 equals new { revision.RequestId, revision.Revision }
                             where request.Id == urgent.RequestId && request.Revision == urgent.Revision
                                 && request.IsUrgent && (request.RequestType == "Onboarding" || request.RequestType == "Offboarding")
                                 && request.ExecutionStatus == "NotStarted" && request.ApprovalStatus == "Pending"
                                 && (request.Status == "Submitted" || request.Status == "PendingApproval")
                             select request.Id).AnyAsync();
        }
        else if (requirement.Operation == "Delegation" && context.Resource is DelegateAssignment delegation)
        {
            allowed = Has("approvals.delegate.manage");
            if (!allowed && Has("approvals.delegate.own")
                && int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                allowed = await database.AppUsers.AsNoTracking().AnyAsync(user => user.Id == userId
                    && user.EmployeeId == delegation.ManagerEmployeeId);
        }
        else if (requirement.Operation == "Retention")
            allowed = Has("retention.manage");
        if (allowed)
            context.Succeed(requirement);
    }
}
