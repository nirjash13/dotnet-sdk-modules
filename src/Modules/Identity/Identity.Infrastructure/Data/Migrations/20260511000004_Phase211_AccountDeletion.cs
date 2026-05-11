using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Data.Migrations;

/// <summary>
/// Migration 20260511000004 — Phase 2.11: Account deletion with 30-day grace period.
/// Adds deletion columns to <c>users</c>, <c>account_restore_tokens</c>, and <c>user_tombstones</c> tables.
/// </summary>
public partial class Phase211AccountDeletion : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Soft-delete columns on users ─────────────────────────────────────
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletedAt",
            schema: "identity",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeletionScheduledFor",
            schema: "identity",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_users_DeletionScheduledFor",
            schema: "identity",
            table: "users",
            column: "DeletionScheduledFor",
            filter: "\"DeletionScheduledFor\" IS NOT NULL");

        // ── Account restore tokens ────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "account_restore_tokens",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_account_restore_tokens", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_account_restore_tokens_TokenHash",
            schema: "identity",
            table: "account_restore_tokens",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_account_restore_tokens_UserId_UsedAt",
            schema: "identity",
            table: "account_restore_tokens",
            columns: new[] { "UserId", "UsedAt" });

        // ── User tombstones ───────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "user_tombstones",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OriginalUserId = table.Column<Guid>(type: "uuid", nullable: false),
                DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                HardDeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_user_tombstones", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_user_tombstones_OriginalUserId",
            schema: "identity",
            table: "user_tombstones",
            column: "OriginalUserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "user_tombstones", schema: "identity");
        migrationBuilder.DropTable(name: "account_restore_tokens", schema: "identity");

        migrationBuilder.DropIndex(
            name: "IX_users_DeletionScheduledFor",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(name: "DeletedAt", schema: "identity", table: "users");
        migrationBuilder.DropColumn(name: "DeletionScheduledFor", schema: "identity", table: "users");
    }
}
