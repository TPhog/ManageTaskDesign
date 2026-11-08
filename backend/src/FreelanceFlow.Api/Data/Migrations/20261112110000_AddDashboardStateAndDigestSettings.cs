using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreelanceFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardStateAndDigestSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "digest_enabled",
                table: "settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "digest_hour_vn",
                table: "settings",
                type: "integer",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.CreateTable(
                name: "dashboard_state",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false),
                    digest_last_run_date = table.Column<DateOnly>(type: "date", nullable: true),
                    setup_dismissed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dashboard_state", x => x.id);
                    table.CheckConstraint("ck_dashboard_state_singleton", "id = 1");
                });

            migrationBuilder.Sql("INSERT INTO dashboard_state (id) VALUES (1) ON CONFLICT (id) DO NOTHING;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_settings_digest_hour",
                table: "settings",
                sql: "digest_hour_vn BETWEEN 0 AND 23");

            migrationBuilder.CreateIndex(
                name: "ix_activity_occurred",
                table: "activity_logs",
                column: "occurred_at",
                descending: new[] { true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dashboard_state");

            migrationBuilder.DropCheckConstraint(
                name: "ck_settings_digest_hour",
                table: "settings");

            migrationBuilder.DropIndex(
                name: "ix_activity_occurred",
                table: "activity_logs");

            migrationBuilder.DropColumn(
                name: "digest_enabled",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "digest_hour_vn",
                table: "settings");
        }
    }
}
