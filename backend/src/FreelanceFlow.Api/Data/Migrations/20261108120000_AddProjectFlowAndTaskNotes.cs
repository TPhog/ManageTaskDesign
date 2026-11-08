using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreelanceFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectFlowAndTaskNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "task_checklist_items",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "task_template",
                table: "services",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "client_decision",
                table: "deliverables",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "client_decision_at",
                table: "deliverables",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "task_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_notes", x => x.id);
                    table.CheckConstraint("ck_task_notes_body", "char_length(body) BETWEEN 1 AND 5000");
                    table.ForeignKey(
                        name: "FK_task_notes_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_checklist_description",
                table: "task_checklist_items",
                sql: "char_length(description) <= 5000");

            migrationBuilder.AddCheckConstraint(
                name: "ck_services_task_template",
                table: "services",
                sql: "jsonb_typeof(task_template) = 'array' AND jsonb_array_length(task_template) <= 20");

            migrationBuilder.AddCheckConstraint(
                name: "ck_deliverables_client_decision",
                table: "deliverables",
                sql: "client_decision IN ('APPROVED')");

            migrationBuilder.CreateIndex(
                name: "ix_task_notes_task",
                table: "task_notes",
                columns: new[] { "task_id", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_notes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_checklist_description",
                table: "task_checklist_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_services_task_template",
                table: "services");

            migrationBuilder.DropCheckConstraint(
                name: "ck_deliverables_client_decision",
                table: "deliverables");

            migrationBuilder.DropColumn(
                name: "description",
                table: "task_checklist_items");

            migrationBuilder.DropColumn(
                name: "task_template",
                table: "services");

            migrationBuilder.DropColumn(
                name: "client_decision",
                table: "deliverables");

            migrationBuilder.DropColumn(
                name: "client_decision_at",
                table: "deliverables");
        }
    }
}
