-- Identity module — organizations, members, invitations, RBAC.
-- Apply via:
--   psql "$DATABASE_URL" -f migrations/identity/001_organizations_rbac.sql
-- Or via EF Core (preferred — keeps the migration history table in sync):
--   dotnet ef database update --project src/Modules/Identity/Identity.Infrastructure --startup-project samples/SaasBuilder.Sample.Host
--
-- This file mirrors the EF Core migration
-- src/Modules/Identity/Identity.Infrastructure/Data/Migrations/20260511000001_Phase2_OrganizationsRbac.cs
-- so that the SaasBuilder.ArchitectureTests.MigrationRlsCoherenceTests can verify
-- a CREATE POLICY exists for every ITenantScoped entity at the SQL layer.
--
-- Tenancy:
--   * organizations is tenant-scoped (carries TenantId; ITenantScoped).
--   * organization_members and organization_invitations are organization-scoped, transitively
--     tenant-scoped via their parent organization. Their RLS is enforced at the application
--     layer (EF Core global query filter on the parent join). The architecture test scans only
--     directly-tenant-scoped entities — these tables do not need a CREATE POLICY here.
--   * roles, permissions, role_permissions are global (system roles have OrganizationId=NULL);
--     not tenant-scoped.
--
-- This script is idempotent (IF NOT EXISTS guards where possible).

-- ── Schema ────────────────────────────────────────────────────────────────────

CREATE SCHEMA IF NOT EXISTS identity;

-- ── organizations ─────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS identity.organizations (
    "Id"           uuid                     NOT NULL,
    "TenantId"     uuid                     NOT NULL,
    "Slug"         character varying(64)    NOT NULL,
    "Name"         character varying(200)   NOT NULL,
    "BrandingJson" text                     NULL,
    "SettingsJson" text                     NULL,
    "Status"       integer                  NOT NULL,
    "CreatedAt"    timestamp with time zone NOT NULL,
    "UpdatedAt"    timestamp with time zone NOT NULL,
    CONSTRAINT "PK_organizations" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_organizations_TenantId_Slug"
    ON identity.organizations ("TenantId", "Slug");

-- RLS: organizations
ALTER TABLE identity.organizations ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.organizations FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON identity.organizations
    USING ("TenantId" = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK ("TenantId" = current_setting('app.tenant_id', true)::uuid);

-- ── organization_members ──────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS identity.organization_members (
    "Id"             uuid                     NOT NULL,
    "OrganizationId" uuid                     NOT NULL,
    "UserId"         uuid                     NOT NULL,
    "RoleId"         uuid                     NOT NULL,
    "Status"         integer                  NOT NULL,
    "JoinedAt"       timestamp with time zone NOT NULL,
    "InvitedById"    uuid                     NULL,
    CONSTRAINT "PK_organization_members" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_organization_members_organizations_OrganizationId"
        FOREIGN KEY ("OrganizationId") REFERENCES identity.organizations ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_organization_members_OrganizationId_UserId"
    ON identity.organization_members ("OrganizationId", "UserId");

-- ── organization_invitations ──────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS identity.organization_invitations (
    "Id"             uuid                     NOT NULL,
    "OrganizationId" uuid                     NOT NULL,
    "Email"          character varying(254)   NOT NULL,
    "RoleId"         uuid                     NOT NULL,
    "TokenHash"      character varying(128)   NOT NULL,
    "ExpiresAt"      timestamp with time zone NOT NULL,
    "RedeemedAt"     timestamp with time zone NULL,
    "RevokedAt"      timestamp with time zone NULL,
    "CreatedById"    uuid                     NOT NULL,
    CONSTRAINT "PK_organization_invitations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_organization_invitations_organizations_OrganizationId"
        FOREIGN KEY ("OrganizationId") REFERENCES identity.organizations ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_organization_invitations_OrganizationId"
    ON identity.organization_invitations ("OrganizationId");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_organization_invitations_TokenHash"
    ON identity.organization_invitations ("TokenHash");

-- ── roles ─────────────────────────────────────────────────────────────────────
-- Roles can be system-wide (OrganizationId IS NULL) or per-organization.
-- Not tenant-scoped at the SQL level; access control enforced at the application layer.

CREATE TABLE IF NOT EXISTS identity.roles (
    "Id"             uuid                     NOT NULL,
    "OrganizationId" uuid                     NULL,
    "Name"           character varying(100)   NOT NULL,
    "IsSystem"       boolean                  NOT NULL,
    CONSTRAINT "PK_roles" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_roles_OrganizationId_Name"
    ON identity.roles ("OrganizationId", "Name");

-- ── permissions ───────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS identity.permissions (
    "Id"       uuid                   NOT NULL,
    "Resource" character varying(100) NOT NULL,
    "Action"   character varying(100) NOT NULL,
    "Scope"    character varying(50)  NOT NULL,
    CONSTRAINT "PK_permissions" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_permissions_Resource_Action_Scope"
    ON identity.permissions ("Resource", "Action", "Scope");

-- ── role_permissions ──────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS identity.role_permissions (
    "RoleId"       uuid NOT NULL,
    "PermissionId" uuid NOT NULL,
    CONSTRAINT "PK_role_permissions" PRIMARY KEY ("RoleId", "PermissionId"),
    CONSTRAINT "FK_role_permissions_roles_RoleId"
        FOREIGN KEY ("RoleId") REFERENCES identity.roles ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_role_permissions_permissions_PermissionId"
        FOREIGN KEY ("PermissionId") REFERENCES identity.permissions ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_role_permissions_PermissionId"
    ON identity.role_permissions ("PermissionId");
