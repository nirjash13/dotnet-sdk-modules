using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Services;

/// <summary>
/// Application-layer interface for loading signing and encryption certificates.
/// Implementations live in Infrastructure: dev uses OpenIddict managed dev certs;
/// prod uses the OS certificate store (by thumbprint) or Azure Key Vault.
/// </summary>
public interface ICertificateProvider
{
    /// <summary>
    /// Returns the signing certificate (<c>RSA 2048+</c>, with private key).
    /// Returns <see langword="null"/> when running in development mode
    /// (OpenIddict's built-in development signing certificate is used instead).
    /// </summary>
    Task<X509Certificate2?> GetSigningCertificateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the encryption certificate (<c>RSA 2048+</c>, with private key).
    /// Returns <see langword="null"/> when running in development mode
    /// (OpenIddict's built-in development encryption certificate is used instead).
    /// </summary>
    Task<X509Certificate2?> GetEncryptionCertificateAsync(CancellationToken cancellationToken = default);
}
