using System;
using System.Threading.Tasks;
using Testcontainers.PostgreSql;
using Xunit;

namespace Chassis.IntegrationTests;

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

        CREATE POLICY tenant_isolation ON test_scoped_items
            USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
            WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);

        INSERT INTO test_scoped_items (id, tenant_id, value) VALUES
            (gen_random_uuid(), '{TenantA}', 'tenant-a-row-1'),
            (gen_random_uuid(), '{TenantA}', 'tenant-a-row-2');

        INSERT INTO test_scoped_items (id, tenant_id, value) VALUES
            (gen_random_uuid(), '{TenantB}', 'tenant-b-row-1');
        """;
}
