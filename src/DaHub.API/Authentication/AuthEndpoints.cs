using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;

namespace DaHub.API.Authentication;

public static class AuthEndpoints
{
    public static void MapDaHubAuthentication(this WebApplication app)
    {
        app.MapGet("/api/auth/login", (string? returnUrl, OktaSettings settings) =>
        {
            var target = returnUrl ?? "/";
            if (!OktaSettings.IsLocalReturn(target))
                return Results.BadRequest();
            if (!settings.Enabled)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            return Results.Challenge(new AuthenticationProperties { RedirectUri = settings.PublicOrigin + target }, [AuthRegistration.OktaScheme]);
        }).AllowAnonymous();

        app.MapGet("/api/auth/session", (ClaimsPrincipal user) => Results.Ok(new
        {
            appUserId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture),
            displayName = user.Identity!.Name
        })).RequireAuthorization();

        app.MapGet("/api/auth/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
            Results.Ok(new { requestToken = antiforgery.GetAndStoreTokens(context).RequestToken })).RequireAuthorization();

        app.MapPost("/api/auth/logout", async (HttpContext context, OktaSettings settings) =>
        {
            // Capture the ID token before deleting the server-side ticket.
            var idToken = await context.GetTokenAsync(AuthRegistration.CookieScheme, "id_token");
            await context.SignOutAsync(AuthRegistration.CookieScheme);
            if (settings.Enabled)
            {
                var properties = new AuthenticationProperties { RedirectUri = settings.PublicOrigin + "/" };
                properties.Parameters["id_token_hint"] = idToken;
                await context.SignOutAsync(AuthRegistration.OktaScheme, properties);
            }
            else
                context.Response.StatusCode = StatusCodes.Status204NoContent;
        }).RequireAuthorization();
    }
}
