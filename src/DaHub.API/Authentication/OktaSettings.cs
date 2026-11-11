namespace DaHub.API.Authentication;

public sealed class OktaSettings
{
    public bool Enabled { get; set; }
    public string Issuer { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecretFile { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string PublicOrigin { get; set; } = "";

    public void Validate()
    {
        if (!Uri.TryCreate(Issuer, UriKind.Absolute, out var issuer) || issuer.Scheme != "https"
            || issuer.IsLoopback || !string.IsNullOrEmpty(issuer.Query) || !string.IsNullOrEmpty(issuer.Fragment)
            || !string.IsNullOrEmpty(issuer.UserInfo) || Issuer.EndsWith('/'))
            throw new InvalidOperationException("Okta:Issuer must be an exact trusted HTTPS issuer without a trailing slash.");
        if (!Uri.TryCreate(PublicOrigin, UriKind.Absolute, out var origin) || origin.Scheme != "https"
            || !string.IsNullOrEmpty(origin.UserInfo)
            || PublicOrigin != origin.GetLeftPart(UriPartial.Authority))
            throw new InvalidOperationException("Okta:PublicOrigin must be an HTTPS origin without a path or trailing slash.");
        if (string.IsNullOrWhiteSpace(ClientId)
            || (string.IsNullOrWhiteSpace(ClientSecretFile) == string.IsNullOrWhiteSpace(ClientSecret)))
            throw new InvalidOperationException("Okta client ID and exactly one secret source (file or configuration) are required.");
    }

    public static bool IsLocalReturn(string value) => value.StartsWith('/')
        && !value.StartsWith("//", StringComparison.Ordinal) && !value.Contains('\\')
        && !value.Any(char.IsControl);
}
