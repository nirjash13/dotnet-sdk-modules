namespace Identity.Contracts;

/// <summary>
/// DTO representing the OAuth 2.0 token response payload returned from <c>/connect/token</c>.
/// Matches the RFC 6749 §5.1 structure for cross-framework consumption.
/// </summary>
public sealed class TokenResponse
{
    /// <summary>Gets or sets the issued access token.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the token type (always "Bearer" for this server).</summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>Gets or sets the lifetime in seconds of the access token.</summary>
    public int ExpiresIn { get; set; }

    /// <summary>Gets or sets the refresh token, if one was issued.</summary>
    public string? RefreshToken { get; set; }

    /// <summary>Gets or sets the space-delimited list of scopes included in the token.</summary>
    public string? Scope { get; set; }
}
