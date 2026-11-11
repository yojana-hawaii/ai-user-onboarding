using DaHub.API;
using DaHub.API.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using DaHub.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddApiFoundation();
builder.Services.AddOpenApi();
builder.Services.AddDaHubAuthentication(builder.Configuration);
builder.Services.AddDbContext<DaHubDbContext>(options =>
{
    var path = builder.Configuration["Database:ConnectionStringFile"];
    var connection = string.IsNullOrWhiteSpace(path)
        ? builder.Configuration.GetConnectionString("DaHub")
        : File.ReadAllText(path).Trim();
    options.UseSqlServer(DatabaseConnection.Validate(connection, builder.Environment.IsDevelopment()),
        sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", DaHubDbContext.Schema));
});
builder.AddDependencyHealth();

var app = builder.Build();

app.UseApiFoundation();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "DaHub API v1"));
}

app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api")
        && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
        && !HttpMethods.IsOptions(context.Request.Method))
    {
        try
        {
            await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
    }
    await next(context);
});
app.MapDaHubAuthentication();
app.MapFallback("{**path}", () => Results.NotFound()).AllowAnonymous();

app.MapGet("/api/health/live", (HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-store, no-cache";
    return Results.Text("Healthy");
})
    .AllowAnonymous()
    .WithName("Liveness")
    .WithSummary("Checks that the API process is running; does not check dependencies.")
    .Produces<string>(StatusCodes.Status200OK, "text/plain");
app.MapGet("/health", DependencyHealth.AggregateAsync).AllowAnonymous();
app.MapGet("/api/health", DependencyHealth.AggregateAsync).AllowAnonymous();
app.MapGet("/api/health/ready", async (HttpContext context, HealthCheckService checks, CancellationToken cancellationToken) =>
{
    context.Response.Headers.CacheControl = "no-store, no-cache";
    var report = await checks.CheckHealthAsync(check => check.Tags.Contains("ready"), cancellationToken);
    return report.Status == HealthStatus.Healthy ? Results.Text(report.Status.ToString())
        : Results.Problem(statusCode: 503, title: "Service Unavailable", detail: "Database is unavailable or migrations are pending.");
})
    .AllowAnonymous()
    .WithName("Readiness")
    .WithSummary("Checks SQL connectivity and pending migrations; does not check directory or identity providers.")
    .Produces<string>(StatusCodes.Status200OK, "text/plain")
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.Run();

// Allow the integration test host to start this entry point.
public partial class Program { }
