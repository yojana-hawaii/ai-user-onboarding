using DaHub.API;
using DaHub.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<DaHubDbContext>(options =>
{
    var path = builder.Configuration["Database:ConnectionStringFile"];
    var connection = string.IsNullOrWhiteSpace(path)
        ? builder.Configuration.GetConnectionString("DaHub")
        : File.ReadAllText(path).Trim();
    options.UseSqlServer(DatabaseConnection.Validate(connection, builder.Environment.IsDevelopment()),
        sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", DaHubDbContext.Schema));
});
builder.Services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("sql", tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "DaHub API v1"));
}

app.MapGet("/api/health/live", (HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-store, no-cache";
    return Results.Text("Healthy");
})
    .WithName("Liveness")
    .WithSummary("Checks that the API process is running; does not check dependencies.")
    .Produces<string>(StatusCodes.Status200OK, "text/plain");
// SQL readiness only; directory/identity-provider readiness comes with those integrations.
app.MapGet("/api/health/ready", async (HttpContext context, HealthCheckService checks, CancellationToken cancellationToken) =>
{
    context.Response.Headers.CacheControl = "no-store, no-cache";
    var report = await checks.CheckHealthAsync(check => check.Tags.Contains("ready"), cancellationToken);
    return Results.Text(report.Status.ToString(), statusCode: report.Status == HealthStatus.Unhealthy
        ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK);
})
    .WithName("Readiness")
    .WithSummary("Checks SQL connectivity and pending migrations; does not check directory or identity providers.")
    .Produces<string>(StatusCodes.Status200OK, "text/plain")
    .Produces<string>(StatusCodes.Status503ServiceUnavailable, "text/plain");

app.Run();

// Allow the integration test host to start this entry point.
public partial class Program { }
