using DaHub.API.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace DaHub.API.Authorization;

public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute("Permission:" + permission);

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class PermissionPolicyProvider(Microsoft.Extensions.Options.IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) =>
        policyName.StartsWith("Permission:", StringComparison.Ordinal)
            ? Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(policyName[11..])).Build())
            : base.GetPolicyAsync(policyName);
}

public sealed class PermissionHandler(IServiceProvider services) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // Revalidate immediately before authorization, including resource/service authorization calls.
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.HasClaim(PermissionResolver.PermissionClaim, requirement.Permission)
            && await services.GetRequiredService<LocalIdentity>().IsCurrentAsync(context.User, CancellationToken.None))
            context.Succeed(requirement);
    }
}

public sealed class AuthorizationProblemHandler : IAuthorizationMiddlewareResultHandler
{
    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
    {
        if (result.Succeeded)
        {
            await next(context);
            return;
        }
        var status = result.Forbidden ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized;
        await Results.Problem(statusCode: status,
            title: result.Forbidden ? "Forbidden" : "Unauthorized",
            detail: result.Forbidden ? "You do not have permission to perform this operation." : "A current application session is required.")
            .ExecuteAsync(context);
    }
}
