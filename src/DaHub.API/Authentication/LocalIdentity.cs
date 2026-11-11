using System.Globalization;
using DaHub.API.Authorization;
using System.Security.Claims;
using DaHub.Core.Entities;
using DaHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DaHub.API.Authentication;

public sealed class LocalIdentity(DaHubDbContext database, TimeProvider clock, PermissionResolver permissions)
{
    public const string IdentityClaim = "dahub:identity";
    public const string VersionClaim = "dahub:authorization-version";

    public async Task<ClaimsPrincipal?> SignInAsync(string issuer, string subject, CancellationToken cancellationToken)
    {
        var record = await (from identity in database.AppExternalIdentities
                            join user in database.AppUsers on identity.AppUserId equals user.Id
                            where identity.ProviderKey == "Okta" && identity.Issuer == issuer && identity.Subject == subject
                                && identity.IsActive && identity.RevokedAt == null && user.IsActive
                            select new { Identity = identity, User = user }).SingleOrDefaultAsync(cancellationToken);
        // SQL uses a binary collation for issuer/subject; also enforce exact equality in the application.
        if (record is null || record.Identity.Issuer != issuer || record.Identity.Subject != subject)
            return null;

        record.User.LastLoginAt = clock.GetUtcNow().UtcDateTime;
        database.AuditLog.Add(new AuditLogEntry
        {
            ActorUserId = record.User.Id,
            ActorUPN = record.User.UPN ?? $"AppUser:{record.User.Id}",
            ActionType = "Login",
            AffectedSystem = "App",
            Result = "Success",
            TimestampUTC = clock.GetUtcNow().UtcDateTime
        });
        await database.SaveChangesAsync(cancellationToken);
        // Provider groups, email and roles never enter the application principal.
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, record.User.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, record.User.DisplayName),
            new Claim(IdentityClaim, record.Identity.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(VersionClaim, record.User.AuthorizationVersion.ToString(CultureInfo.InvariantCulture))
        };
        claims.AddRange((await permissions.ResolveAsync(record.User.Id, cancellationToken))
            .Select(permission => new Claim(PermissionResolver.PermissionClaim, permission)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthRegistration.CookieScheme));
    }

    public async Task<bool> IsCurrentAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !int.TryParse(principal.FindFirstValue(IdentityClaim), out var identityId)
            || !int.TryParse(principal.FindFirstValue(VersionClaim), out var version))
            return false;
        return await (from identity in database.AppExternalIdentities.AsNoTracking()
                      join user in database.AppUsers.AsNoTracking() on identity.AppUserId equals user.Id
                      where identity.Id == identityId && identity.AppUserId == userId
                          && identity.ProviderKey == "Okta" && identity.IsActive && identity.RevokedAt == null
                          && user.IsActive && user.AuthorizationVersion == version
                      select user.Id).AnyAsync(cancellationToken);
    }
}
