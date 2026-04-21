using FluentValidation;

namespace Identity.Application.Validators;

/// <summary>
/// Validates a raw token request before it is forwarded to OpenIddict.
/// OpenIddict performs its own grant-type validation; this validator provides
/// application-layer defence-in-depth for fields we control.
/// </summary>
public sealed class TokenRequestValidator : AbstractValidator<TokenRequestDto>
{
    /// <summary>Initializes the validator with validation rules.</summary>
    public TokenRequestValidator()
    {
        RuleFor(x => x.GrantType)
            .NotEmpty()
            .WithMessage("grant_type is required.")
            .Must(g => g is "client_credentials" or "authorization_code" or "refresh_token")
            .WithMessage("Unsupported grant_type. Allowed: client_credentials, authorization_code, refresh_token.");

        RuleFor(x => x.ClientId)
            .NotEmpty()
            .WithMessage("client_id is required.")
            .MaximumLength(256);
    }
}

/// <summary>
/// DTO for the incoming token request fields this module validates.
/// </summary>
public sealed class TokenRequestDto
{
    /// <summary>Gets or sets the OAuth 2.0 grant type.</summary>
    public string GrantType { get; set; } = string.Empty;

    /// <summary>Gets or sets the OAuth 2.0 client identifier.</summary>
    public string ClientId { get; set; } = string.Empty;
}
