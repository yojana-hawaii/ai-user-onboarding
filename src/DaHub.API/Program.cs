using DaHub.API;
using DaHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
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

app.MapHealthChecks("/api/health/live", new HealthCheckOptions { Predicate = _ => false });
// SQL readiness only; directory/identity-provider readiness comes with those integrations.
app.MapHealthChecks("/api/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

// Allow the integration test host to start this entry point.
public partial class Program { }
