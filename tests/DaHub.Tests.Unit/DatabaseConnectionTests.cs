using DaHub.Infrastructure.Persistence;
using Xunit;

namespace DaHub.Tests.Unit;

public sealed class DatabaseConnectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Server=sql;Database=DaHub;User ID=app;Password=synthetic;Encrypt=false")]
    [InlineData("Server=sql;Database=DaHub;User ID=sa;Password=synthetic;Encrypt=true")]
    [InlineData("Server=sql;Database=DaHub;User ID= sa ;Password=synthetic;Encrypt=true")]
    public void MissingInsecureOrAdministratorConnectionsAreRejected(string? connection)
    {
        Assert.Throws<InvalidOperationException>(() => DatabaseConnection.Validate(connection, true));
    }

    [Fact]
    public void UntrustedCertificatesAreRestrictedToDevelopment()
    {
        const string connection = "Server=sql;Database=DaHub;User ID=app;Password=synthetic;Encrypt=true;TrustServerCertificate=true";
        Assert.Throws<InvalidOperationException>(() => DatabaseConnection.Validate(connection, false));
        Assert.NotEmpty(DatabaseConnection.Validate(connection, true));
    }

    [Fact]
    public void VerifiedIntegratedAuthenticationIsSupported()
    {
        Assert.NotEmpty(DatabaseConnection.Validate(
            "Server=sql;Database=DaHub;Integrated Security=true;Encrypt=true;TrustServerCertificate=false", false));
    }
}
