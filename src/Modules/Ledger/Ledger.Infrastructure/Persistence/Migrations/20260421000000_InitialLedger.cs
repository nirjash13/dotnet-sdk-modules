using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ledger.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialLedger : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Create the ledger schema.
        migrationBuilder.Sql("CREATE SCHEMA IF NOT EXISTS ledger;");

        // accounts table.
        migrationBuilder.CreateTable(
            name: "accounts",
            schema: "ledger",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Currency = table.Column<string>(type: "char(3)", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_accounts", x => x.Id);
            });

        // Index for tenant-scoped account lookups.
        migrationBuilder.CreateIndex(
            name: "IX_accounts_TenantId_Id",
            table: "accounts",
            schema: "ledger",
            columns: new[] { "TenantId", "Id" });

        // postings table.
        migrationBuilder.CreateTable(
            name: "postings",
            schema: "ledger",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                amount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                currency = table.Column<string>(type: "char(3)", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Memo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_postings", x => x.Id);
                table.ForeignKey(
                    name: "FK_postings_accounts_AccountId",
                    column: x => x.AccountId,
                    principalSchema: "ledger",
                    principalTable: "accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // Partial unique index for idempotency on postings.
        // Ensures at-most-once semantics per (tenant_id, idempotency_key) pair.
        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX \"ix_postings_tenant_idempotency_key\" " +
            "ON ledger.postings (\"TenantId\", \"IdempotencyKey\") " +
            "WHERE \"IdempotencyKey\" IS NOT NULL;");

        migrationBuilder.CreateIndex(
            name: "IX_postings_AccountId",
            table: "postings",
            schema: "ledger",
            column: "AccountId");

        // RLS on accounts.
        migrationBuilder.Sql("ALTER TABLE ledger.accounts ENABLE ROW LEVEL SECURITY;");
        migrationBuilder.Sql("ALTER TABLE ledger.accounts FORCE ROW LEVEL SECURITY;");
        migrationBuilder.Sql(
            "CREATE POLICY tenant_isolation ON ledger.accounts " +
            "USING (\"TenantId\" = current_setting('app.tenant_id', true)::uuid) " +
            "WITH CHECK (\"TenantId\" = current_setting('app.tenant_id', true)::uuid);");

        // RLS on postings.
        migrationBuilder.Sql("ALTER TABLE ledger.postings ENABLE ROW LEVEL SECURITY;");
        migrationBuilder.Sql("ALTER TABLE ledger.postings FORCE ROW LEVEL SECURITY;");
        migrationBuilder.Sql(
            "CREATE POLICY tenant_isolation ON ledger.postings " +
            "USING (\"TenantId\" = current_setting('app.tenant_id', true)::uuid) " +
            "WITH CHECK (\"TenantId\" = current_setting('app.tenant_id', true)::uuid);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop RLS policies before dropping tables.
        migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON ledger.postings;");
        migrationBuilder.Sql("ALTER TABLE ledger.postings DISABLE ROW LEVEL SECURITY;");
        migrationBuilder.Sql("ALTER TABLE ledger.postings NO FORCE ROW LEVEL SECURITY;");

        migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON ledger.accounts;");
        migrationBuilder.Sql("ALTER TABLE ledger.accounts DISABLE ROW LEVEL SECURITY;");
        migrationBuilder.Sql("ALTER TABLE ledger.accounts NO FORCE ROW LEVEL SECURITY;");

        migrationBuilder.DropTable(
            name: "postings",
            schema: "ledger");

        migrationBuilder.DropTable(
            name: "accounts",
            schema: "ledger");
    }
}
