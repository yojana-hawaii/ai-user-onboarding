-- Deployment-only batch; never run from an HTTP endpoint or the runtime identity.
-- A deployment operator must bind these nvarchar parameters after independent verification:
-- @Issuer (400), @Subject (255), @DisplayName (200), @VerificationReference (200).
-- @Issuer must exactly equal the trusted Okta issuer configured for this deployment.
-- Keep the populated invocation private/ignored and remove it after successful setup.
-- No credentials, tokens, email matching or provider group claims belong in these inputs.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
IF @Issuer IS NULL OR @Subject IS NULL OR @DisplayName IS NULL OR @VerificationReference IS NULL
    OR LEN(@Subject) = 0 OR LEN(@DisplayName) = 0 OR LEN(@VerificationReference) = 0
    OR @Issuer NOT LIKE N'https://%' OR RIGHT(@Issuer, 1) = N'/'
    OR DATALENGTH(@Issuer) <> DATALENGTH(TRIM(@Issuer))
    OR DATALENGTH(@Subject) <> DATALENGTH(TRIM(@Subject))
    THROW 51000, 'Verified bootstrap inputs are required.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock @Resource = N'DaHub.FirstAdminBootstrap',
        @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 10000;
    IF @LockResult < 0 THROW 51000, 'Bootstrap lock unavailable.', 1;
    -- The immutable audit latch permanently retires bootstrap even if users are later deactivated.
    IF EXISTS (SELECT 1 FROM dahub_ita.AppUsers)
        OR EXISTS (SELECT 1 FROM dahub_ita.AppExternalIdentities)
        OR EXISTS (SELECT 1 FROM dahub_ita.AppUserRoles)
        OR EXISTS (SELECT 1 FROM dahub_ita.AuditLog WHERE ActionType = N'AdminBootstrapped')
        THROW 51000, 'Bootstrap is retired or the identity registry is not empty.', 1;
    DECLARE @RoleId int = (SELECT Id FROM dahub_ita.AppRoles WHERE Name = N'Admin' AND IsActive = 1);
    IF @RoleId IS NULL OR NOT EXISTS (
        SELECT 1 FROM dahub_ita.AppRolePermissions g
        JOIN dahub_ita.AppPermissions p ON p.Id = g.PermissionId
        WHERE g.RoleId = @RoleId AND p.Name = N'users.manage' AND p.IsActive = 1)
        THROW 51000, 'Active local Admin enrollment grant is required.', 1;
    INSERT dahub_ita.AppUsers (DisplayName) VALUES (@DisplayName);
    DECLARE @UserId int = CONVERT(int, SCOPE_IDENTITY());
    INSERT dahub_ita.AppExternalIdentities (AppUserId, ProviderKey, Issuer, Subject, LinkMethod)
        VALUES (@UserId, N'Okta', @Issuer, @Subject, N'Bootstrap');
    INSERT dahub_ita.AppUserRoles (UserId, RoleId, AssignedAt, AssignedBy)
        VALUES (@UserId, @RoleId, SYSUTCDATETIME(), N'system:bootstrap');
    DECLARE @Details nvarchar(max) = (SELECT @UserId AS AppUserId, @RoleId AS RoleId,
        @VerificationReference AS VerificationReference FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    -- The deployment operator is a system actor, never impersonated as the newly enrolled user.
    INSERT dahub_ita.AuditLog (ActorUPN, ActionType, AffectedSystem, Result, NewValueJson)
        VALUES (N'system:bootstrap', N'AdminBootstrapped', N'App', N'Success', @Details);
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;
