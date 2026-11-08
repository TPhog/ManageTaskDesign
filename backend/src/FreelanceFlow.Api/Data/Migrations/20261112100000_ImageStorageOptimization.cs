using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreelanceFlow.Api.Data.Migrations
{
    /// <summary>P10: client photos stored once (preview_is_original), discarded originals (original_deleted_at), duplicate-warning index. Additive; existing rows keep their three objects.</summary>
    public partial class ImageStorageOptimization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "original_deleted_at",
                table: "files",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "preview_is_original",
                table: "files",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_files_project_sha256",
                table: "files",
                columns: new[] { "project_id", "sha256" },
                filter: "deleted_at IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_files_preview_alias",
                table: "files",
                sql: "NOT preview_is_original OR (uploaded_by = 'CLIENT' AND original_deleted_at IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_files_project_sha256",
                table: "files");

            migrationBuilder.DropCheckConstraint(
                name: "ck_files_preview_alias",
                table: "files");

            migrationBuilder.DropColumn(
                name: "original_deleted_at",
                table: "files");

            migrationBuilder.DropColumn(
                name: "preview_is_original",
                table: "files");
        }
    }
}
