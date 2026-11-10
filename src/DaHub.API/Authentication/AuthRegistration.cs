using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace DaHub.API.Authentication;

public static class AuthRegistration
{
    public const string CookieScheme = "DaHubSession";
    public const string OktaScheme = "Okta";
    public const string CallbackPath = "/api/auth/callback";
    public const string LogoutCallbackPath = "/api/auth/signed-out";

    public static void AddDaHubAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("Okta").Get<OktaSettings>() ?? new OktaSettings();
        services.AddSingleton(settings);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ServerTicketStore>();
        services.AddScoped<LocalIdentity>();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = "__Host-DaHub.Antiforgery";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
        services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser().Build());

        var authentication = services.AddAuthentication(CookieScheme).AddCookie(CookieScheme, options =>
        {
            options.Cookie.Name = "__Host-DaHub.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
            options.ExpireTimeSpan = ServerTicketStore.IdleTimeout;
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
            options.Events.OnValidatePrincipal = async context =>
            {
                var identities = context.HttpContext.RequestServices.GetRequiredService<LocalIdentity>();
                if (context.Principal is null || !await identities.IsCurrentAsync(context.Principal, context.HttpContext.RequestAborted))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieScheme);
                }
            };
        });
        services.AddOptions<CookieAuthenticationOptions>(CookieScheme)
            .Configure<ServerTicketStore>((options, store) => options.SessionStore = store);

        if (!settings.Enabled)
            return;
        settings.Validate();
        var secret = File.ReadAllText(settings.ClientSecretFile).Trim();
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("The Okta client secret file is empty.");

        authentication.AddOpenIdConnect(OktaScheme, options =>
        {
            options.SignInScheme = CookieScheme;
            options.Authority = settings.Issuer;
            options.ClientId = settings.ClientId;
            options.ClientSecret = secret;
            options.RequireHttpsMetadata = true;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;
            options.MapInboundClaims = false;
            options.SaveTokens = false;
            options.GetClaimsFromUserInfoEndpoint = false;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.CallbackPath = CallbackPath;
            options.SignedOutCallbackPath = LogoutCallbackPath;
            options.RemoteSignOutPath = "/api/auth/remote-signout-disabled";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = settings.Issuer,
                ValidateAudience = true,
                ValidAudience = settings.ClientId,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
            options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                context.ProtocolMessage.RedirectUri = settings.PublicOrigin + CallbackPath;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToIdentityProviderForSignOut = context =>
            {
                context.ProtocolMessage.PostLogoutRedirectUri = settings.PublicOrigin + LogoutCallbackPath;
                context.ProtocolMessage.IdTokenHint = context.Properties.GetParameter<string>("id_token_hint");
                return Task.CompletedTask;
            };
            options.Events.OnSignedOutCallbackRedirect = context =>
            {
                if (context.Properties?.RedirectUri != settings.PublicOrigin + "/")
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.HandleResponse();
                }
                return Task.CompletedTask;
            };
            options.Events.OnRemoteSignOut = context =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.HandleResponse();
                return Task.CompletedTask;
            };
            options.Events.OnTokenValidated = context =>
            {
                if (context.SecurityToken.Issuer != settings.Issuer)
                {
                    context.Fail("Invalid issuer.");
                    return Task.CompletedTask;
                }
                // Retained server-side only; local login/audit occurs after nonce validation completes.
                context.Properties!.StoreTokens([new AuthenticationToken
                {
                    Name = "id_token", Value = context.TokenEndpointResponse?.IdToken ?? context.ProtocolMessage.IdToken
                }]);
                return Task.CompletedTask;
            };
            options.Events.OnTicketReceived = async context =>
            {
                var subject = context.Principal?.FindFirst("sub")?.Value;
                if (string.IsNullOrWhiteSpace(subject))
                {
                    context.Response.Redirect("/?authError=sign-in-failed");
                    context.HandleResponse();
                    return;
                }
                var identities = context.HttpContext.RequestServices.GetRequiredService<LocalIdentity>();
                var principal = await identities.SignInAsync(settings.Issuer, subject, context.HttpContext.RequestAborted);
                if (principal is null)
                {
                    context.Response.Redirect("/?authError=sign-in-failed");
                    context.HandleResponse();
                    return;
                }
                context.Principal = principal;
                context.Properties!.IsPersistent = false;
            };
            options.Events.OnRemoteFailure = context =>
            {
                context.HandleResponse();
                context.Response.Redirect("/?authError=sign-in-failed");
                return Task.CompletedTask;
            };
        });
    }
}
