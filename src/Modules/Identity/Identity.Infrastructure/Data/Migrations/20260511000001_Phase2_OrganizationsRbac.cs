using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Data.Migrations;

/// <summary>
/// Migration 20260511000001 — Phase 2: Organizations, Members, Invitations, Roles, Permissions, RolePermissions.
/// </summary>
/// <remarks>
/// Tables created in the <c>identity</c> schema to match existing module conventions.
/// All organization-scoped tables carry <c>organization_id</c> for efficient join filtering.
/// Tenant-scoped RLS policy is enforced at the DB level via the existing <c>app.tenant_id</c>
/// session variable pattern established in migration 002_TenantIdColumns.
/// </remarks>
public partial class Phase2OrganizationsRbac : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Roles ────────────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "roles",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                IsSystem = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_roles", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_roles_OrganizationId_Name",
            schema: "identity",
            table: "roles",
            columns: new[] { "OrganizationId", "Name" },
            unique: true);

        // ── Permissions ───────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "permissions",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Resource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_permissions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_permissions_Resource_Action_Scope",
            schema: "identity",
            table: "permissions",
            columns: new[] { "Resource", "Action", "Scope" },
            unique: true);

        // ── RolePermissions ───────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "role_permissions",
            schema: "identity",
            columns: table => new
            {
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_role_permissions", x => new { x.RoleId, x.PermissionId });
                table.ForeignKey(
                    name: "FK_role_permissions_roles_RoleId",
                    column: x => x.RoleId,
                    principalSchema: "identity",
                    principalTable: "roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_role_permissions_permissions_PermissionId",
                    column: x => x.PermissionId,
                    principalSchema: "identity",
                    principalTable: "permissions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // ── Organizations ─────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "organizations",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                BrandingJson = table.Column<string>(type: "jsonb", nullable: true),
                SettingsJson = table.Column<string>(type: "jsonb", nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_organizations", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_organizations_TenantId_Slug",
            schema: "identity",
            table: "organizations",
            columns: new[] { "TenantId", "Slug" },
            unique: true);

        // RLS policy — organizations are tenant-scoped.
        migrationBuilder.Sql(@"
            ALTER TABLE identity.organizations ENABLE ROW LEVEL SECURITY;
            ALTER TABLE identity.organizations FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation ON identity.organizations
                USING (""TenantId"" = current_setting('app.tenant_id', true)::uuid);
        ");

        // ── OrganizationMembers ───────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "organization_members",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                InvitedById = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_organization_members", x => x.Id);
                table.ForeignKey(
                    name: "FK_organization_members_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "identity",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_organization_members_OrganizationId_UserId_Status",
            schema: "identity",
            table: "organization_members",
            columns: new[] { "OrganizationId", "UserId", "Status" });

        // ── OrganizationInvitations ───────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "organization_invitations",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RedeemedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_organization_invitations", x => x.Id);
                table.ForeignKey(
                    name: "FK_organization_invitations_organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalSchema: "identity",
                    principalTable: "organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_organization_invitations_TokenHash",
            schema: "identity",
            table: "organization_invitations",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_organization_invitations_OrganizationId_Email",
            schema: "identity",
            table: "organization_invitations",
            columns: new[] { "OrganizationId", "Email" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "organization_invitations", schema: "identity");
        migrationBuilder.DropTable(name: "organization_members", schema: "identity");

        migrationBuilder.Sql(@"DROP POLICY IF EXISTS tenant_isolation ON identity.organizations;");
        migrationBuilder.DropTable(name: "organizations", schema: "identity");

        migrationBuilder.DropTable(name: "role_permissions", schema: "identity");
        migrationBuilder.DropTable(name: "permissions", schema: "identity");
        migrationBuilder.DropTable(name: "roles", schema: "identity");
    }
}
