using System.Security.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Formatting.Json;

namespace DaHub.API;

public static class ApiFoundation
{
    public static void AddApiFoundation(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IProblemDetailsWriter, FoundationProblemWriter>();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<SafeExceptionHandler>();
        builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
        builder.Services.AddHttpsRedirection(options =>
        {
            options.RedirectStatusCode = StatusCodes.Status301MovedPermanently;
            options.HttpsPort = builder.Configuration.GetValue("Https:Port", 443);
        });
        builder.Services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
            options.ExcludedHosts.Clear();
        });
        builder.WebHost.ConfigureKestrel(options => options.ConfigureHttpsDefaults(https =>
            https.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13));

        var directory = builder.Configuration["Logging:Directory"] ?? Path.Combine(Path.GetTempPath(), "dahub-logs");
        // Fail startup if the configured log destination cannot be written; never silently discard file logs.
        Directory.CreateDirectory(directory);
        var probe = Path.Combine(directory, ".write-probe-" + Guid.NewGuid().ToString("N"));
        using (File.Open(probe, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { }
        File.Delete(probe);
        builder.Services.AddSerilog((services, configuration) =>
        {
            // Allow only explicitly safe operational events. Framework/SQL/OIDC exceptions may include secrets.
            configuration.Enrich.FromLogContext()
                .Filter.ByIncludingOnly(entry => entry.Properties.ContainsKey("DaHubSafeEvent"))
                .WriteTo.Console(new JsonFormatter())
                .WriteTo.File(new JsonFormatter(), Path.Combine(directory, "api-.jsonl"),
                    rollingInterval: RollingInterval.Day, fileSizeLimitBytes: 10 * 1024 * 1024,
                    rollOnFileSizeLimit: true, retainedFileCountLimit: 14, shared: true);
            foreach (var sink in services.GetServices<ILogEventSink>())
                configuration.WriteTo.Sink(sink);
        });
    }

    public static void UseApiFoundation(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var supplied = context.Request.Headers["X-Correlation-ID"].ToString();
            context.TraceIdentifier = supplied.Length is > 0 and <= 64
                && supplied.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
                ? supplied : Guid.NewGuid().ToString("N");
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
                if (context.Request.IsHttps)
                    context.Response.Headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
                if (context.Request.Path.StartsWithSegments("/api/auth"))
                    context.Response.Headers.CacheControl = "no-store, no-cache";
                return Task.CompletedTask;
            });
            using var correlation = LogContext.PushProperty("CorrelationId", context.TraceIdentifier);
            try { await next(context); }
            finally
            {
                // Route templates avoid employee identifiers, unknown paths and callback/query values.
                var endpoint = context.GetEndpoint() ?? context.Features.Get<IExceptionHandlerFeature>()?.Endpoint;
                var path = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)";
                if (path == "{**path}") path = "(unmatched)";
                context.RequestServices.GetRequiredService<Serilog.ILogger>()
                    .ForContext("DaHubSafeEvent", true)
                    .ForContext("ActorUserId", context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous")
                    .ForContext("ActorUPN", context.User.FindFirstValue(ClaimTypes.Upn) ?? "anonymous")
                    .Information("HTTP {Method} {RequestPath} returned {StatusCode}", context.Request.Method, path, context.Response.StatusCode);
            }
        });
        app.UseHsts();
        if (!(app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Https:AllowDevelopmentHttp")))
            app.UseHttpsRedirection();
        app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => true });
        app.UseStatusCodePages(async status =>
        {
            await Results.Problem(statusCode: status.HttpContext.Response.StatusCode,
                detail: "The request could not be completed.").ExecuteAsync(status.HttpContext);
        });
    }
}

// Error contracts remain JSON even when a browser sends Accept: text/html.
public sealed class FoundationProblemWriter : IProblemDetailsWriter
{
    public bool CanWrite(ProblemDetailsContext context) => true;

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        problem.Status ??= context.HttpContext.Response.StatusCode;
        problem.Type ??= "about:blank";
        problem.Title ??= Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(problem.Status.Value);
        problem.Detail ??= "The request could not be completed.";
        problem.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
        context.HttpContext.Response.StatusCode = problem.Status.Value;
        return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(problem, options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json", cancellationToken: context.HttpContext.RequestAborted));
    }
}

public sealed class SafeExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            BadHttpRequestException bad => bad.StatusCode,
            FluentValidation.ValidationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };
        context.Response.StatusCode = status;
        await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status),
                Detail = "The request could not be completed."
            }
        });
        return true;
    }
}
