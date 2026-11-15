using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DaHub.API.OrganizationSeeds;

public static class SeedEndpoints
{
    public const int MaxBytes = 524288;

    public static void MapOrganizationSeeds(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/organization-seeds")
            .RequireAuthorization("Permission:templates.manage", "Permission:users.manage");
        group.MapPost("/preview", (HttpContext context, SeedImporter importer) => HandleAsync(context, importer, false));
        group.MapPost("/apply", (HttpContext context, SeedImporter importer) => HandleAsync(context, importer, true));
    }

    private static async Task<IResult> HandleAsync(HttpContext context, SeedImporter importer, bool apply)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!context.Request.HasJsonContentType())
            return Results.Problem(statusCode: 415, title: "Unsupported Media Type");
        if (context.Request.ContentLength > MaxBytes)
            return Results.Problem(statusCode: 413, title: "Content Too Large");
        var bytes = new byte[MaxBytes + 1];
        var length = 0;
        while (length < bytes.Length)
        {
            var read = await context.Request.Body.ReadAsync(bytes.AsMemory(length), context.RequestAborted);
            if (read == 0) break;
            length += read;
        }
        if (length > MaxBytes) return Results.Problem(statusCode: 413, title: "Content Too Large");
        try
        {
            var json = new UTF8Encoding(false, true).GetString(bytes, 0, length);
            var package = SeedPackage.Parse(json);
            var actor = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture);
            if (!apply) return Results.Ok(await importer.PreviewAsync(package, actor, context.RequestAborted));
            var token = context.Request.Headers["X-Seed-Preview"].ToString();
            if (token.Length is 0 or > 2048) throw new SeedConflictException();
            var authorization = context.RequestServices.GetRequiredService<IAuthorizationService>();
            async Task<bool> Authorized() =>
                (await authorization.AuthorizeAsync(context.User, "Permission:templates.manage")).Succeeded
                && (await authorization.AuthorizeAsync(context.User, "Permission:users.manage")).Succeeded;
            return Results.Ok(await importer.ApplyAsync(package, token, actor, Authorized, context.RequestAborted));
        }
        catch (SeedValidationException exception)
        {
            return Results.Problem(statusCode: 400, title: "Invalid seed package", detail: exception.Message);
        }
        catch (DecoderFallbackException)
        {
            return Results.Problem(statusCode: 400, title: "Invalid seed package", detail: "UTF-8 JSON is required.");
        }
        catch (SeedConflictException)
        {
            return Results.Problem(statusCode: 409, title: "Preview expired or changed", detail: "Preview this package again before applying it.");
        }
        catch (DbUpdateException)
        {
            return Results.Problem(statusCode: 409, title: "Catalog conflict", detail: "No changes were committed. Preview again or reconcile catalog keys.");
        }
        catch (Exception exception) when (exception.GetBaseException() is SqlException { Number: 1205 })
        {
            return Results.Problem(statusCode: 409, title: "Concurrent catalog change", detail: "No changes were committed. Preview the package again.");
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Problem(statusCode: 403, title: "Forbidden");
        }
    }
}
