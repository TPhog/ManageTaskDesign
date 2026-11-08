using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreelanceFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImageStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "watermark_enabled",
                table: "settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "watermark_opacity",
                table: "settings",
                type: "integer",
                nullable: false,
                defaultValue: 18);

            migrationBuilder.AddColumn<string>(
                name: "watermark_text",
                table: "settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "height",
                table: "files",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "original_bytes",
                table: "files",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "files",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_type",
                table: "files",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "preview_bytes",
                table: "files",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "thumb_bytes",
                table: "files",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "width",
                table: "files",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "storage_alerts",
                columns: table => new
                {
                    threshold = table.Column<int>(type: "integer", nullable: false),
                    notified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cleared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_alerts", x => x.threshold);
                    table.CheckConstraint("ck_storage_alerts_threshold", "threshold IN (50,70,85,90,95)");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_settings_watermark_opacity",
                table: "settings",
                sql: "watermark_opacity BETWEEN 10 AND 40");

            migrationBuilder.AddCheckConstraint(
                name: "ck_settings_watermark_text",
                table: "settings",
                sql: "watermark_text IS NULL OR char_length(watermark_text) <= 100");

            migrationBuilder.CreateIndex(
                name: "ix_files_owner",
                table: "files",
                columns: new[] { "owner_type", "owner_id" },
                filter: "deleted_at IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_files_owner",
                table: "files",
                sql: "(owner_type IS NULL) = (owner_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_files_owner_type",
                table: "files",
                sql: "owner_type IS NULL OR owner_type IN ('TASK','PROGRESS_UPDATE','CLIENT_REQUEST','DELIVERABLE')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "storage_alerts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_settings_watermark_opacity",
                table: "settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_settings_watermark_text",
                table: "settings");

            migrationBuilder.DropIndex(
                name: "ix_files_owner",
                table: "files");

            migrationBuilder.DropCheckConstraint(
                name: "ck_files_owner",
                table: "files");

            migrationBuilder.DropCheckConstraint(
                name: "ck_files_owner_type",
                table: "files");

            migrationBuilder.DropColumn(
                name: "watermark_enabled",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "watermark_opacity",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "watermark_text",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "height",
                table: "files");

            migrationBuilder.DropColumn(
                name: "original_bytes",
                table: "files");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "files");

            migrationBuilder.DropColumn(
                name: "owner_type",
                table: "files");

            migrationBuilder.DropColumn(
                name: "preview_bytes",
                table: "files");

            migrationBuilder.DropColumn(
                name: "thumb_bytes",
                table: "files");

            migrationBuilder.DropColumn(
                name: "width",
                table: "files");
        }
    }
}
