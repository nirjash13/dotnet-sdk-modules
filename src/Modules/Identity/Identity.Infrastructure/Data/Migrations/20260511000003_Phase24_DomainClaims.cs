using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Data.Migrations;

/// <summary>
/// Migration 20260511000003 — Phase 2.4: Organization domain claims.
/// Adds <c>organization_domain_claims</c> table with unique index on domain.
/// </summary>
public partial class Phase24DomainClaims : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "organization_domain_claims",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                VerificationToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_organization_domain_claims", x => x.Id);
                table.ForeignKey(
                    name: "FK_organization_domain_claims_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "identity",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_organization_domain_claims_Domain",
            schema: "identity",
            table: "organization_domain_claims",
            column: "Domain",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_organization_domain_claims_OrganizationId",
            schema: "identity",
            table: "organization_domain_claims",
            column: "OrganizationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "organization_domain_claims", schema: "identity");
    }
}
