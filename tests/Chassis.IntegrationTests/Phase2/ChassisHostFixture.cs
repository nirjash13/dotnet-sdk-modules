using System;
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

namespace Chassis.IntegrationTests.Phase2;

/// <summary>
/// Shared fixture that boots Chassis.Host in-process against a Testcontainers Postgres
/// instance and wires a deterministic RSA test signing key so JWT tests are fully
/// independent of OpenIddict's ephemeral dev certificate.
/// </summary>
/// <remarks>
/// The fixture replaces the production JWT Bearer validation configuration with a static
/// RSA key so that <see cref="MintToken"/> can produce tokens that the running host accepts.
/// This allows testing of tampered tokens without needing a live OpenIddict server.
///
/// Docker unavailable: if <see cref="InitializeAsync"/> throws, individual tests will
/// fail with a connection error. CI always runs with Docker available.
/// </remarks>
public sealed class ChassisHostFixture : IAsyncLifetime
{
    /// <summary>The test issuer value embedded in test tokens and validated by the test host.</summary>
    internal const string TestIssuer = "https://chassis-test-issuer";

    /// <summary>The test audience value embedded in test tokens and validated by the test host.</summary>
    internal const string TestAudience = "chassis-api";

    // Fixed tenant and user IDs for reproducible assertions.
    public static readonly Guid TenantA = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid TenantB = new Guid("bbbbbbbb-0000-0000-0000-000000000002");
    public static readonly Guid UserA = new Guid("cccccccc-0000-0000-0000-000000000003");
    public static readonly Guid UserB = new Guid("dddddddd-0000-0000-0000-000000000004");

    // The test signing key — all minted tokens use this key;
    // the test host is configured to validate against the same key.
    private static readonly RsaSecurityKey _signingKey = CreateSigningKey();

    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    private WebApplicationFactory<Program>? _factory;
    private string? _connectionString;

    /// <summary>Gets the RSA public/private key used for test token signing and validation.</summary>
    public static RsaSecurityKey SigningKey => _signingKey;

    /// <summary>Gets the live connection string to the Testcontainers Postgres instance.</summary>
    public string ConnectionString =>
        _connectionString ?? throw new InvalidOperationException(
            "Fixture not initialised — call InitializeAsync first.");

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);
        _connectionString = _postgres.GetConnectionString();
        _factory = new ChassisWebApplicationFactory(_connectionString, _signingKey);
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
    /// Creates an <see cref="HttpClient"/> that does not automatically follow redirects.
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
    /// Mints a signed JWT using the test RSA key.
    /// </summary>
    /// <param name="tenantId">The <c>tenant_id</c> claim value.</param>
    /// <param name="userId">The <c>sub</c> claim value.</param>
    /// <param name="roles">The <c>roles</c> claim values.</param>
    /// <param name="audience">Override the <c>aud</c> claim.</param>
    /// <param name="issuer">Override the <c>iss</c> claim.</param>
    /// <param name="notBefore">Override the <c>nbf</c> time.</param>
    /// <param name="expires">Override the <c>exp</c> time.</param>
    /// <param name="signingKey">Override the signing key.</param>
    public static string MintToken(
        Guid tenantId,
        Guid userId,
        string[]? roles = null,
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

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
                new[]
                {
                    new Claim("sub", userId.ToString()),
                    new Claim("tenant_id", tenantId.ToString()),
                }),
            Issuer = issuer,
            Audience = audience,
            NotBefore = notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(key, algorithm),
        };

        if (roles is not null)
        {
            foreach (string role in roles)
            {
                tokenDescriptor.Subject.AddClaim(new Claim("roles", role));
            }
        }

        var handler = new JwtSecurityTokenHandler();
        return handler.CreateEncodedJwt(tokenDescriptor);
    }

    private static RsaSecurityKey CreateSigningKey()
    {
        // Generate a test-only RSA key. Not used in production.
        var rsa = RSA.Create(2048);
        return new RsaSecurityKey(rsa) { KeyId = "chassis-test-key" };
    }

    /// <summary>
    /// WebApplicationFactory that overrides DB connection, OpenIddict startup, and JWT
    /// validation to use the static test signing key.
    /// </summary>
    private sealed class ChassisWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;
        private readonly RsaSecurityKey _signingKey;

        public ChassisWebApplicationFactory(string connectionString, RsaSecurityKey signingKey)
        {
            _connectionString = connectionString;
            _signingKey = signingKey;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(
                    new System.Collections.Generic.Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = _connectionString,
                        ["Identity:SeedDevClient"] = "true",
                        ["Identity:AllowInsecureMetadata"] = "true",
                        ["Identity:Authority"] = TestIssuer,
                        ["Identity:Audience"] = TestAudience,
                        // Required by DevClientDataSeeder when Identity:SeedDevClient=true.
                        ["Identity:DevClient:Secret"] = "chassis-test-secret",
                    });
            });

            // After the host's services are registered, replace JWT validation so that
            // tokens minted by MintToken() are accepted without OIDC discovery.
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
