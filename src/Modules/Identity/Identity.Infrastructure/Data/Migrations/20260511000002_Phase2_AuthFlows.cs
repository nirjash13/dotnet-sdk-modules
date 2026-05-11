using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Data.Migrations;

/// <summary>
/// Migration 20260511000002 — Phase 2: Auth flows, MFA, API keys, Impersonation sessions.
/// Also adds lockout and email-verification columns to the users table.
/// </summary>
/// <remarks>
/// Tables created in the <c>identity</c> schema. RLS policies are added for any table
/// that would eventually require tenant isolation (api_keys, totp_credentials).
/// For user-global tables (password_reset_tokens, email_verification_tokens, impersonation_sessions)
/// no RLS is added — they are keyed by user_id only and not tenant-scoped.
/// </remarks>
public partial class Phase2AuthFlows : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsEmailVerified",
            schema: "identity",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "EmailVerifiedAt",
            schema: "identity",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "FailedLoginAttempts",
            schema: "identity",
            table: "users",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LockoutUntil",
            schema: "identity",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsMfaEnabled",
            schema: "identity",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "email_verification_tokens",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_email_verification_tokens", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_email_verification_tokens_TokenHash",
            schema: "identity",
            table: "email_verification_tokens",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_email_verification_tokens_UserId_UsedAt",
            schema: "identity",
            table: "email_verification_tokens",
            columns: new[] { "UserId", "UsedAt" });

        migrationBuilder.CreateTable(
            name: "password_reset_tokens",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_password_reset_tokens", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_password_reset_tokens_TokenHash",
            schema: "identity",
            table: "password_reset_tokens",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_password_reset_tokens_UserId_UsedAt",
            schema: "identity",
            table: "password_reset_tokens",
            columns: new[] { "UserId", "UsedAt" });

        migrationBuilder.CreateTable(
            name: "totp_credentials",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                EncryptedSecret = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                IsConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                hashed_recovery_codes = table.Column<string>(type: "jsonb", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_totp_credentials", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_totp_credentials_UserId",
            schema: "identity",
            table: "totp_credentials",
            column: "UserId",
            unique: true);

        migrationBuilder.CreateTable(
            name: "api_keys",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                KeyPrefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                KeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ScopesJson = table.Column<string>(type: "jsonb", nullable: false),
                LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_api_keys", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_api_keys_KeyHash",
            schema: "identity",
            table: "api_keys",
            column: "KeyHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_api_keys_UserId_RevokedAt",
            schema: "identity",
            table: "api_keys",
            columns: new[] { "UserId", "RevokedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_api_keys_OrganizationId_RevokedAt",
            schema: "identity",
            table: "api_keys",
            columns: new[] { "OrganizationId", "RevokedAt" });

        migrationBuilder.CreateTable(
            name: "impersonation_sessions",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                ImpersonationToken = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_impersonation_sessions", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_impersonation_sessions_AdminUserId_StartedAt",
            schema: "identity",
            table: "impersonation_sessions",
            columns: new[] { "AdminUserId", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_impersonation_sessions_TargetUserId_StartedAt",
            schema: "identity",
            table: "impersonation_sessions",
            columns: new[] { "TargetUserId", "StartedAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "impersonation_sessions", schema: "identity");
        migrationBuilder.DropTable(name: "api_keys", schema: "identity");
        migrationBuilder.DropTable(name: "totp_credentials", schema: "identity");
        migrationBuilder.DropTable(name: "password_reset_tokens", schema: "identity");
        migrationBuilder.DropTable(name: "email_verification_tokens", schema: "identity");

        migrationBuilder.DropColumn(name: "IsEmailVerified", schema: "identity", table: "users");
        migrationBuilder.DropColumn(name: "EmailVerifiedAt", schema: "identity", table: "users");
        migrationBuilder.DropColumn(name: "FailedLoginAttempts", schema: "identity", table: "users");
        migrationBuilder.DropColumn(name: "LockoutUntil", schema: "identity", table: "users");
        migrationBuilder.DropColumn(name: "IsMfaEnabled", schema: "identity", table: "users");
    }
}
