using System.Text.Json;
using DaHub.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DaHub.Tests.Integration;

internal static class FirstReleaseSchemaAssertions
{
    private sealed record AccessSeeds(string[] Roles, string[] Permissions, string[] Grants);
    private sealed record Column(string Name, string Type, bool Nullable, bool Identity, string? Collation);
    private sealed record SchemaIndex(bool Unique, string Name, string[] Columns, string? Filter);
    private sealed record Check(string Name, string Sql);
    private sealed record ForeignKey(string[] Columns, string Principal, string[] PrincipalColumns);
    private sealed record SchemaTable(string Table, Column[] Columns, string[] Keys, SchemaIndex[] Indexes, Check[] Checks, ForeignKey[] ForeignKeys);

    public static async Task VerifyAsync(SqlConnection connection)
    {
        await VerifyMetadataAsync(connection);
        await VerifySeedsAsync(connection);
        await VerifyConstraintsAsync(connection);
    }

    private static async Task VerifyMetadataAsync(SqlConnection connection)
    {
        var contractPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "schema-contract.json");
        var expected = JsonSerializer.Deserialize<SchemaTable[]>(await File.ReadAllTextAsync(contractPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var actual = new Dictionary<string, Column>();
        await using (var command = new SqlCommand("""
            SELECT t.name, c.name, ty.name, c.max_length, c.is_nullable, c.is_identity, c.collation_name
            FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id
            JOIN sys.types ty ON ty.user_type_id=c.user_type_id
            WHERE t.schema_id=SCHEMA_ID('dahub_ita') AND t.name<>'__EFMigrationsHistory'
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var sqlType = reader.GetString(2);
                if (sqlType == "nvarchar")
                {
                    sqlType += reader.GetInt16(3) == -1 ? "(max)" : $"({reader.GetInt16(3) / 2})";
                }
                actual.Add(reader.GetString(0) + "." + reader.GetString(1),
                    new Column(reader.GetString(1), sqlType, reader.GetBoolean(4), reader.GetBoolean(5),
                        reader.IsDBNull(6) ? null : reader.GetString(6)));
            }
        }
        Assert.Equal(31, expected.Length);
        Assert.Equal(300, actual.Count);
        foreach (var table in expected)
        {
            foreach (var column in table.Columns)
            {
                Assert.True(actual.TryGetValue(table.Table + "." + column.Name, out var found), $"{table.Table}.{column.Name} missing");
                Assert.Equal(column.Type, found!.Type);
                Assert.Equal(column.Nullable, found.Nullable);
                Assert.Equal(column.Identity, found.Identity);
                if (column.Collation is not null) Assert.Equal(column.Collation, found.Collation);
            }
            var primaryColumns = await StringsAsync(connection, $"""
                SELECT c.name FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
                JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                WHERE i.object_id=OBJECT_ID(N'dahub_ita.{table.Table}') AND i.is_primary_key=1
                ORDER BY ic.key_ordinal
                """);
            Assert.Equal(table.Keys, primaryColumns);
            foreach (var index in table.Indexes)
            {
                var indexColumns = await StringsAsync(connection, $"""
                    SELECT c.name FROM sys.indexes i
                    JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
                    JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                    WHERE i.object_id=OBJECT_ID(N'dahub_ita.{table.Table}') AND i.name=N'{index.Name}'
                    AND ic.is_included_column=0 ORDER BY ic.key_ordinal
                    """);
                Assert.Equal(index.Columns, indexColumns);
                Assert.Equal(index.Unique ? 1L : 0L, await ScalarAsync(connection,
                    $"SELECT CONVERT(int,is_unique) FROM sys.indexes WHERE object_id=OBJECT_ID(N'dahub_ita.{table.Table}') AND name=N'{index.Name}'"));
                if (index.Filter is not null)
                {
                    var filter = Assert.Single(await StringsAsync(connection,
                        $"SELECT filter_definition FROM sys.indexes WHERE object_id=OBJECT_ID(N'dahub_ita.{table.Table}') AND name=N'{index.Name}'"));
                    Assert.Equal(Normalize(index.Filter), Normalize(filter));
                }
            }
            foreach (var check in table.Checks)
            {
                var definition = Assert.Single(await StringsAsync(connection,
                    $"SELECT definition FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dahub_ita.{table.Table}') AND name=N'{check.Name}' AND is_disabled=0 AND is_not_trusted=0"));
                Assert.NotEmpty(definition);
            }
            foreach (var fk in table.ForeignKeys)
            {
                for (var i = 0; i < fk.Columns.Length; i++)
                {
                    Assert.True(await ScalarAsync(connection, $"""
                        SELECT COUNT(*) FROM sys.foreign_key_columns f
                        JOIN sys.foreign_keys k ON k.object_id=f.constraint_object_id
                        WHERE f.parent_object_id=OBJECT_ID(N'dahub_ita.{table.Table}')
                        AND COL_NAME(f.parent_object_id,f.parent_column_id)=N'{fk.Columns[i]}'
                        AND f.referenced_object_id=OBJECT_ID(N'dahub_ita.{fk.Principal}')
                        AND COL_NAME(f.referenced_object_id,f.referenced_column_id)=N'{fk.PrincipalColumns[i]}'
                        AND k.is_disabled=0 AND k.is_not_trusted=0 AND k.delete_referential_action=0
                        """) > 0, $"Missing non-cascading FK {table.Table}.{fk.Columns[i]}");
                }
            }
        }
        Assert.Equal(0, await ScalarAsync(connection, "SELECT COUNT(*) FROM sys.tables WHERE name=N'Assets'"));
    }

    private static async Task VerifySeedsAsync(SqlConnection connection)
    {
        var seeds = JsonSerializer.Deserialize<AccessSeeds>(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "access-seeds.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(seeds.Roles.Order(StringComparer.Ordinal), (await StringsAsync(connection, "SELECT Name FROM dahub_ita.AppRoles")).Order(StringComparer.Ordinal));
        Assert.Equal(seeds.Permissions.Order(StringComparer.Ordinal), (await StringsAsync(connection, "SELECT Name FROM dahub_ita.AppPermissions")).Order(StringComparer.Ordinal));
        var actualGrants = await StringsAsync(connection, "SELECT r.Name+N':'+p.Name FROM dahub_ita.AppRolePermissions rp JOIN dahub_ita.AppRoles r ON r.Id=rp.RoleId JOIN dahub_ita.AppPermissions p ON p.Id=rp.PermissionId");
        Assert.Equal(seeds.Grants.Order(StringComparer.Ordinal), actualGrants.Order(StringComparer.Ordinal));
        Assert.Equal(7, await ScalarAsync(connection, "SELECT COUNT(*) FROM dahub_ita.AppRoles"));
        Assert.Equal(19, await ScalarAsync(connection, "SELECT COUNT(*) FROM dahub_ita.AppPermissions"));
        Assert.Equal(39, await ScalarAsync(connection, "SELECT COUNT(*) FROM dahub_ita.AppRolePermissions"));
        Assert.Equal(0, await ScalarAsync(connection, "SELECT COUNT(*) FROM dahub_ita.AppUsers"));
        Assert.Equal(0, await ScalarAsync(connection, "SELECT COUNT(*) FROM dahub_ita.AppUserRoles"));
        Assert.Equal(0, await ScalarAsync(connection, "SELECT COUNT(*) FROM dahub_ita.AppPermissions WHERE Name=N'approvals.bypass'"));
        var urgentGrants = await StringsAsync(connection, """
            SELECT r.Name FROM dahub_ita.AppRolePermissions rp
            JOIN dahub_ita.AppRoles r ON r.Id=rp.RoleId JOIN dahub_ita.AppPermissions p ON p.Id=rp.PermissionId
            WHERE p.Name=N'provisioning.urgent'
            """);
        Assert.Equal(new[] { "UrgentOperator" }, urgentGrants);
        var supplemental = await StringsAsync(connection, """
            SELECT p.Name FROM dahub_ita.AppRolePermissions rp
            JOIN dahub_ita.AppRoles r ON r.Id=rp.RoleId JOIN dahub_ita.AppPermissions p ON p.Id=rp.PermissionId
            WHERE r.Name=N'UrgentOperator'
            """);
        Assert.Equal(new[] { "provisioning.urgent" }, supplemental);
        Assert.Equal(new[] { "710NK", "952NK", "ADMIN", "DT", "KAAAHI", "KOHOU", "MAIN", "VIRTUAL" },
            await StringsAsync(connection, "SELECT SiteCode FROM dahub_ita.Locations WHERE SiteCode<>N'TEST' ORDER BY SiteCode"));
    }

    private static async Task VerifyConstraintsAsync(SqlConnection connection)
    {
        await ExecuteAsync(connection, """
            INSERT dahub_ita.Employees (LegalFirstName,LegalLastName,Username,Email,EmploymentType)
            VALUES (N'Synthetic',N'Worker',N'schema-worker',N'schema-worker@example.test',N'Student'),
                   (N'Synthetic',N'Other',N'schema-other',N'schema-other@example.test',N'Temporary');
            INSERT dahub_ita.AppUsers (DisplayName,UPN) VALUES (N'Synthetic approver',N'same@example.test'),(N'Synthetic other',N'same@example.test');
            INSERT dahub_ita.AccessTemplates (Name,M365LicenseSku,CreatedByUPN)
            VALUES (N'Synthetic template',N'synthetic-sku',N'actor@example.test');
            """);
        Assert.Equal(2, await ScalarAsync(connection, "SELECT COUNT(*) FROM dahub_ita.Employees WHERE StartDate IS NULL AND VerificationStatus=N'PendingHR'"));
        await ViolationAsync(connection, "UPDATE dahub_ita.Employees SET EmploymentType=N'Unknown'", 547);
        await ViolationAsync(connection, "UPDATE dahub_ita.Employees SET Status=N'Unknown'", 547);
        await ViolationAsync(connection, "UPDATE dahub_ita.Employees SET VerificationStatus=N'Unknown'", 547);
        await ViolationAsync(connection, "UPDATE dahub_ita.Employees SET ManagerId=2147483647", 547);
        await ViolationAsync(connection, "UPDATE dahub_ita.AppUsers SET AuthorizationVersion=0", 547);
        await ViolationAsync(connection, """
            INSERT dahub_ita.AccessTemplateGroups (TemplateId,GroupType,GroupName,GroupIdentifier)
            SELECT Id,N'Unknown',N'Synthetic',N'synthetic' FROM dahub_ita.AccessTemplates
            """, 547);
        await ExecuteAsync(connection, """
            INSERT dahub_ita.AppExternalIdentities (AppUserId,ProviderKey,Issuer,Subject,LinkMethod)
            SELECT MIN(Id),N'okta',N'https://issuer.example.test',N'CaseSubject',N'Bootstrap' FROM dahub_ita.AppUsers;
            INSERT dahub_ita.AppExternalIdentities (AppUserId,ProviderKey,Issuer,Subject,LinkMethod)
            SELECT MAX(Id),N'future-provider',N'https://issuer.example.test',N'casesubject',N'Bootstrap' FROM dahub_ita.AppUsers;
            """);
        await ViolationAsync(connection, """
            INSERT dahub_ita.AppExternalIdentities (AppUserId,ProviderKey,Issuer,Subject,LinkMethod)
            SELECT MIN(Id),N'other-provider',N'https://issuer.example.test',N'CaseSubject',N'Bootstrap' FROM dahub_ita.AppUsers
            """, 2601, 2627);
        await ViolationAsync(connection, """
            INSERT dahub_ita.AppExternalIdentities (AppUserId,ProviderKey,Issuer,Subject,LinkMethod)
            SELECT MIN(Id),N'okta',N'https://issuer.example.test',N'Unlinked',N'Admin' FROM dahub_ita.AppUsers
            """, 547);
        await ExecuteAsync(connection, """
            INSERT dahub_ita.EmployeeLocations (EmployeeId,LocationId,IsPrimary)
            SELECT e.Id,l.Id,1 FROM dahub_ita.Employees e CROSS JOIN dahub_ita.Locations l WHERE e.Username=N'schema-worker' AND l.SiteCode=N'MAIN';
            """);
        await ViolationAsync(connection, """
            INSERT dahub_ita.EmployeeLocations (EmployeeId,LocationId,IsPrimary)
            SELECT e.Id,l.Id,1 FROM dahub_ita.Employees e CROSS JOIN dahub_ita.Locations l WHERE e.Username=N'schema-worker' AND l.SiteCode=N'DT'
            """, 2601, 2627);
        await ExecuteAsync(connection, """
            INSERT dahub_ita.EmployeeLocations (EmployeeId,LocationId,IsPrimary)
            SELECT e.Id,l.Id,0 FROM dahub_ita.Employees e CROSS JOIN dahub_ita.Locations l WHERE e.Username=N'schema-worker' AND l.SiteCode=N'DT';
            INSERT dahub_ita.EmployeeAccounts (EmployeeId,ADObjectGuid,EntraObjectId,Username,Email)
            SELECT Id,'00000000-0000-0000-0000-000000000001',N'00000000-0000-0000-0000-000000000002',Username,Email FROM dahub_ita.Employees WHERE Username=N'schema-worker';
            INSERT dahub_ita.EmployeeAccounts (EmployeeId,Username,Email)
            SELECT Id,Username,Email FROM dahub_ita.Employees;
            INSERT dahub_ita.EmploymentPeriods (EmployeeId,AccountId,EmploymentType)
            SELECT EmployeeId,Id,N'Student' FROM dahub_ita.EmployeeAccounts WHERE ADObjectGuid IS NOT NULL;
            """);
        await ViolationAsync(connection, """
            INSERT dahub_ita.EmployeeAccounts (EmployeeId,ADObjectGuid,Username,Email)
            SELECT Id,'00000000-0000-0000-0000-000000000001',Username,Email FROM dahub_ita.Employees WHERE Username=N'schema-other'
            """, 2601, 2627);
        await ViolationAsync(connection, """
            INSERT dahub_ita.EmployeeAccounts (EmployeeId,EntraObjectId,Username,Email)
            SELECT Id,N'00000000-0000-0000-0000-000000000002',Username,Email FROM dahub_ita.Employees WHERE Username=N'schema-other'
            """, 2601, 2627);
        await ViolationAsync(connection, """
            INSERT dahub_ita.EmploymentPeriods (EmployeeId,AccountId,EmploymentType)
            SELECT e.Id,a.Id,N'Temporary' FROM dahub_ita.Employees e CROSS JOIN dahub_ita.EmployeeAccounts a WHERE e.Username=N'schema-other' AND a.ADObjectGuid IS NOT NULL
            """, 547);
        await ExecuteAsync(connection, """
            INSERT dahub_ita.ApprovalDelegations (ManagerEmployeeId,DelegateUserId,AssignedByUPN,StartsAt,EndsAt)
            SELECT MIN(e.Id),MIN(u.Id),N'actor@example.test','2026-11-01','2026-12-01' FROM dahub_ita.Employees e CROSS JOIN dahub_ita.AppUsers u;
            INSERT dahub_ita.Requests (RequestType,EmployeeId,SubmittedByUPN,ExecutionStatus,ApprovalStatus)
            SELECT N'Onboarding',MIN(Id),N'actor@example.test',N'Completed',N'Pending' FROM dahub_ita.Employees;
            INSERT dahub_ita.RequestRevisions (RequestId,Revision,SnapshotJson,CreatedByUPN,ChangeReason)
            SELECT Id,1,N'{}',N'actor@example.test',N'Synthetic initial request' FROM dahub_ita.Requests;
            """);
        await ViolationAsync(connection, "UPDATE dahub_ita.ApprovalDelegations SET EndsAt=StartsAt", 547);
        await ViolationAsync(connection, "UPDATE dahub_ita.Requests SET ApprovalStatus=N'Unknown'", 547);
        await ViolationAsync(connection, "UPDATE dahub_ita.Requests SET MailboxRetentionDays=-1", 547);
        await ViolationAsync(connection, """
            INSERT dahub_ita.RequestRevisions (RequestId,Revision,SnapshotJson,CreatedByUPN,ChangeReason)
            SELECT Id,2,N'not-json',N'actor@example.test',N'Synthetic change' FROM dahub_ita.Requests
            """, 547);
        await ViolationAsync(connection, """
            INSERT dahub_ita.RequestRevisions (RequestId,Revision,SnapshotJson,CreatedByUPN,ChangeReason)
            SELECT Id,0,N'{}',N'actor@example.test',N'Synthetic change' FROM dahub_ita.Requests
            """, 547);
        await ViolationAsync(connection, """
            INSERT dahub_ita.UrgentExecutions (RequestId,Revision,ActorUPN,Justification,ActionsJson)
            SELECT Id,2,N'actor@example.test',N'Synthetic reason',N'{}' FROM dahub_ita.Requests
            """, 547);
        await ViolationAsync(connection, """
            INSERT dahub_ita.UrgentExecutions (RequestId,Revision,ActorUPN,Justification,ActionsJson)
            SELECT Id,1,N'actor@example.test',N'   ',N'{}' FROM dahub_ita.Requests
            """, 547);
        await ViolationAsync(connection, """
            INSERT dahub_ita.RequestApprovals (RequestId,Revision,ApproverUserId,ApproverUPN,ApproverRole)
            SELECT r.Id,2,MIN(u.Id),N'approver@example.test',N'Manager' FROM dahub_ita.Requests r CROSS JOIN dahub_ita.AppUsers u GROUP BY r.Id
            """, 547);
        await ExecuteAsync(connection, """
            INSERT dahub_ita.BackgroundJobs (JobType) VALUES
            (N'ScheduledTermination'),(N'WorkerExpiry'),(N'WorkerExpiryNotice');
            """);
        await ViolationAsync(connection, "INSERT dahub_ita.BackgroundJobs (JobType) VALUES (N'Unknown')", 547);
        await ExecuteAsync(connection, """
            INSERT dahub_ita.Departments (Name,OUPath) VALUES (N'Schema department A',N'OU=SyntheticA,DC=example,DC=test'),(N'Schema department B',N'OU=SyntheticB,DC=example,DC=test');
            INSERT dahub_ita.JobTitles (Title) VALUES (N'Schema title A'),(N'Schema title B');
            INSERT dahub_ita.Specialties (Name) VALUES (N'Schema specialty A'),(N'Schema specialty B');
            """);
        foreach (var assignment in new[]
        {
            (Table: "EmployeeDepartments", Lookup: "Departments", Key: "DepartmentId", ExtraColumn: "", ExtraValue: ""),
            (Table: "EmployeeJobTitles", Lookup: "JobTitles", Key: "JobTitleId", ExtraColumn: ",AssignedBy", ExtraValue: ",N'actor@example.test'"),
            (Table: "EmployeeSpecialties", Lookup: "Specialties", Key: "SpecialtyId", ExtraColumn: ",AssignedBy", ExtraValue: ",N'actor@example.test'")
        })
        {
            var first = $"INSERT dahub_ita.{assignment.Table} (EmployeeId,{assignment.Key},IsPrimary{assignment.ExtraColumn}) SELECT e.Id,l.Id,1{assignment.ExtraValue} FROM dahub_ita.Employees e CROSS JOIN dahub_ita.{assignment.Lookup} l WHERE e.Username=N'schema-worker' AND l.Id=(SELECT MIN(Id) FROM dahub_ita.{assignment.Lookup})";
            var second = $"INSERT dahub_ita.{assignment.Table} (EmployeeId,{assignment.Key},IsPrimary{assignment.ExtraColumn}) SELECT e.Id,l.Id,1{assignment.ExtraValue} FROM dahub_ita.Employees e CROSS JOIN dahub_ita.{assignment.Lookup} l WHERE e.Username=N'schema-worker' AND l.Id=(SELECT MAX(Id) FROM dahub_ita.{assignment.Lookup})";
            await ExecuteAsync(connection, first);
            await ViolationAsync(connection, second, 2601, 2627);
            await ExecuteAsync(connection, second.Replace(",1", ",0", StringComparison.Ordinal));
        }
        await ExecuteAsync(connection, "UPDATE dahub_ita.AppUsers SET EmployeeId=(SELECT MIN(Id) FROM dahub_ita.Employees) WHERE Id=(SELECT MIN(Id) FROM dahub_ita.AppUsers)");
        await ViolationAsync(connection, "UPDATE dahub_ita.AppUsers SET EmployeeId=(SELECT MIN(Id) FROM dahub_ita.Employees)", 2601, 2627);
        await ViolationAsync(connection, "INSERT dahub_ita.AppRolePermissions (RoleId,PermissionId,GrantedBy) SELECT TOP(1) RoleId,PermissionId,N'actor@example.test' FROM dahub_ita.AppRolePermissions", 2601, 2627);
        await ViolationAsync(connection, """
            INSERT dahub_ita.UrgentExecutions (RequestId,Revision,ActorUPN,Justification,ActionsJson)
            SELECT Id,1,N'actor@example.test',N'Synthetic reason',N'not-json' FROM dahub_ita.Requests
            """, 547);
        Assert.Equal(1, await ScalarAsync(connection, "SELECT COUNT(*) FROM dahub_ita.Requests WHERE ExecutionStatus=N'Completed' AND ApprovalStatus=N'Pending'"));
        await ViolationAsync(connection, "DELETE dahub_ita.Employees", 547);
    }

    public static async Task VerifyRuntimePermissionsAsync(DaHubDbContext context)
    {
        await context.Database.OpenConnectionAsync();
        var connection = (SqlConnection)context.Database.GetDbConnection();
        await ExecuteAsync(connection, """
            INSERT dahub_ita.AuditLog (ActorUPN,ActionType,Result)
            VALUES (N'system:test',N'SyntheticEvent',N'Info');
            INSERT dahub_ita.RequestRevisions (RequestId,Revision,SnapshotJson,CreatedByUPN,ChangeReason)
            SELECT Id,2,N'{}',N'actor@example.test',N'Synthetic second revision' FROM dahub_ita.Requests;
            INSERT dahub_ita.UrgentExecutions (RequestId,Revision,ActorUPN,Justification,ActionsJson)
            SELECT Id,2,N'actor@example.test',N'Synthetic authorized urgency',N'{}' FROM dahub_ita.Requests;
            """);
        foreach (var table in new[] { "AuditLog", "RequestRevisions", "UrgentExecutions" })
        {
            var commands = table switch
            {
                "AuditLog" => (Read: "SELECT TOP(1) * FROM dahub_ita.AuditLog", Update: "UPDATE dahub_ita.AuditLog SET Detail=Detail", Delete: "DELETE dahub_ita.AuditLog"),
                "RequestRevisions" => (Read: "SELECT TOP(1) * FROM dahub_ita.RequestRevisions", Update: "UPDATE dahub_ita.RequestRevisions SET ChangeReason=ChangeReason", Delete: "DELETE dahub_ita.RequestRevisions"),
                _ => (Read: "SELECT TOP(1) * FROM dahub_ita.UrgentExecutions", Update: "UPDATE dahub_ita.UrgentExecutions SET Justification=Justification", Delete: "DELETE dahub_ita.UrgentExecutions")
            };
            await context.Database.ExecuteSqlRawAsync(commands.Read);
            var deniedUpdate = await Assert.ThrowsAsync<SqlException>(() =>
                context.Database.ExecuteSqlRawAsync(commands.Update));
            Assert.Equal(229, deniedUpdate.Number);
            var deniedDelete = await Assert.ThrowsAsync<SqlException>(() =>
                context.Database.ExecuteSqlRawAsync(commands.Delete));
            Assert.Equal(229, deniedDelete.Number);
        }
        await using (var permissionCommand = new SqlCommand("""
            SELECT t.name,
                HAS_PERMS_BY_NAME(N'dahub_ita.'+t.name,N'OBJECT',N'SELECT'),
                HAS_PERMS_BY_NAME(N'dahub_ita.'+t.name,N'OBJECT',N'INSERT'),
                HAS_PERMS_BY_NAME(N'dahub_ita.'+t.name,N'OBJECT',N'UPDATE'),
                HAS_PERMS_BY_NAME(N'dahub_ita.'+t.name,N'OBJECT',N'DELETE')
            FROM sys.tables t WHERE schema_id=SCHEMA_ID(N'dahub_ita') AND t.name<>N'__EFMigrationsHistory'
            """, connection))
        await using (var reader = await permissionCommand.ExecuteReaderAsync())
        {
            var tables = 0;
            while (await reader.ReadAsync())
            {
                tables++;
                var appendOnly = reader.GetString(0) is "AuditLog" or "RequestRevisions" or "UrgentExecutions";
                Assert.Equal(1, reader.GetInt32(1));
                Assert.Equal(1, reader.GetInt32(2));
                Assert.Equal(appendOnly ? 0 : 1, reader.GetInt32(3));
                Assert.Equal(appendOnly ? 0 : 1, reader.GetInt32(4));
            }
            Assert.Equal(31, tables);
        }
        var historyDenied = await Assert.ThrowsAsync<SqlException>(() =>
            context.Database.ExecuteSqlRawAsync("DELETE dahub_ita.__EFMigrationsHistory"));
        Assert.Equal(229, historyDenied.Number);
    }

    private static string Normalize(string sql) => new(sql.Where(c => !char.IsWhiteSpace(c) && !"[]()".Contains(c)).Select(char.ToUpperInvariant).ToArray());

    private static async Task ViolationAsync(SqlConnection connection, string sql, params int[] errors)
    {
        var exception = await Assert.ThrowsAsync<SqlException>(() => ExecuteAsync(connection, sql));
        Assert.Contains(exception.Number, errors);
    }

    private static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<string[]> StringsAsync(SqlConnection connection, string sql)
    {
        var result = new List<string>();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result.ToArray();
    }
}
