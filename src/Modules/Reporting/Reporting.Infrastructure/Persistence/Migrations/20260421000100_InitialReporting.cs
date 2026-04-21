using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations;

/// <summary>
/// Initial schema for the Reporting bounded context:
/// <c>reporting.transaction_projections</c> table with RLS policies and idempotency index.
/// </summary>
public partial class InitialReporting : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE SCHEMA IF NOT EXISTS reporting;");

        migrationBuilder.CreateTable(
            name: "transaction_projections",
            schema: "reporting",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                SourceMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                Currency = table.Column<string>(type: "char(3)", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_transaction_projections", x => x.Id);
            });

        // Business-level idempotency guard: prevents duplicate projection rows
        // for the same (tenant, source message) pair.
        migrationBuilder.CreateIndex(
            name: "ix_transaction_projections_tenant_source_message",
            table: "transaction_projections",
            schema: "reporting",
            columns: new[] { "TenantId", "SourceMessageId" },
            unique: true);

        // Composite index for tenant-scoped queries.
        migrationBuilder.CreateIndex(
            name: "IX_transaction_projections_TenantId_Id",
            table: "transaction_projections",
            schema: "reporting",
            columns: new[] { "TenantId", "Id" });

        // RLS policies — mirror the Ledger pattern.
        migrationBuilder.Sql("ALTER TABLE reporting.transaction_projections ENABLE ROW LEVEL SECURITY;");
        migrationBuilder.Sql("ALTER TABLE reporting.transaction_projections FORCE ROW LEVEL SECURITY;");
        migrationBuilder.Sql(
            "CREATE POLICY tenant_isolation ON reporting.transaction_projections " +
            "USING (\"TenantId\" = current_setting('app.tenant_id', true)::uuid) " +
            "WITH CHECK (\"TenantId\" = current_setting('app.tenant_id', true)::uuid);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON reporting.transaction_projections;");
        migrationBuilder.Sql("ALTER TABLE reporting.transaction_projections DISABLE ROW LEVEL SECURITY;");
        migrationBuilder.Sql("ALTER TABLE reporting.transaction_projections NO FORCE ROW LEVEL SECURITY;");

        migrationBuilder.DropTable(
            name: "transaction_projections",
            schema: "reporting");
    }
}
