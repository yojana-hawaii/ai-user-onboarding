using Microsoft.Data.SqlClient;

namespace DaHub.Infrastructure.Persistence;

public static class DatabaseConnection
{
    public static string Validate(string? connectionString, bool allowUntrustedCertificate)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Database configuration is missing.");
        }

        var connection = new SqlConnectionStringBuilder(connectionString);
        if (connection.Encrypt == SqlConnectionEncryptOption.Optional ||
            (!allowUntrustedCertificate && connection.TrustServerCertificate))
        {
            throw new InvalidOperationException("Database connections must use verified TLS outside development.");
        }

        if (string.Equals(connection.UserID.Trim(), "sa", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The API requires a dedicated database identity.");
        }

        return connection.ConnectionString;
    }
}
