using System.Data;
using System.Security.Claims;
using System.Text.Json;
using DaHub.API.Authorization;
using DaHub.Core.Entities;
using DaHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DaHub.API.Authentication;

public sealed record EnrollIdentity(int? AppUserId, string DisplayName, string? UPN, string Issuer, string Subject,
    string VerificationReference);
public sealed record VerifyEmployeeLink(int? EmployeeId, string VerificationReference);
public sealed class EnrollmentConflictException : Exception;

public sealed class EnrollmentService(DaHubDbContext database, LocalIdentity identities, OktaSettings settings, TimeProvider clock)
{
    private static bool Text(string? value, int maximum) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= maximum && value == value.Trim() && !value.Any(char.IsControl);

    private static void ValidateEvidence(string reference)
    {
        if (!Text(reference, 200)) throw new ArgumentException("A bounded verification reference is required.");
    }

    private async Task<AppUser> AuthorizeAsync(ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        if (!actor.HasClaim(PermissionResolver.PermissionClaim, "users.manage")
            || !await identities.IsCurrentAsync(actor, cancellationToken)) throw new UnauthorizedAccessException();
        var id = int.Parse(actor.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture);
        return await database.AppUsers.SingleAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<int> EnrollAsync(EnrollIdentity input, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        ValidateEvidence(input.VerificationReference);
        if (!settings.Enabled || input.Issuer != settings.Issuer || !Text(input.Issuer, 400)
            || !Text(input.Subject, 255) || !Text(input.DisplayName, 200)
            || (input.UPN is not null && !Text(input.UPN, 200)))
            throw new ArgumentException("Verified exact Okta identity and local display fields are required.");
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var administrator = await AuthorizeAsync(actor, cancellationToken);
        // SQL equality also rejects padded aliases. Never move or reactivate an existing identity binding.
        if (await database.AppExternalIdentities.AnyAsync(x => x.Issuer == input.Issuer && x.Subject == input.Subject, cancellationToken))
            throw new EnrollmentConflictException();
        AppUser user;
        if (input.AppUserId is { } userId)
        {
            user = await database.AppUsers.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken)
                ?? throw new EnrollmentConflictException();
            // Existing contact data, employee link and local roles are not supplied by this identity.
        }
        else
        {
            user = new AppUser
            {
                DisplayName = input.DisplayName,
                UPN = input.UPN,
                CreatedAt = clock.GetUtcNow().UtcDateTime,
                UpdatedAt = clock.GetUtcNow().UtcDateTime
            };
            database.AppUsers.Add(user);
            await database.SaveChangesAsync(cancellationToken);
        }
        var identity = new AppExternalIdentity
        {
            AppUserId = user.Id,
            ProviderKey = "Okta",
            Issuer = input.Issuer,
            Subject = input.Subject,
            LinkedByUserId = administrator.Id,
            LinkMethod = "Admin",
            LinkedAt = clock.GetUtcNow().UtcDateTime
        };
        database.AppExternalIdentities.Add(identity);
        await database.SaveChangesAsync(cancellationToken);
        Audit(administrator, "IdentityEnrolled", new { AppUserId = user.Id, IdentityId = identity.Id, input.VerificationReference });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return user.Id;
    }

    public async Task LinkEmployeeAsync(int userId, VerifyEmployeeLink input, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        ValidateEvidence(input.VerificationReference);
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var administrator = await AuthorizeAsync(actor, cancellationToken);
        var user = await database.AppUsers.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken)
            ?? throw new EnrollmentConflictException();
        if (input.EmployeeId is { } employeeId
            && (!await database.Employees.AnyAsync(x => x.Id == employeeId && x.VerificationStatus == "Verified", cancellationToken)
                || await database.AppUsers.AnyAsync(x => x.Id != userId && x.EmployeeId == employeeId, cancellationToken)))
            throw new EnrollmentConflictException();
        var previous = user.EmployeeId;
        user.EmployeeId = input.EmployeeId;
        user.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        Audit(administrator, "EmployeeLinkVerified", new { AppUserId = user.Id, PreviousEmployeeId = previous, input.EmployeeId, input.VerificationReference });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeAsync(int identityId, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var administrator = await AuthorizeAsync(actor, cancellationToken);
        var identity = await database.AppExternalIdentities.SingleOrDefaultAsync(x => x.Id == identityId, cancellationToken)
            ?? throw new EnrollmentConflictException();
        identity.IsActive = false;
        identity.RevokedAt ??= clock.GetUtcNow().UtcDateTime;
        Audit(administrator, "IdentityRevoked", new { identity.AppUserId, IdentityId = identity.Id });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private void Audit(AppUser actor, string action, object details) => database.AuditLog.Add(new AuditLogEntry
    {
        ActorUserId = actor.Id,
        ActorUPN = actor.UPN ?? $"AppUser:{actor.Id}",
        ActionType = action,
        AffectedSystem = "App",
        Result = "Success",
        NewValueJson = JsonSerializer.Serialize(details),
        TimestampUTC = clock.GetUtcNow().UtcDateTime
    });
}
