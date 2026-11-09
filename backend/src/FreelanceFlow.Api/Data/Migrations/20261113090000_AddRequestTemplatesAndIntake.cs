using System;
using FreelanceFlow.Api.Domain.RequestTemplates;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreelanceFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestTemplatesAndIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_files_owner_type",
                table: "files");

            migrationBuilder.AddColumn<bool>(
                name: "intake_enabled",
                table: "settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "intake_slug",
                table: "settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "answers",
                table: "client_requests",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<Guid>(
                name: "template_id",
                table: "client_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "template_name",
                table: "client_requests",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "request_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    design_type = table.Column<string>(type: "text", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fields = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_templates", x => x.id);
                    table.CheckConstraint("ck_request_templates_description", "char_length(description) <= 500");
                    table.CheckConstraint("ck_request_templates_design_type", "design_type IN ('LOGO','BANNER','POSTER','BRANDING','UI_UX','SOCIAL_MEDIA','PACKAGING','PRINT','OTHER')");
                    table.CheckConstraint("ck_request_templates_fields", "jsonb_typeof(fields) = 'array' AND jsonb_array_length(fields) <= 30");
                    table.CheckConstraint("ck_request_templates_name", "char_length(name) BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_request_templates_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "intake_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    template_name = table.Column<string>(type: "text", nullable: true),
                    contact_name = table.Column<string>(type: "text", nullable: false),
                    contact_phone = table.Column<string>(type: "text", nullable: false),
                    contact_email = table.Column<string>(type: "text", nullable: true),
                    answers = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    note = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "NEW"),
                    reject_reason = table.Column<string>(type: "text", nullable: true),
                    converted_client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    converted_project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    converted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intake_submissions", x => x.id);
                    table.CheckConstraint("ck_intake_answers", "jsonb_typeof(answers) = 'array'");
                    table.CheckConstraint("ck_intake_body", "char_length(body) BETWEEN 1 AND 10000");
                    table.CheckConstraint("ck_intake_contact_email", "contact_email IS NULL OR char_length(contact_email) <= 200");
                    table.CheckConstraint("ck_intake_contact_name", "char_length(contact_name) BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_intake_contact_phone", "char_length(contact_phone) BETWEEN 1 AND 20");
                    table.CheckConstraint("ck_intake_converted", "(status = 'CONVERTED') = (converted_client_id IS NOT NULL AND converted_project_id IS NOT NULL AND converted_at IS NOT NULL)");
                    table.CheckConstraint("ck_intake_note", "char_length(note) <= 2000");
                    table.CheckConstraint("ck_intake_reject_reason", "reject_reason IS NULL OR char_length(reject_reason) <= 500");
                    table.CheckConstraint("ck_intake_rejected", "(status = 'REJECTED') = (rejected_at IS NOT NULL)");
                    table.CheckConstraint("ck_intake_status", "status IN ('NEW','CONVERTED','REJECTED')");
                    table.ForeignKey(
                        name: "FK_intake_submissions_clients_converted_client_id",
                        column: x => x.converted_client_id,
                        principalTable: "clients",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_intake_submissions_projects_converted_project_id",
                        column: x => x.converted_project_id,
                        principalTable: "projects",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_intake_submissions_request_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "request_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ux_settings_intake_slug",
                table: "settings",
                column: "intake_slug",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_settings_intake_enabled",
                table: "settings",
                sql: "NOT intake_enabled OR intake_slug IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_settings_intake_slug",
                table: "settings",
                sql: "intake_slug IS NULL OR intake_slug ~ '^[A-Za-z0-9_-]{22}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_files_image_project",
                table: "files",
                sql: "owner_type IS NULL OR owner_type = 'INTAKE_SUBMISSION' OR project_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_files_intake_request",
                table: "files",
                sql: "owner_type IS DISTINCT FROM 'INTAKE_SUBMISSION' OR request_id IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_files_owner_type",
                table: "files",
                sql: "owner_type IS NULL OR owner_type IN ('TASK','PROGRESS_UPDATE','CLIENT_REQUEST','DELIVERABLE','INTAKE_SUBMISSION')");

            migrationBuilder.CreateIndex(
                name: "IX_client_requests_template_id",
                table: "client_requests",
                column: "template_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_requests_answers",
                table: "client_requests",
                sql: "jsonb_typeof(answers) = 'array'");

            migrationBuilder.CreateIndex(
                name: "ix_intake_status",
                table: "intake_submissions",
                columns: new[] { "status", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_intake_submissions_converted_client_id",
                table: "intake_submissions",
                column: "converted_client_id");

            migrationBuilder.CreateIndex(
                name: "IX_intake_submissions_converted_project_id",
                table: "intake_submissions",
                column: "converted_project_id");

            migrationBuilder.CreateIndex(
                name: "IX_intake_submissions_template_id",
                table: "intake_submissions",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_templates_order",
                table: "request_templates",
                columns: new[] { "sort_order", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_request_templates_service_id",
                table: "request_templates",
                column: "service_id");

            migrationBuilder.AddForeignKey(
                name: "FK_client_requests_request_templates_template_id",
                table: "client_requests",
                column: "template_id",
                principalTable: "request_templates",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Plan 13 decision #1: the six presets, seeded once; the designer edits/deletes them afterwards.
            migrationBuilder.Sql(RequestTemplatePresets.SeedSql());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_client_requests_request_templates_template_id",
                table: "client_requests");

            migrationBuilder.DropTable(
                name: "intake_submissions");

            migrationBuilder.DropTable(
                name: "request_templates");

            migrationBuilder.DropIndex(
                name: "ux_settings_intake_slug",
                table: "settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_settings_intake_enabled",
                table: "settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_settings_intake_slug",
                table: "settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_files_image_project",
                table: "files");

            migrationBuilder.DropCheckConstraint(
                name: "ck_files_intake_request",
                table: "files");

            migrationBuilder.DropCheckConstraint(
                name: "ck_files_owner_type",
                table: "files");

            migrationBuilder.DropIndex(
                name: "IX_client_requests_template_id",
                table: "client_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_requests_answers",
                table: "client_requests");

            migrationBuilder.DropColumn(
                name: "intake_enabled",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "intake_slug",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "answers",
                table: "client_requests");

            migrationBuilder.DropColumn(
                name: "template_id",
                table: "client_requests");

            migrationBuilder.DropColumn(
                name: "template_name",
                table: "client_requests");

            migrationBuilder.AddCheckConstraint(
                name: "ck_files_owner_type",
                table: "files",
                sql: "owner_type IS NULL OR owner_type IN ('TASK','PROGRESS_UPDATE','CLIENT_REQUEST','DELIVERABLE')");
        }
    }
}
