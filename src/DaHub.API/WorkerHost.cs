using DaHub.Core.Jobs;
using DaHub.Infrastructure.Jobs;
using DaHub.Infrastructure.Persistence;
using DaHub.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;

namespace DaHub.API;

public static class WorkerHost
{
    public static IHost Build(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        // Only this category emits allowlisted worker events. Never enable EF/SQL/provider
        // logging in the worker: exception messages and payloads may contain credentials.
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole();
        builder.Logging.AddFilter((category, _) => category == typeof(JobWorker).FullName);
        var path = builder.Configuration["Database:ConnectionStringFile"];
        var connection = string.IsNullOrWhiteSpace(path)
            ? builder.Configuration.GetConnectionString("DaHub") : File.ReadAllText(path).Trim();
        var validated = DatabaseConnection.Validate(connection, builder.Environment.IsDevelopment());
        builder.Services.AddDbContext<DaHubDbContext>(options =>
        {
            options.UseSqlServer(validated,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", DaHubDbContext.Schema));
        });
        builder.Services.AddScoped<IJobStore, SqlJobStore>();
        builder.Services.AddNotificationDelivery(builder.Configuration);
        builder.Services.AddHostedService<JobWorker>();
        return builder.Build();
    }
}

