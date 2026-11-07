var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();

// Process liveness only. Database and directory readiness are separate future checks.
app.MapHealthChecks("/api/health/live");

app.Run();

// Allow the integration test host to start this entry point.
public partial class Program { }
