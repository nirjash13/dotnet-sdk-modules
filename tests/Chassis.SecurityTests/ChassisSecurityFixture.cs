using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Chassis.Host;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Xunit;

namespace Chassis.SecurityTests;

/// <summary>
/// Shared security-test fixture. Boots <c>Chassis.Host</c> in-process against an ephemeral
/// Testcontainers Postgres instance and wires a deterministic RSA signing key so JWT security
/// tests run without a live OpenIddict server.
/// </summary>
/// <remarks>
/// Tests that require Docker will ERROR when Docker is unavailable (accepted — CI always has Docker).
/// The fixture seeds a dev client and tenant so endpoints that require a tenant context work.
/// </remarks>
public sealed class ChassisSecurityFixture : IAsyncLifetime
{
    /// <summary>The test issuer embedded in tokens validated by the test host.</summary>
    public const string TestIssuer = "https://chassis-security-test-issuer";

    /// <summary>The test audience embedded in tokens validated by the test host.</summary>
    public const string TestAudience = "chassis-api";

    // Fixed identifiers — deterministic values prevent test flakiness from Guid.NewGuid().
    public static readonly Guid TenantA = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid TenantB = new Guid("bbbbbbbb-0000-0000-0000-000000000002");
    public static readonly Guid UserA = new Guid("cccccccc-0000-0000-0000-000000000003");
    public static readonly Guid UserB = new Guid("dddddddd-0000-0000-0000-000000000004");

    // Static RSA key — all minted tokens use this key;
    // the test host is configured to validate against the same public key.
    private static readonly RsaSecurityKey _signingKey = CreateTestSigningKey();

    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    private SecurityWebApplicationFactory? _factory;
    private string? _connectionString;

    /// <summary>Gets the RSA signing key used for all test token operations.</summary>
    public static RsaSecurityKey SigningKey => _signingKey;

    /// <summary>Gets the Postgres connection string (available after <see cref="InitializeAsync"/>).</summary>
    public string ConnectionString =>
        _connectionString ?? throw new InvalidOperationException("Fixture not initialised.");

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);
        _connectionString = _postgres.GetConnectionString();
        _factory = new SecurityWebApplicationFactory(_connectionString, _signingKey);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync().ConfigureAwait(false);
        }

        await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> that does not follow redirects automatically.
    /// </summary>
    public HttpClient CreateClient()
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("Fixture not initialised.");
        }

        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> targeting an in-process server configured with
    /// the provided rate-limit permit count for the test window. Allows rate-limit tests to
    /// use a tight window (e.g., 2 permits) without modifying the shared factory.
    /// </summary>
    public HttpClient CreateClientWithRateLimit(int permitLimit)
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("Fixture not initialised.");
        }

        WebApplicationFactory<Program> rateLimitFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimit:AuthEndpoints:PermitLimit"] = permitLimit.ToString(),
                    ["RateLimit:AuthEndpoints:WindowSeconds"] = "60",
                });
            });
        });

        return rateLimitFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    /// <summary>
    /// Mints a signed JWT using the test RSA key. All parameters have overridable defaults
    /// to simplify targeted tampering in security tests.
    /// </summary>
    public static string MintToken(
        Guid tenantId,
        Guid userId,
        string audience = TestAudience,
        string issuer = TestIssuer,
        DateTime? notBefore = null,
        DateTime? expires = null,
        SecurityKey? signingKey = null)
    {
        SecurityKey key = signingKey ?? _signingKey;
        string algorithm = key is RsaSecurityKey
            ? SecurityAlgorithms.RsaSha256
            : SecurityAlgorithms.HmacSha256;

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", userId.ToString()),
                new Claim("tenant_id", tenantId.ToString()),
            ]),
            Issuer = issuer,
            Audience = audience,
            NotBefore = notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(key, algorithm),
        };

        return new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
    }

    /// <summary>
    /// Mints a JWT with only the <c>sub</c> claim — no <c>tenant_id</c> claim.
    /// Used to test TenantMiddleware rejection of authenticated-but-untenant-scoped tokens.
    /// </summary>
    public static string MintTokenWithoutTenantClaim(Guid userId)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", userId.ToString()),
                // Intentionally omitting tenant_id claim.
            ]),
            Issuer = TestIssuer,
            Audience = TestAudience,
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256),
        };

        return new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
    }

    private static RsaSecurityKey CreateTestSigningKey()
    {
        // Test-only RSA key — never used in production.
        var rsa = RSA.Create(2048);
        return new RsaSecurityKey(rsa) { KeyId = "chassis-security-test-key" };
    }

    /// <summary>
    /// Inner <see cref="WebApplicationFactory{TEntryPoint}"/> that overrides DB connection,
    /// JWT validation, and OpenIddict startup to use the static test key.
    /// </summary>
    private sealed class SecurityWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;
        private readonly RsaSecurityKey _signingKey;

        public SecurityWebApplicationFactory(string connectionString, RsaSecurityKey signingKey)
        {
            _connectionString = connectionString;
            _signingKey = signingKey;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["Identity:SeedDevClient"] = "true",
                    ["Identity:AllowInsecureMetadata"] = "true",
                    ["Identity:Authority"] = TestIssuer,
                    ["Identity:Audience"] = TestAudience,
                    ["Identity:DevClient:Secret"] = "chassis-security-test-secret",
                });
            });

            // Replace JWT validation with static test key so MintToken() tokens are accepted.
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigureAll<JwtBearerOptions>(options =>
                {
                    options.Authority = null;
                    options.RequireHttpsMetadata = false;
                    options.BackchannelHttpHandler = null;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = _signingKey,
                        ValidateIssuer = true,
                        ValidIssuer = TestIssuer,
                        ValidateAudience = true,
                        ValidAudience = TestAudience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(5),
                        NameClaimType = "sub",
                        RoleClaimType = "roles",
                    };
                });
            });
        }
    }
}
