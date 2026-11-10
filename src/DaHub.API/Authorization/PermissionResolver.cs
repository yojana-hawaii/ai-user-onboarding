using System.Security.Claims;
using DaHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DaHub.API.Authorization;

public sealed class PermissionResolver(DaHubDbContext database)
{
    public const string PermissionClaim = "dahub:permission";

    public async Task<string[]> ResolveAsync(int userId, CancellationToken cancellationToken) =>
        await (from user in database.AppUsers.AsNoTracking()
               join assignment in database.AppUserRoles on user.Id equals assignment.UserId
               join role in database.AppRoles on assignment.RoleId equals role.Id
               join grant in database.AppRolePermissions on role.Id equals grant.RoleId
               join permission in database.AppPermissions on grant.PermissionId equals permission.Id
               where user.Id == userId && user.IsActive && role.IsActive && permission.IsActive
               select permission.Name).Distinct().ToArrayAsync(cancellationToken);

    // Only locally constructed principals are stored in the server-side ticket store.
    public static string[] Cached(ClaimsPrincipal user) => user.FindAll(PermissionClaim)
        .Select(claim => claim.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
