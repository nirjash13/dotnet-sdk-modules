using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Registration.Infrastructure.Persistence.Migrations;

/// <summary>
/// Initial migration for the <c>registration</c> schema.
/// Creates the <c>registration_saga_state</c> table that backs the MassTransit saga repository.
/// </summary>
/// <remarks>
/// This migration mirrors <c>migrations/registration/001_initial_registration.sql</c>.
/// The SQL file is the canonical apply-script for production deployments;
/// this class enables <c>dotnet ef</c> tooling (migrations list / database update).
/// </remarks>
#pragma warning disable SA1601 // Partial elements should be documented — generated migration pattern
public sealed partial class InitialRegistration : Migration
#pragma warning restore SA1601
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.EnsureSchema(name: "registration");

        migrationBuilder.CreateTable(
            name: "registration_saga_state",
            schema: "registration",
            columns: table => new
            {
                correlation_id = table.Column<Guid>(nullable: false),
                current_state = table.Column<string>(maxLength: 64, nullable: false, defaultValue: "Initial"),
                tenant_id = table.Column<Guid>(nullable: false),
                association_name = table.Column<string>(maxLength: 256, nullable: false),
                primary_user_email = table.Column<string>(maxLength: 256, nullable: false),
                currency = table.Column<string>(maxLength: 3, nullable: false),
                user_id = table.Column<Guid>(nullable: true),
                account_id = table.Column<Guid>(nullable: true),
                reporting_id = table.Column<Guid>(nullable: true),
                started_at = table.Column<DateTimeOffset>(nullable: false),
                completed_at = table.Column<DateTimeOffset>(nullable: true),
                failure_reason = table.Column<string>(maxLength: 2048, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_registration_saga_state", x => x.correlation_id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_registration_saga_state_current_state",
            schema: "registration",
            table: "registration_saga_state",
            column: "current_state");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(name: "registration_saga_state", schema: "registration");
        migrationBuilder.DropSchema(name: "registration");
    }
}
