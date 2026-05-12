using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace SaasBuilder.IntegrationTests;

/// <summary>
/// Load-bearing RLS acceptance tests.
/// These tests prove tenant isolation at the Postgres layer — independent of EF Core filters —
/// so they remain valid even if the EF global query filter is accidentally bypassed.
/// </summary>
[Collection("RlsFixture")]
public sealed class RlsTenantBoundaryTests
{
    private readonly RlsFixture _fixture;

    public RlsTenantBoundaryTests(RlsFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    // ── Test 1: Tenant A sees only Tenant A rows ──────────────────────────────────
    [Fact]
    public async Task ReadOnly_WhenTenantAContextActive_CanReadOnlyTenantARows()
    {
        List<string> rows = await QueryValuesAsync(RlsFixture.TenantA, "SELECT value FROM test_scoped_items");

        rows.Should().HaveCount(2);
        rows.Should().AllSatisfy(v => v.Should().StartWith("tenant-a-"));
    }

    // ── Test 2: No tenant context → empty result (RLS evaluates null → false) ──────
    [Fact]
    public async Task ReadOnly_WhenNoTenantContext_ReturnsEmpty()
    {
        List<string> rows = await QueryValuesWithoutTenantAsync("SELECT value FROM test_scoped_items");

        rows.Should().BeEmpty();
    }

    // ── Test 3: WHERE 1=1 trick does not bypass RLS ───────────────────────────────
    [Fact]
    public async Task ReadOnly_CrossTenantWithWhere1Equals1_StillBlocked()
    {
        List<string> rows = await QueryValuesAsync(RlsFixture.TenantA, "SELECT value FROM test_scoped_items WHERE 1=1");

        rows.Should().HaveCount(2, because: "WHERE 1=1 does not bypass RLS");
        rows.Should().AllSatisfy(v => v.Should().StartWith("tenant-a-"));
    }

    // ── Test 4: Insert with wrong tenant_id rejected by WITH CHECK ────────────────
    [Fact]
    public async Task Insert_AcrossTenant_Rejected()
    {
        Func<Task> act = async () =>
        {
            await using NpgsqlConnection conn = new NpgsqlConnection(_fixture.ConnectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            await using NpgsqlTransaction tx = await conn.BeginTransactionAsync().ConfigureAwait(false);

            // Switch to the restricted role so FORCE ROW LEVEL SECURITY WITH CHECK is enforced.
            await using (NpgsqlCommand setRole = conn.CreateCommand())
            {
                setRole.Transaction = tx;
                setRole.CommandText = "SET LOCAL ROLE rls_test_user";
                await setRole.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await using (NpgsqlCommand setCmd = conn.CreateCommand())
            {
                setCmd.Transaction = tx;
                setCmd.CommandText = $"SET LOCAL app.tenant_id = '{RlsFixture.TenantA}'";
                await setCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            await using NpgsqlCommand insertCmd = conn.CreateCommand();
            insertCmd.Transaction = tx;
            insertCmd.CommandText =
                $"INSERT INTO test_scoped_items (id, tenant_id, value) VALUES (gen_random_uuid(), '{RlsFixture.TenantB}', 'cross-tenant')";
            await insertCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            await tx.CommitAsync().ConfigureAwait(false);
        };

        // Postgres raises 42501 (insufficient_privilege) for RLS WITH CHECK violation
        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "42501");
    }

    // ── Test 5: FORCE ROW LEVEL SECURITY — table owner is also filtered ───────────
    [Fact]
    public async Task ForceRowLevel_EvenAsOwner_TenantBRowsNotVisible()
    {
        // The Testcontainers Postgres connection user IS the table owner.
        // FORCE ROW LEVEL SECURITY means even the owner sees filtered rows.
        // As Tenant A, only 2 rows should be visible, not all 3.
        List<string> rows = await QueryValuesAsync(
            RlsFixture.TenantA,
            "SELECT value FROM test_scoped_items");

        rows.Should().HaveCount(2, because: "FORCE ROW LEVEL SECURITY filters even the table owner");
    }

    // ConfigureAwait(false) is correct in private helper methods — xUnit1030 only applies to [Fact]/[Theory] methods.
    private async Task<List<string>> QueryValuesAsync(Guid tenantId, string sql)
    {
        var results = new List<string>();

        await using NpgsqlConnection conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        await using NpgsqlTransaction tx = await conn.BeginTransactionAsync().ConfigureAwait(false);

        // Switch to a non-superuser role so FORCE ROW LEVEL SECURITY is respected.
        // Postgres superusers bypass RLS; rls_test_user is a plain login role that obeys policies.
        await using (NpgsqlCommand setRole = conn.CreateCommand())
        {
            setRole.Transaction = tx;
            setRole.CommandText = "SET LOCAL ROLE rls_test_user";
            await setRole.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using (NpgsqlCommand setCmd = conn.CreateCommand())
        {
            setCmd.Transaction = tx;
            setCmd.CommandText = $"SET LOCAL app.tenant_id = '{tenantId}'";
            await setCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        // Reader must be disposed before CommitAsync — Npgsql does not allow a command
        // in progress while the transaction is being committed (NpgsqlOperationInProgressException).
        await using (NpgsqlCommand queryCmd = conn.CreateCommand())
        {
            queryCmd.Transaction = tx;
            queryCmd.CommandText = sql;
            await using NpgsqlDataReader reader = await queryCmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                results.Add(reader.GetString(0));
            }
        } // reader and queryCmd disposed here, before CommitAsync.

        await tx.CommitAsync().ConfigureAwait(false);
        return results;
    }

    private async Task<List<string>> QueryValuesWithoutTenantAsync(string sql)
    {
        var results = new List<string>();

        await using NpgsqlConnection conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        // Switch to a non-superuser role so FORCE ROW LEVEL SECURITY is respected.
        await using (NpgsqlCommand setRole = conn.CreateCommand())
        {
            setRole.CommandText = "SET ROLE rls_test_user";
            await setRole.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using NpgsqlCommand queryCmd = conn.CreateCommand();
        queryCmd.CommandText = sql;
        await using NpgsqlDataReader reader = await queryCmd.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }
}
