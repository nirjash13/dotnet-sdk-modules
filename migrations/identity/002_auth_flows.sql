-- Identity module — auth flows, MFA, API keys, impersonation sessions.
-- Apply via:
--   psql "$DATABASE_URL" -f migrations/identity/002_auth_flows.sql
-- Or via EF Core (preferred):
--   dotnet ef database update --project src/Modules/Identity/Identity.Infrastructure --startup-project samples/SaasBuilder.Sample.Host
--
-- This file mirrors the EF Core migration
-- src/Modules/Identity/Identity.Infrastructure/Data/Migrations/20260511000002_Phase2_AuthFlows.cs
--
-- Tenancy notes:
--   * email_verification_tokens, password_reset_tokens: keyed by UserId only; no TenantId column.
--   * totp_credentials: keyed by UserId only; no TenantId column.
--   * api_keys: tenant isolation is transitive via UserId/OrganizationId FK to tenant-scoped entities.
--   * impersonation_sessions: system-level; no tenant isolation required.
--
-- This script is idempotent (IF NOT EXISTS guards where possible).

-- ── Schema ────────────────────────────────────────────────────────────────────

CREATE SCHEMA IF NOT EXISTS identity;

-- ── Extend users table ────────────────────────────────────────────────────────

ALTER TABLE identity.users
    ADD COLUMN IF NOT EXISTS "IsEmailVerified"    boolean                  NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "EmailVerifiedAt"    timestamp with time zone NULL,
    ADD COLUMN IF NOT EXISTS "FailedLoginAttempts" integer                 NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "LockoutUntil"       timestamp with time zone NULL,
    ADD COLUMN IF NOT EXISTS "IsMfaEnabled"       boolean                  NOT NULL DEFAULT false;

-- ── email_verification_tokens ─────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS identity.email_verification_tokens (
    "Id"        uuid                     NOT NULL,
    "UserId"    uuid                     NOT NULL,
    "TokenHash" character varying(128)   NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "UsedAt"    timestamp with time zone NULL,
    CONSTRAINT "PK_email_verification_tokens" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_email_verification_tokens_TokenHash"
    ON identity.email_verification_tokens ("TokenHash");

CREATE INDEX IF NOT EXISTS "IX_email_verification_tokens_UserId_UsedAt"
    ON identity.email_verification_tokens ("UserId", "UsedAt");

-- ── password_reset_tokens ─────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS identity.password_reset_tokens (
    "Id"        uuid                     NOT NULL,
    "UserId"    uuid                     NOT NULL,
    "TokenHash" character varying(128)   NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "UsedAt"    timestamp with time zone NULL,
    CONSTRAINT "PK_password_reset_tokens" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_password_reset_tokens_TokenHash"
    ON identity.password_reset_tokens ("TokenHash");

CREATE INDEX IF NOT EXISTS "IX_password_reset_tokens_UserId_UsedAt"
    ON identity.password_reset_tokens ("UserId", "UsedAt");

-- ── totp_credentials ──────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS identity.totp_credentials (
    "Id"                     uuid                     NOT NULL,
    "UserId"                 uuid                     NOT NULL,
    "EncryptedSecret"        character varying(256)   NOT NULL,
    "IsConfirmed"            boolean                  NOT NULL DEFAULT false,
    "ConfirmedAt"            timestamp with time zone NULL,
    hashed_recovery_codes    jsonb                    NOT NULL DEFAULT '[]',
    CONSTRAINT "PK_totp_credentials" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_totp_credentials_UserId"
    ON identity.totp_credentials ("UserId");

-- ── api_keys ──────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS identity.api_keys (
    "Id"             uuid                     NOT NULL,
    "UserId"         uuid                     NULL,
    "OrganizationId" uuid                     NULL,
    "Name"           character varying(200)   NOT NULL,
    "KeyPrefix"      character varying(20)    NOT NULL,
    "KeyHash"        character varying(128)   NOT NULL,
    "ScopesJson"     jsonb                    NOT NULL DEFAULT '[]',
    "LastUsedAt"     timestamp with time zone NULL,
    "CreatedAt"      timestamp with time zone NOT NULL,
    "RevokedAt"      timestamp with time zone NULL,
    CONSTRAINT "PK_api_keys" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_api_keys_KeyHash"
    ON identity.api_keys ("KeyHash");

CREATE INDEX IF NOT EXISTS "IX_api_keys_UserId_RevokedAt"
    ON identity.api_keys ("UserId", "RevokedAt");

CREATE INDEX IF NOT EXISTS "IX_api_keys_OrganizationId_RevokedAt"
    ON identity.api_keys ("OrganizationId", "RevokedAt");

-- ── impersonation_sessions ────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS identity.impersonation_sessions (
    "Id"                  uuid                     NOT NULL,
    "AdminUserId"         uuid                     NOT NULL,
    "TargetUserId"        uuid                     NOT NULL,
    "Reason"              character varying(1000)  NOT NULL,
    "ImpersonationToken"  character varying(2048)  NOT NULL,
    "StartedAt"           timestamp with time zone NOT NULL,
    "ExpiresAt"           timestamp with time zone NOT NULL,
    "EndedAt"             timestamp with time zone NULL,
    CONSTRAINT "PK_impersonation_sessions" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_impersonation_sessions_AdminUserId_StartedAt"
    ON identity.impersonation_sessions ("AdminUserId", "StartedAt");

CREATE INDEX IF NOT EXISTS "IX_impersonation_sessions_TargetUserId_StartedAt"
    ON identity.impersonation_sessions ("TargetUserId", "StartedAt");

-- ── Note: No RLS policies needed for these tables ────────────────────────────
-- email_verification_tokens, password_reset_tokens, totp_credentials:
--   keyed by user_id only; application-layer auth enforces access.
-- api_keys: tenant isolation is transitive via user_id/organization_id FK.
-- impersonation_sessions: system-level; accessible only to system.admin role.
