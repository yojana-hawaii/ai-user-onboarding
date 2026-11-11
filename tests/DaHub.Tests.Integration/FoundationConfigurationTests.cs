using System.Net;
using System.Reflection;
using System.Text.Json;
using DaHub.API.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class FoundationConfigurationTests
{
    [Fact]
    public async Task DevelopmentStartsWithSyntheticUserSecretsAndExpectedKeys()
    {
        Assert.Equal("dahub-api-development", typeof(Program).Assembly.GetCustomAttribute<UserSecretsIdAttribute>()!.UserSecretsId);
        var id = "dahub-synthetic-test-" + Guid.NewGuid();
        var directory = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets", id)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".microsoft", "usersecrets", id);
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "secrets.json"), JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["Okta:Enabled"] = "true",
                ["Okta:Issuer"] = "https://synthetic.okta.com",
                ["Okta:ClientId"] = "synthetic-client",
                ["Okta:PublicOrigin"] = "https://localhost",
                ["Okta:ClientSecret"] = "synthetic-only-test-value"
            }));
            using var secrets = (ConfigurationRoot)new ConfigurationBuilder().AddUserSecrets(id).Build();
            Assert.Equal(5, secrets.AsEnumerable().Count(pair => pair.Value is not null));
            var start = new System.Diagnostics.ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "user-secrets", "list", "--id", id }) start.ArgumentList.Add(argument);
            using (var process = System.Diagnostics.Process.Start(start)!)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                Assert.Equal(0, process.ExitCode);
                Assert.Contains("Okta:ClientId = synthetic-client", output);
            }
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                foreach (var pair in secrets.AsEnumerable().Where(pair => pair.Value is not null))
                    builder.UseSetting(pair.Key, pair.Value);
            });
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            using var response = await client.GetAsync("/api/health/live");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(factory.Services.GetRequiredService<OktaSettings>().Enabled);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task ProductionStartsWithSyntheticEnvironmentConfiguration()
    {
        var prefix = "DAHUB_SYNTHETIC_" + Guid.NewGuid().ToString("N") + "_";
        var settings = new Dictionary<string, string>
        {
            ["Okta__Enabled"] = "true",
            ["Okta__Issuer"] = "https://synthetic.okta.com",
            ["Okta__ClientId"] = "synthetic-client",
            ["Okta__PublicOrigin"] = "https://localhost",
            ["Okta__ClientSecret"] = "synthetic-only-test-value"
        };
        try
        {
            foreach (var pair in settings) Environment.SetEnvironmentVariable(prefix + pair.Key, pair.Value);
            using var source = (ConfigurationRoot)new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build();
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                foreach (var pair in source.AsEnumerable().Where(pair => pair.Value is not null)) builder.UseSetting(pair.Key, pair.Value);
            });
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            using var response = await client.GetAsync("/api/health/live");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(factory.Services.GetRequiredService<OktaSettings>().Enabled);
        }
        finally { foreach (var key in settings.Keys) Environment.SetEnvironmentVariable(prefix + key, null); }
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("synthetic-file", "synthetic-value")]
    public void OktaRequiresExactlyOneSecretSource(string file, string value)
    {
        Assert.Throws<InvalidOperationException>(() => new OktaSettings
        {
            Issuer = "https://synthetic.okta.com",
            PublicOrigin = "https://localhost",
            ClientId = "synthetic-client",
            ClientSecretFile = file,
            ClientSecret = value
        }.Validate());
    }
}
