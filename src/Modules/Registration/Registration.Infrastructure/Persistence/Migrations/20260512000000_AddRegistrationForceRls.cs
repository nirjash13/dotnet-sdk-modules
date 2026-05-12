using Microsoft.EntityFrameworkCore.Migrations;

namespace Registration.Infrastructure.Persistence.Migrations;

/// <summary>
/// Migration 20260512000000 — Enforce FORCE ROW LEVEL SECURITY on tenant-scoped
/// <c>registration.registration_saga_state</c> table (C-2 remediation).
/// </summary>
/// <remarks>
/// Follows the same pattern as <c>Ledger.InitialLedger</c> and
/// <c>Reporting.InitialReporting</c>: ENABLE + FORCE + tenant_isolation policy
/// using <c>current_setting('app.tenant_id', true)::uuid</c>.
/// </remarks>
public partial class AddRegistrationForceRls : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE registration.registration_saga_state ENABLE ROW LEVEL SECURITY;");
        migrationBuilder.Sql("ALTER TABLE registration.registration_saga_state FORCE ROW LEVEL SECURITY;");
        migrationBuilder.Sql(
            "CREATE POLICY tenant_isolation ON registration.registration_saga_state " +
            "USING (tenant_id = current_setting('app.tenant_id', true)::uuid) " +
            "WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON registration.registration_saga_state;");
        migrationBuilder.Sql("ALTER TABLE registration.registration_saga_state NO FORCE ROW LEVEL SECURITY;");
        migrationBuilder.Sql("ALTER TABLE registration.registration_saga_state DISABLE ROW LEVEL SECURITY;");
    }
}
