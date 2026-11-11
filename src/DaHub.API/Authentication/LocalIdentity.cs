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
    public const string IssuerClaim = "dahub:issuer";
    public const string SubjectClaim = "dahub:subject";

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
            new Claim(ClaimTypes.Upn, record.User.UPN ?? $"AppUser:{record.User.Id}"),
            new Claim(IdentityClaim, record.Identity.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(IssuerClaim, record.Identity.Issuer),
            new Claim(SubjectClaim, record.Identity.Subject),
            new Claim(VersionClaim, record.User.AuthorizationVersion.ToString(CultureInfo.InvariantCulture))
        };
        claims.AddRange((await permissions.ResolveAsync(record.User.Id, cancellationToken))
            .Select(permission => new Claim(PermissionResolver.PermissionClaim, permission)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthRegistration.CookieScheme));
    }

    public async Task<ClaimsPrincipal?> RefreshAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var issuer = principal.FindFirstValue(IssuerClaim);
        var subject = principal.FindFirstValue(SubjectClaim);
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !int.TryParse(principal.FindFirstValue(IdentityClaim), out var identityId)
            || !int.TryParse(principal.FindFirstValue(VersionClaim), out var version)
            || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(subject))
            return null;
        var user = await (from identity in database.AppExternalIdentities.AsNoTracking()
                          join account in database.AppUsers.AsNoTracking() on identity.AppUserId equals account.Id
                          where identity.Id == identityId && identity.AppUserId == userId
                              && identity.Issuer == issuer && identity.Subject == subject
                              && identity.ProviderKey == "Okta" && identity.IsActive && identity.RevokedAt == null && account.IsActive
                          select account).SingleOrDefaultAsync(cancellationToken);
        if (user is null)
            return null;
        if (user.AuthorizationVersion == version)
            return principal;

        // Never label old permission claims with a new version. A concurrent change fails closed.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Upn, user.UPN ?? $"AppUser:{userId}"),
            new(IdentityClaim, identityId.ToString(CultureInfo.InvariantCulture)),
            new(IssuerClaim, issuer),
            new(SubjectClaim, subject),
            new(VersionClaim, user.AuthorizationVersion.ToString(CultureInfo.InvariantCulture))
        };
        claims.AddRange((await permissions.ResolveAsync(userId, cancellationToken))
            .Select(permission => new Claim(PermissionResolver.PermissionClaim, permission)));
        var refreshed = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthRegistration.CookieScheme));
        return await IsCurrentAsync(refreshed, cancellationToken) ? refreshed : null;
    }

    public async Task<bool> IsCurrentAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var issuer = principal.FindFirstValue(IssuerClaim);
        var subject = principal.FindFirstValue(SubjectClaim);
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !int.TryParse(principal.FindFirstValue(IdentityClaim), out var identityId)
            || !int.TryParse(principal.FindFirstValue(VersionClaim), out var version)
            || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(subject))
            return false;
        return await (from identity in database.AppExternalIdentities.AsNoTracking()
                      join user in database.AppUsers.AsNoTracking() on identity.AppUserId equals user.Id
                      where identity.Id == identityId && identity.AppUserId == userId
                          && identity.Issuer == issuer && identity.Subject == subject
                          && identity.ProviderKey == "Okta" && identity.IsActive && identity.RevokedAt == null
                          && user.IsActive && user.AuthorizationVersion == version
                      select user.Id).AnyAsync(cancellationToken);
    }
}
