using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Certificates;

/// <summary>
/// Loads signing and encryption certificates for the OpenIddict server.
/// </summary>
/// <remarks>
/// <para>
/// <b>Development:</b> returns <see langword="null"/> so the caller uses
/// OpenIddict's built-in development certificates.
/// </para>
/// <para>
/// <b>Production:</b> loads from the OS certificate store by thumbprint.
/// Configure via <c>Identity:Certificates:SigningThumbprint</c> and
/// <c>Identity:Certificates:EncryptionThumbprint</c> in environment variables.
/// </para>
/// <para>
/// TODO Phase 7: add a 24-hour polling refresh per §13 Q1 decision (Option A).
/// </para>
/// </remarks>
public sealed class CertificateLoader(
    IConfiguration configuration,
    ILogger<CertificateLoader> logger)
    : ICertificateProvider
{
    private const string SigningThumbprintKey = "Identity:Certificates:SigningThumbprint";
    private const string EncryptionThumbprintKey = "Identity:Certificates:EncryptionThumbprint";

    /// <inheritdoc />
    public Task<X509Certificate2?> GetSigningCertificateAsync(CancellationToken cancellationToken = default)
        => LoadFromStoreAsync(SigningThumbprintKey, "signing", cancellationToken);

    /// <inheritdoc />
    public Task<X509Certificate2?> GetEncryptionCertificateAsync(CancellationToken cancellationToken = default)
        => LoadFromStoreAsync(EncryptionThumbprintKey, "encryption", cancellationToken);

    private Task<X509Certificate2?> LoadFromStoreAsync(
        string configKey,
        string purpose,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string? thumbprint = configuration[configKey];
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            logger.LogDebug(
                "CertificateLoader: no thumbprint for '{Purpose}' ({ConfigKey}); dev certificate will be used.",
                purpose,
                configKey);
            return Task.FromResult<X509Certificate2?>(null);
        }

        thumbprint = thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

        using X509Store currentUserStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        currentUserStore.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        X509Certificate2Collection found = currentUserStore.Certificates.Find(
            X509FindType.FindByThumbprint,
            thumbprint,
            validOnly: true);

        if (found.Count == 0)
        {
            currentUserStore.Close();

            using X509Store machineStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            machineStore.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            found = machineStore.Certificates.Find(
                X509FindType.FindByThumbprint,
                thumbprint,
                validOnly: true);
        }

        if (found.Count == 0)
        {
            logger.LogError(
                "CertificateLoader: certificate with thumbprint '{Thumbprint}' not found for '{Purpose}'.",
                thumbprint,
                purpose);
            return Task.FromResult<X509Certificate2?>(null);
        }

        X509Certificate2 cert = found[0];
        logger.LogInformation(
            "CertificateLoader: loaded {Purpose} certificate Subject='{Subject}' Expiry={Expiry:yyyy-MM-dd}.",
            purpose,
            cert.Subject,
            cert.NotAfter);

        return Task.FromResult<X509Certificate2?>(cert);
    }
}
