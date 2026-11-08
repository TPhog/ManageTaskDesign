using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreelanceFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRevisionItemServiceAndCreatedDeliverable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "created_deliverable_id",
                table: "revision_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "service_id",
                table: "revision_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_revision_items_service_id",
                table: "revision_items",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ux_items_created_deliverable",
                table: "revision_items",
                column: "created_deliverable_id",
                unique: true,
                filter: "created_deliverable_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_items_created_deliverable_type",
                table: "revision_items",
                sql: "created_deliverable_id IS NULL OR type = 'NEW_DELIVERABLE'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_items_service_type",
                table: "revision_items",
                sql: "service_id IS NULL OR type = 'NEW_DELIVERABLE'");

            migrationBuilder.AddForeignKey(
                name: "FK_revision_items_deliverables_created_deliverable_id",
                table: "revision_items",
                column: "created_deliverable_id",
                principalTable: "deliverables",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_revision_items_services_service_id",
                table: "revision_items",
                column: "service_id",
                principalTable: "services",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_revision_items_deliverables_created_deliverable_id",
                table: "revision_items");

            migrationBuilder.DropForeignKey(
                name: "FK_revision_items_services_service_id",
                table: "revision_items");

            migrationBuilder.DropIndex(
                name: "IX_revision_items_service_id",
                table: "revision_items");

            migrationBuilder.DropIndex(
                name: "ux_items_created_deliverable",
                table: "revision_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_items_created_deliverable_type",
                table: "revision_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_items_service_type",
                table: "revision_items");

            migrationBuilder.DropColumn(
                name: "created_deliverable_id",
                table: "revision_items");

            migrationBuilder.DropColumn(
                name: "service_id",
                table: "revision_items");
        }
    }
}
