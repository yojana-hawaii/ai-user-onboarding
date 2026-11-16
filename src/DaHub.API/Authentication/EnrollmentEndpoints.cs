using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DaHub.API.Authentication;

public static class EnrollmentEndpoints
{
    public static void MapEnrollment(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/identities").RequireAuthorization("Permission:users.manage");
        group.MapPost("/", (EnrollIdentity input, HttpContext context, EnrollmentService service) =>
            HandleAsync(context, async () => Results.Ok(new { AppUserId = await service.EnrollAsync(input, context.User, context.RequestAborted) })));
        group.MapPost("/{identityId:int}/revoke", (int identityId, HttpContext context, EnrollmentService service) =>
            HandleAsync(context, async () => { await service.RevokeAsync(identityId, context.User, context.RequestAborted); return Results.NoContent(); }));
        group.MapPost("/users/{userId:int}/employee", (int userId, VerifyEmployeeLink input, HttpContext context, EnrollmentService service) =>
            HandleAsync(context, async () => { await service.LinkEmployeeAsync(userId, input, context.User, context.RequestAborted); return Results.NoContent(); }));
    }

    private static async Task<IResult> HandleAsync(HttpContext context, Func<Task<IResult>> operation)
    {
        context.Response.Headers.CacheControl = "no-store";
        try { return await operation(); }
        catch (ArgumentException) { return Results.Problem(statusCode: 400, title: "Invalid enrollment verification"); }
        catch (UnauthorizedAccessException) { return Results.Problem(statusCode: 403, title: "Forbidden"); }
        catch (EnrollmentConflictException) { return Results.Problem(statusCode: 409, title: "Enrollment conflict"); }
        catch (DbUpdateException) { return Results.Problem(statusCode: 409, title: "Enrollment conflict"); }
        catch (Exception exception) when (exception.GetBaseException() is SqlException { Number: 1205 })
        { return Results.Problem(statusCode: 409, title: "Concurrent enrollment; retry after reviewing current state"); }
    }
}
