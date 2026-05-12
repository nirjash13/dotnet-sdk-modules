using System;
using System.Threading.Tasks;
using Testcontainers.PostgreSql;
using Xunit;

namespace SaasBuilder.IntegrationTests;

/// <summary>
/// Collection definition that shares <see cref="RlsFixture"/> across all tests in the
/// "RlsFixture" collection, ensuring a single Testcontainers Postgres instance is booted
/// and that tests run sequentially to avoid <c>NpgsqlOperationInProgressException</c> races.
/// </summary>
[CollectionDefinition("RlsFixture")]
public sealed class RlsFixtureCollection : ICollectionFixture<RlsFixture>
{
}

/// <summary>
/// Shared Testcontainers fixture that boots an ephemeral Postgres instance, creates a
/// tenant-scoped table with RLS applied, and seeds rows for two tenants.
/// </summary>
public sealed class RlsFixture : IAsyncLifetime
{
    /// <summary>Tenant A identifier — used in test data seeding.</summary>
    public static readonly Guid TenantA = new Guid("aaaaaaaa-0000-0000-0000-000000000001");

    /// <summary>Tenant B identifier — used in test data seeding.</summary>
    public static readonly Guid TenantB = new Guid("bbbbbbbb-0000-0000-0000-000000000002");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    /// <summary>Gets the connection string for the ephemeral Postgres instance.</summary>
    public string ConnectionString => _postgres.GetConnectionString();

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);
        await _postgres.ExecScriptAsync(BuildSetupScript()).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    private static string BuildSetupScript() => $"""
        CREATE TABLE IF NOT EXISTS test_scoped_items (
            id        uuid         PRIMARY KEY,
            tenant_id uuid         NOT NULL,
            value     text         NOT NULL
        );

        ALTER TABLE test_scoped_items ENABLE ROW LEVEL SECURITY;
        ALTER TABLE test_scoped_items FORCE ROW LEVEL SECURITY;

        -- nullif converts empty string to NULL so that uuid cast does not raise 22P02.
        -- When no tenant is set, current_setting returns '' (not NULL) with missing_ok=true.
        CREATE POLICY tenant_isolation ON test_scoped_items
            USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
            WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);

        -- A restricted non-superuser role so FORCE ROW LEVEL SECURITY is respected.
        -- Postgres superusers bypass RLS; tests must use this role when asserting RLS.
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rls_test_user') THEN
                CREATE ROLE rls_test_user LOGIN PASSWORD 'rls_test_pass';
            END IF;
        END;
        $$;

        GRANT SELECT, INSERT ON test_scoped_items TO rls_test_user;

        INSERT INTO test_scoped_items (id, tenant_id, value) VALUES
            (gen_random_uuid(), '{TenantA}', 'tenant-a-row-1'),
            (gen_random_uuid(), '{TenantA}', 'tenant-a-row-2');

        INSERT INTO test_scoped_items (id, tenant_id, value) VALUES
            (gen_random_uuid(), '{TenantB}', 'tenant-b-row-1');
        """;
}
