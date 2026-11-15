using DaHub.Core.Entities;
using DaHub.Core.Lookups;
using Microsoft.EntityFrameworkCore;

namespace DaHub.Infrastructure.Persistence;

internal static class SchemaSeeds
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        // Fixed values keep migrations deterministic. No user or user-role assignment is seeded.
        var createdAt = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<AppRole>().HasData(
            new AppRole { Id = 1, Name = "HR", Description = "Submit onboarding, offboarding, transfer, name change, and access requests", IsActive = true, CreatedAt = createdAt },
            new AppRole { Id = 2, Name = "IT", Description = "Execute provisioning tasks, retry failed steps, view all requests and full audit logs", IsActive = true, CreatedAt = createdAt },
            new AppRole { Id = 3, Name = "Manager", Description = "Approve or reject requests for direct reports; requests escalate up if unresponsive", IsActive = true, CreatedAt = createdAt },
            new AppRole { Id = 4, Name = "Executive", Description = "Read-only access to dashboards, metrics, and compliance reports", IsActive = true, CreatedAt = createdAt },
            new AppRole { Id = 5, Name = "Admin", Description = "Template management, user administration, integration settings; urgency requires separate designation", IsActive = true, CreatedAt = createdAt },
            new AppRole { Id = 6, Name = "Compliance", Description = "Read-only audit log search and export for HIPAA/HRSA reviews", IsActive = true, CreatedAt = createdAt },
            new AppRole { Id = 7, Name = "UrgentOperator", Description = "Supplemental permission for designated IT staff; requires IT execution permission", IsActive = true, CreatedAt = createdAt });
        modelBuilder.Entity<AppPermission>().HasData(
            new AppPermission { Id = 1, Name = "requests.submit", Category = "Requests", Description = "Submit new onboarding, offboarding, transfer, name change, and access requests", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 2, Name = "requests.view.own", Category = "Requests", Description = "View status of own submitted requests", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 3, Name = "requests.view.all", Category = "Requests", Description = "View all requests regardless of submitter", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 4, Name = "requests.cancel", Category = "Requests", Description = "Cancel a request before execution begins", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 5, Name = "provisioning.execute", Category = "Provisioning", Description = "Execute provisioning steps on approved requests", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 6, Name = "provisioning.retry", Category = "Provisioning", Description = "Retry a failed provisioning step", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 7, Name = "provisioning.skip", Category = "Provisioning", Description = "Mark a provisioning step as Skipped with reason", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 8, Name = "provisioning.override", Category = "Provisioning", Description = "Mark a provisioning step as Completed via Manual Override", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 9, Name = "approvals.action", Category = "Approvals", Description = "Approve or reject requests assigned to this user", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 10, Name = "provisioning.urgent", Category = "Provisioning", Description = "Execute urgent onboarding/offboarding with justification; requires provisioning.execute", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 11, Name = "approvals.delegate.own", Category = "Approvals", Description = "Manage dated delegation of own manager approvals", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 12, Name = "approvals.delegate.manage", Category = "Approvals", Description = "HR/Admin backup assignment of dated delegates", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 13, Name = "retention.manage", Category = "Provisioning", Description = "IT authorization of mailbox and OneDrive actions", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 14, Name = "templates.view", Category = "Templates", Description = "View access templates and their group assignments", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 15, Name = "templates.manage", Category = "Templates", Description = "Create, edit, clone, and deactivate access templates", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 16, Name = "audit.search", Category = "Audit", Description = "Search and filter the audit log", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 17, Name = "audit.export", Category = "Audit", Description = "Export filtered audit log records to CSV or PDF", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 18, Name = "users.manage", Category = "Users", Description = "Create, edit, and deactivate app users and role assignments", IsActive = true, CreatedAt = createdAt },
            new AppPermission { Id = 19, Name = "dashboard.view", Category = "Dashboard", Description = "View executive dashboards, metrics, and compliance reports", IsActive = true, CreatedAt = createdAt });
        modelBuilder.Entity<AppRolePermission>().HasData(
            new AppRolePermission { RoleId = 1, PermissionId = 1, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 3, PermissionId = 1, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 5, PermissionId = 1, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 1, PermissionId = 2, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 2, PermissionId = 2, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 3, PermissionId = 2, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 5, PermissionId = 2, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 2, PermissionId = 3, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 5, PermissionId = 3, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 1, PermissionId = 4, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 2, PermissionId = 4, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 5, PermissionId = 4, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 2, PermissionId = 5, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 5, PermissionId = 5, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 2, PermissionId = 6, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 5, PermissionId = 6, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 2, PermissionId = 7, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 5, PermissionId = 7, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 2, PermissionId = 8, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 5, PermissionId = 8, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 3, PermissionId = 9, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 5, PermissionId = 9, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 3, PermissionId = 11, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 1, PermissionId = 12, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 5, PermissionId = 12, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 2, PermissionId = 13, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 2, PermissionId = 14, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 5, PermissionId = 14, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 5, PermissionId = 15, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 2, PermissionId = 16, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 5, PermissionId = 16, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 6, PermissionId = 16, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 2, PermissionId = 17, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 5, PermissionId = 17, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 6, PermissionId = 17, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 5, PermissionId = 18, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 4, PermissionId = 19, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 5, PermissionId = 19, GrantedAt = createdAt, GrantedBy = "system:seed" },
            new AppRolePermission { RoleId = 7, PermissionId = 10, GrantedAt = createdAt, GrantedBy = "system:seed" });
    }
}
