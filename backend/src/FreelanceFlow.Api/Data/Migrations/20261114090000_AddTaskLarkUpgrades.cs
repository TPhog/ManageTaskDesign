using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreelanceFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskLarkUpgrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "blocked_by_task_id",
                table: "tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "blocked_reason",
                table: "tasks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "links",
                table: "tasks",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "recurrence_kind",
                table: "tasks",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "recurrence_month_day",
                table: "tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recurrence_source_id",
                table: "tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "recurrence_spawned",
                table: "tasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "recurrence_until",
                table: "tasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "recurrence_weekdays",
                table: "tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "links",
                table: "deliverables",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.CreateTable(
                name: "task_view_state",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_view_state", x => x.id);
                    table.CheckConstraint("ck_task_view_state_config", "jsonb_typeof(config) = 'object'");
                    table.CheckConstraint("ck_task_view_state_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "task_views",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_views", x => x.id);
                    table.CheckConstraint("ck_task_views_config", "jsonb_typeof(config) = 'object'");
                    table.CheckConstraint("ck_task_views_name", "char_length(name) BETWEEN 1 AND 60");
                });

            // Saved view names are unique ignoring case; EF cannot model an expression index (TaskViewConfiguration)
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_task_views_name ON task_views (lower(name));");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_blocked_by",
                table: "tasks",
                column: "blocked_by_task_id",
                filter: "blocked_by_task_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_recurrence_source",
                table: "tasks",
                column: "recurrence_source_id",
                unique: true,
                filter: "recurrence_source_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tasks_blocked_status",
                table: "tasks",
                sql: "(blocked_reason IS NULL AND blocked_by_task_id IS NULL) OR status = 'BLOCKED'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tasks_links",
                table: "tasks",
                sql: "jsonb_typeof(links) = 'array' AND jsonb_array_length(links) <= 10");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tasks_recurrence_kind",
                table: "tasks",
                sql: "recurrence_kind IS NULL OR recurrence_kind IN ('DAILY','WEEKLY','MONTHLY')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tasks_recurrence_rule",
                table: "tasks",
                sql: "recurrence_kind IS NULL OR recurrence_kind = 'DAILY' OR (recurrence_kind = 'WEEKLY' AND COALESCE(recurrence_weekdays, 0) BETWEEN 1 AND 127) OR (recurrence_kind = 'MONTHLY' AND COALESCE(recurrence_month_day, 0) BETWEEN 1 AND 31)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_deliverables_links",
                table: "deliverables",
                sql: "jsonb_typeof(links) = 'array' AND jsonb_array_length(links) <= 10");

            migrationBuilder.AddForeignKey(
                name: "FK_tasks_tasks_blocked_by_task_id",
                table: "tasks",
                column: "blocked_by_task_id",
                principalTable: "tasks",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tasks_tasks_recurrence_source_id",
                table: "tasks",
                column: "recurrence_source_id",
                principalTable: "tasks",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tasks_tasks_blocked_by_task_id",
                table: "tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_tasks_tasks_recurrence_source_id",
                table: "tasks");

            migrationBuilder.DropTable(
                name: "task_view_state");

            migrationBuilder.DropTable(
                name: "task_views");

            migrationBuilder.DropIndex(
                name: "ix_tasks_blocked_by",
                table: "tasks");

            migrationBuilder.DropIndex(
                name: "ix_tasks_recurrence_source",
                table: "tasks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tasks_blocked_status",
                table: "tasks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tasks_links",
                table: "tasks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tasks_recurrence_kind",
                table: "tasks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tasks_recurrence_rule",
                table: "tasks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_deliverables_links",
                table: "deliverables");

            migrationBuilder.DropColumn(
                name: "blocked_by_task_id",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "blocked_reason",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "links",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "recurrence_kind",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "recurrence_month_day",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "recurrence_source_id",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "recurrence_spawned",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "recurrence_until",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "recurrence_weekdays",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "links",
                table: "deliverables");
        }
    }
}
