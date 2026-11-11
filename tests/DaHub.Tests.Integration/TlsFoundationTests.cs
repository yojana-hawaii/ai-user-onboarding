using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DaHub.API;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DaHub.Tests.Integration;

public sealed class TlsFoundationTests
{
    [Fact]
    public async Task ActualKestrelAcceptsModernTlsAndRejectsObsoleteProtocols()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        var directory = Path.Combine(Path.GetTempPath(), "dahub-tls-test-" + Guid.NewGuid());
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            { EnvironmentName = "Production", ContentRootPath = Path.GetTempPath() });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:Directory"] = directory });
            builder.AddApiFoundation();
            builder.Services.AddHttpClient();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listener => listener.UseHttps(certificate)));
            await using var app = builder.Build();
            app.UseApiFoundation();
            app.MapGet("/probe", () => "Synthetic");
            await app.StartAsync();
            var address = new Uri(Assert.Single(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses));
            var directoryProbe = new DirectoryConnectivity(new DirectoryHealthSettings { AdHost = "127.0.0.1", AdPort = address.Port },
                app.Services.GetRequiredService<IHttpClientFactory>());
            // Production LDAPS must reject the ephemeral untrusted certificate; no permissive callback is installed.
            using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                await Assert.ThrowsAsync<AuthenticationException>(() => directoryProbe.CheckAsync(true, deadline.Token));

#pragma warning disable SYSLIB0039 // Deliberate negative probes for obsolete TLS against an isolated synthetic host.
            foreach (var protocol in new[] { SslProtocols.Tls, SslProtocols.Tls11, SslProtocols.Tls12, SslProtocols.Tls13 })
#pragma warning restore SYSLIB0039
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, address.Port, timeout.Token);
                // Test-only trust bypass for this ephemeral certificate, never production directory probes.
                using var tls = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
                var error = await Record.ExceptionAsync(() => tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                { TargetHost = "localhost", EnabledSslProtocols = protocol }, timeout.Token));
                if (protocol is SslProtocols.Tls12 or SslProtocols.Tls13)
                {
                    Assert.Null(error);
                    Assert.Equal(protocol, tls.SslProtocol);
                }
                else
                {
                    Assert.NotNull(error);
                    Assert.True(error is AuthenticationException or IOException, error.ToString());
                }
            }
            await app.StopAsync();
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
