using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FreelanceFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "banks",
                columns: table => new
                {
                    bin = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    short_name = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_banks", x => x.bin);
                });

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    company = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    contact_channel = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "ACTIVE"),
                    portal_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    portal_token_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    portal_token_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    portal_token_created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.id);
                    table.CheckConstraint("ck_clients_status", "status IN ('ACTIVE','INACTIVE')");
                });

            migrationBuilder.CreateTable(
                name: "designer_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    security_stamp = table.Column<string>(type: "text", nullable: false),
                    failed_logins = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lockout_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_designer_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quotation_sequences",
                columns: table => new
                {
                    year = table.Column<int>(type: "integer", nullable: false),
                    last_seq = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotation_sequences", x => x.year);
                });

            migrationBuilder.CreateTable(
                name: "services",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    base_price = table.Column<long>(type: "bigint", nullable: false),
                    default_included_rounds = table.Column<int>(type: "integer", nullable: false),
                    extra_round_price = table.Column<long>(type: "bigint", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_services", x => x.id);
                    table.CheckConstraint("ck_services_base_price", "base_price >= 0");
                    table.CheckConstraint("ck_services_extra_price", "extra_round_price >= 0");
                    table.CheckConstraint("ck_services_rounds", "default_included_rounds BETWEEN 0 AND 50");
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    design_type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "DRAFT"),
                    status_reason = table.Column<string>(type: "text", nullable: true),
                    status_reason_public = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    internal_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                    table.CheckConstraint("ck_projects_dates", "due_date IS NULL OR start_date IS NULL OR due_date >= start_date");
                    table.CheckConstraint("ck_projects_design_type", "design_type IN ('LOGO','BANNER','POSTER','BRANDING','UI_UX','SOCIAL_MEDIA','PACKAGING','PRINT','OTHER')");
                    table.CheckConstraint("ck_projects_status", "status IN ('DRAFT','QUOTED','IN_PROGRESS','ON_HOLD','COMPLETED','CANCELLED')");
                    table.ForeignKey(
                        name: "FK_projects_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activity_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    actor_type = table.Column<string>(type: "text", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'"),
                    visible_to_client = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    client_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_logs", x => x.id);
                    table.CheckConstraint("ck_activity_actor_type", "actor_type IN ('DESIGNER','CLIENT','SYSTEM')");
                    table.ForeignKey(
                        name: "FK_activity_logs_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_activity_logs_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "deliverables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    included_rounds = table.Column<int>(type: "integer", nullable: false),
                    extra_round_price = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "NOT_STARTED"),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deliverables", x => x.id);
                    table.CheckConstraint("ck_deliverables_extra_price", "extra_round_price >= 0");
                    table.CheckConstraint("ck_deliverables_quantity", "quantity >= 1");
                    table.CheckConstraint("ck_deliverables_rounds", "included_rounds BETWEEN 0 AND 50");
                    table.CheckConstraint("ck_deliverables_status", "status IN ('NOT_STARTED','IN_PROGRESS','IN_REVIEW','APPROVED','DELIVERED','CANCELLED')");
                    table.ForeignKey(
                        name: "FK_deliverables_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_deliverables_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "project_briefs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "DRAFT"),
                    content = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_briefs", x => x.id);
                    table.CheckConstraint("ck_briefs_confirmed", "(status = 'CONFIRMED') = (confirmed_at IS NOT NULL)");
                    table.CheckConstraint("ck_briefs_status", "status IN ('DRAFT','CONFIRMED')");
                    table.CheckConstraint("ck_briefs_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_project_briefs_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deliverable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "NEW"),
                    decline_reason = table.Column<string>(type: "text", nullable: true),
                    triaged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_requests", x => x.id);
                    table.CheckConstraint("ck_requests_content", "length(content) BETWEEN 1 AND 5000");
                    table.CheckConstraint("ck_requests_created_by", "created_by IN ('CLIENT','DESIGNER')");
                    table.CheckConstraint("ck_requests_decline", "status <> 'DECLINED' OR decline_reason IS NOT NULL");
                    table.CheckConstraint("ck_requests_portal", "created_by <> 'CLIENT' OR source = 'PORTAL'");
                    table.CheckConstraint("ck_requests_source", "source IN ('PORTAL','ZALO','EMAIL','PHONE','OTHER')");
                    table.CheckConstraint("ck_requests_status", "status IN ('NEW','TRIAGED','DECLINED')");
                    table.ForeignKey(
                        name: "FK_client_requests_deliverables_deliverable_id",
                        column: x => x.deliverable_id,
                        principalTable: "deliverables",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_client_requests_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deliverable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    original_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "text", nullable: false),
                    sha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    visible_to_client = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    uploaded_by = table.Column<string>(type: "text", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_files", x => x.id);
                    table.CheckConstraint("ck_files_kind", "kind IN ('REFERENCE','PREVIEW','FINAL','EVIDENCE','BRIEF','SYSTEM')");
                    table.CheckConstraint("ck_files_size", "size_bytes > 0");
                    table.CheckConstraint("ck_files_uploaded_by", "uploaded_by IN ('DESIGNER','CLIENT')");
                    table.ForeignKey(
                        name: "FK_files_client_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "client_requests",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_files_deliverables_deliverable_id",
                        column: x => x.deliverable_id,
                        principalTable: "deliverables",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_files_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "revision_rounds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deliverable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    round_no = table.Column<int>(type: "integer", nullable: false),
                    source_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    received_at = table.Column<DateOnly>(type: "date", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revision_rounds", x => x.id);
                    table.CheckConstraint("ck_rounds_no", "round_no >= 1");
                    table.ForeignKey(
                        name: "FK_revision_rounds_client_requests_source_request_id",
                        column: x => x.source_request_id,
                        principalTable: "client_requests",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_revision_rounds_deliverables_deliverable_id",
                        column: x => x.deliverable_id,
                        principalTable: "deliverables",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_revision_rounds_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quotations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    number = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    previous_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "DRAFT"),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    validity_days = table.Column<int>(type: "integer", nullable: false),
                    subtotal = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    discount_amount = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    vat_percent = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    vat_amount = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    total = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    deposit_percent = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    deposit_amount = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    terms = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    notes_to_client = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    internal_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_at = table.Column<DateOnly>(type: "date", nullable: true),
                    accepted_note = table.Column<string>(type: "text", nullable: true),
                    evidence_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_at = table.Column<DateOnly>(type: "date", nullable: true),
                    rejected_reason = table.Column<string>(type: "text", nullable: true),
                    payment_status = table.Column<string>(type: "text", nullable: false, defaultValue: "UNPAID"),
                    deposit_paid_at = table.Column<DateOnly>(type: "date", nullable: true),
                    paid_at = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotations", x => x.id);
                    table.CheckConstraint("ck_quotations_accepted", "status <> 'ACCEPTED' OR accepted_at IS NOT NULL");
                    table.CheckConstraint("ck_quotations_deposit_amount", "deposit_amount >= 0");
                    table.CheckConstraint("ck_quotations_deposit_paid", "payment_status <> 'DEPOSIT_PAID' OR deposit_percent > 0");
                    table.CheckConstraint("ck_quotations_deposit_percent", "deposit_percent BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_quotations_discount", "discount_amount >= 0 AND discount_amount <= subtotal");
                    table.CheckConstraint("ck_quotations_expiry", "expiry_date IS NULL OR expiry_date >= issue_date");
                    table.CheckConstraint("ck_quotations_kind", "kind IN ('INITIAL','ADDITIONAL')");
                    table.CheckConstraint("ck_quotations_payment_accepted", "payment_status = 'UNPAID' OR status = 'ACCEPTED'");
                    table.CheckConstraint("ck_quotations_payment_status", "payment_status IN ('UNPAID','DEPOSIT_PAID','PAID')");
                    table.CheckConstraint("ck_quotations_rejected", "status <> 'REJECTED' OR rejected_reason IS NOT NULL");
                    table.CheckConstraint("ck_quotations_status", "status IN ('DRAFT','SENT','ACCEPTED','REJECTED','SUPERSEDED')");
                    table.CheckConstraint("ck_quotations_subtotal", "subtotal >= 0");
                    table.CheckConstraint("ck_quotations_total", "total >= 0");
                    table.CheckConstraint("ck_quotations_validity", "validity_days BETWEEN 1 AND 365");
                    table.CheckConstraint("ck_quotations_vat_amount", "vat_amount >= 0");
                    table.CheckConstraint("ck_quotations_vat_percent", "vat_percent BETWEEN 0 AND 20");
                    table.CheckConstraint("ck_quotations_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_quotations_files_evidence_file_id",
                        column: x => x.evidence_file_id,
                        principalTable: "files",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_quotations_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_quotations_quotations_previous_version_id",
                        column: x => x.previous_version_id,
                        principalTable: "quotations",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    contact_email = table.Column<string>(type: "text", nullable: true),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    logo_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bank_bin = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    bank_account_no = table.Column<string>(type: "text", nullable: true),
                    bank_account_name = table.Column<string>(type: "text", nullable: true),
                    quotation_prefix = table.Column<string>(type: "text", nullable: false, defaultValue: "BG"),
                    default_validity_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 7),
                    default_deposit_percent = table.Column<int>(type: "integer", nullable: false, defaultValue: 50),
                    default_terms = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.id);
                    table.CheckConstraint("ck_settings_account_no", "bank_account_no ~ '^[0-9A-Za-z]{4,19}$' OR bank_account_no IS NULL");
                    table.CheckConstraint("ck_settings_deposit", "default_deposit_percent BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_settings_prefix", "quotation_prefix ~ '^[A-Z]{1,5}$'");
                    table.CheckConstraint("ck_settings_singleton", "id = 1");
                    table.CheckConstraint("ck_settings_validity", "default_validity_days BETWEEN 1 AND 365");
                    table.ForeignKey(
                        name: "FK_settings_banks_bank_bin",
                        column: x => x.bank_bin,
                        principalTable: "banks",
                        principalColumn: "bin");
                    table.ForeignKey(
                        name: "FK_settings_files_logo_file_id",
                        column: x => x.logo_file_id,
                        principalTable: "files",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "revision_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    billing = table.Column<string>(type: "text", nullable: false),
                    work_status = table.Column<string>(type: "text", nullable: false, defaultValue: "TODO"),
                    quotation_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope_override_reason = table.Column<string>(type: "text", nullable: true),
                    internal_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revision_items", x => x.id);
                    table.CheckConstraint("ck_items_billing", "billing IN ('FREE_FIX','IN_QUOTA','CHARGEABLE')");
                    table.CheckConstraint("ck_items_description", "length(description) BETWEEN 1 AND 2000");
                    table.CheckConstraint("ck_items_quotation", "billing = 'CHARGEABLE' OR quotation_item_id IS NULL");
                    table.CheckConstraint("ck_items_type", "type IN ('MINOR','MAJOR','SCOPE_CHANGE','NEW_DELIVERABLE')");
                    table.CheckConstraint("ck_items_work_status", "work_status IN ('TODO','IN_PROGRESS','DONE','CANCELLED')");
                    table.ForeignKey(
                        name: "FK_revision_items_revision_rounds_round_id",
                        column: x => x.round_id,
                        principalTable: "revision_rounds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quotation_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quotation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    details = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    source_type = table.Column<string>(type: "text", nullable: false, defaultValue: "MANUAL"),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    copied_from_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotation_items", x => x.id);
                    table.CheckConstraint("ck_qitems_amount", "amount = quantity * unit_price");
                    table.CheckConstraint("ck_qitems_description", "length(description) BETWEEN 1 AND 500");
                    table.CheckConstraint("ck_qitems_quantity", "quantity >= 1");
                    table.CheckConstraint("ck_qitems_source_type", "source_type IN ('MANUAL','SERVICE','DELIVERABLE','REVISION')");
                    table.CheckConstraint("ck_qitems_unit_price", "unit_price >= 0");
                    table.ForeignKey(
                        name: "FK_quotation_items_quotation_items_copied_from_item_id",
                        column: x => x.copied_from_item_id,
                        principalTable: "quotation_items",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_quotation_items_quotations_quotation_id",
                        column: x => x.quotation_id,
                        principalTable: "quotations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deliverable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revision_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "TODO"),
                    priority = table.Column<string>(type: "text", nullable: false, defaultValue: "MEDIUM"),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    sort_order = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.0),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.id);
                    table.CheckConstraint("ck_tasks_priority", "priority IN ('LOW','MEDIUM','HIGH','URGENT')");
                    table.CheckConstraint("ck_tasks_status", "status IN ('TODO','IN_PROGRESS','REVIEW','DONE','BLOCKED')");
                    table.ForeignKey(
                        name: "FK_tasks_deliverables_deliverable_id",
                        column: x => x.deliverable_id,
                        principalTable: "deliverables",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_tasks_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tasks_revision_items_revision_item_id",
                        column: x => x.revision_item_id,
                        principalTable: "revision_items",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "task_checklist_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    done = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_checklist_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_checklist_items_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activity_client",
                table: "activity_logs",
                columns: new[] { "client_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_activity_entity",
                table: "activity_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_activity_project",
                table: "activity_logs",
                columns: new[] { "project_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_client_requests_deliverable_id",
                table: "client_requests",
                column: "deliverable_id");

            migrationBuilder.CreateIndex(
                name: "ix_requests_project",
                table: "client_requests",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_requests_status",
                table: "client_requests",
                columns: new[] { "status", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_deliverables_project",
                table: "deliverables",
                columns: new[] { "project_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_deliverables_service_id",
                table: "deliverables",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "IX_designer_accounts_email",
                table: "designer_accounts",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_files_deliverable_id",
                table: "files",
                column: "deliverable_id");

            migrationBuilder.CreateIndex(
                name: "ix_files_project",
                table: "files",
                columns: new[] { "project_id", "created_at" },
                descending: new[] { false, true },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_files_request",
                table: "files",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_files_storage_key",
                table: "files",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_briefs_project_id_version",
                table: "project_briefs",
                columns: new[] { "project_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_client",
                table: "projects",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_projects_status_due",
                table: "projects",
                columns: new[] { "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "IX_quotation_items_copied_from_item_id",
                table: "quotation_items",
                column: "copied_from_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotation_items_quotation",
                table: "quotation_items",
                columns: new[] { "quotation_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_quotations_evidence_file_id",
                table: "quotations",
                column: "evidence_file_id");

            migrationBuilder.CreateIndex(
                name: "IX_quotations_previous_version_id",
                table: "quotations",
                column: "previous_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotations_project",
                table: "quotations",
                columns: new[] { "project_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_quotations_status",
                table: "quotations",
                columns: new[] { "status", "expiry_date" });

            migrationBuilder.CreateIndex(
                name: "ux_quotations_number_version",
                table: "quotations",
                columns: new[] { "number", "version" },
                unique: true,
                filter: "number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_items_quotation_item",
                table: "revision_items",
                column: "quotation_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_items_round",
                table: "revision_items",
                columns: new[] { "round_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_revision_rounds_deliverable_id",
                table: "revision_rounds",
                column: "deliverable_id");

            migrationBuilder.CreateIndex(
                name: "IX_revision_rounds_project_id",
                table: "revision_rounds",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_revision_rounds_source_request_id",
                table: "revision_rounds",
                column: "source_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_settings_bank_bin",
                table: "settings",
                column: "bank_bin");

            migrationBuilder.CreateIndex(
                name: "IX_settings_logo_file_id",
                table: "settings",
                column: "logo_file_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_checklist_items_task_id",
                table: "task_checklist_items",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_deliverable_id",
                table: "tasks",
                column: "deliverable_id");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_project_status",
                table: "tasks",
                columns: new[] { "project_id", "status", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_revision_item_id",
                table: "tasks",
                column: "revision_item_id");

            // Circular FK: revision_items.quotation_item_id → quotation_items.id
            migrationBuilder.AddForeignKey(
                name: "FK_revision_items_quotation_items_quotation_item_id",
                table: "revision_items",
                column: "quotation_item_id",
                principalTable: "quotation_items",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Partial/filtered unique indexes and GIN search index (not expressible via EF fluent API)
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_clients_portal_token ON clients (portal_token_hash) WHERE portal_token_hash IS NOT NULL;");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_briefs_one_draft ON project_briefs (project_id) WHERE status = 'DRAFT';");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_rounds_deliverable_no ON revision_rounds (deliverable_id, round_no) WHERE deliverable_id IS NOT NULL;");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_rounds_project_no ON revision_rounds (project_id, round_no) WHERE deliverable_id IS NULL;");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_quotations_one_live ON quotations (project_id) WHERE status IN ('SENT', 'ACCEPTED');");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_quotations_one_draft_per_chain ON quotations (COALESCE(previous_version_id, id)) WHERE status = 'DRAFT';");
            migrationBuilder.Sql(
                "CREATE INDEX ix_clients_search ON clients USING gin(to_tsvector('simple', name || ' ' || COALESCE(company, '') || ' ' || COALESCE(phone, '') || ' ' || COALESCE(email, '')));");
            migrationBuilder.Sql(
                "CREATE INDEX ix_tasks_due ON tasks (due_date) WHERE status <> 'DONE';");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_services_name_active ON services (name) WHERE active;");

            // Trigger: prevent editing confirmed briefs
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_brief_immutable() RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.status <> 'DRAFT' THEN
        RAISE EXCEPTION 'brief_immutable: confirmed brief % cannot be modified', OLD.id;
    END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER trg_brief_immutable BEFORE UPDATE ON project_briefs FOR EACH ROW EXECUTE FUNCTION fn_brief_immutable();");

            // Trigger: prevent editing sent/accepted/rejected quotations
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_quotation_locked() RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.status <> 'DRAFT' THEN
        RAISE EXCEPTION 'quotation_locked: quotation % with status % cannot be modified', OLD.id, OLD.status;
    END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER trg_quotation_locked BEFORE UPDATE ON quotations FOR EACH ROW EXECUTE FUNCTION fn_quotation_locked();");

            // Trigger: prevent editing items of non-DRAFT quotations
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_quotation_items_locked() RETURNS TRIGGER LANGUAGE plpgsql AS $$
DECLARE
    q_status TEXT;
BEGIN
    SELECT status INTO q_status FROM quotations WHERE id = COALESCE(OLD.quotation_id, NEW.quotation_id);
    IF q_status <> 'DRAFT' THEN
        RAISE EXCEPTION 'quotation_items_locked: cannot modify items of quotation % (status=%)',
            COALESCE(OLD.quotation_id, NEW.quotation_id), q_status;
    END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER trg_quotation_items_locked BEFORE UPDATE OR DELETE ON quotation_items FOR EACH ROW EXECUTE FUNCTION fn_quotation_items_locked();");

            // Trigger: activity_logs is append-only
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_activity_append_only() RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'activity_append_only: activity_logs rows cannot be modified or deleted';
END; $$;
CREATE TRIGGER trg_activity_append_only BEFORE UPDATE OR DELETE ON activity_logs FOR EACH ROW EXECUTE FUNCTION fn_activity_append_only();");

            // Seed: banks (top 21 Vietnamese banks, NAPAS BINs)
            migrationBuilder.InsertData(
                table: "banks",
                columns: new[] { "bin", "code", "short_name", "name", "active" },
                values: new object[,]
                {
                    { "970436", "VCB",  "Vietcombank", "Ngân hàng TMCP Ngoại Thương Việt Nam", true },
                    { "970415", "VTB",  "VietinBank",  "Ngân hàng TMCP Công Thương Việt Nam", true },
                    { "970418", "BIDV", "BIDV",         "Ngân hàng TMCP Đầu tư và Phát triển Việt Nam", true },
                    { "970405", "AGR",  "Agribank",    "Ngân hàng Nông nghiệp và Phát triển Nông thôn Việt Nam", true },
                    { "970422", "MBB",  "MBBank",      "Ngân hàng TMCP Quân đội", true },
                    { "970407", "TCB",  "Techcombank", "Ngân hàng TMCP Kỹ Thương Việt Nam", true },
                    { "970416", "ACB",  "ACB",         "Ngân hàng TMCP Á Châu", true },
                    { "970403", "STB",  "Sacombank",   "Ngân hàng TMCP Sài Gòn Thương Tín", true },
                    { "970432", "VPB",  "VPBank",      "Ngân hàng TMCP Việt Nam Thịnh Vượng", true },
                    { "970423", "TPB",  "TPBank",      "Ngân hàng TMCP Tiên Phong", true },
                    { "970426", "MSB",  "MSB",         "Ngân hàng TMCP Hàng Hải Việt Nam", true },
                    { "970448", "OCB",  "OCB",         "Ngân hàng TMCP Phương Đông", true },
                    { "970429", "SCB",  "SCB",         "Ngân hàng TMCP Sài Gòn", true },
                    { "970431", "EIB",  "Eximbank",    "Ngân hàng TMCP Xuất Nhập Khẩu Việt Nam", true },
                    { "970443", "SHB",  "SHB",         "Ngân hàng TMCP Sài Gòn - Hà Nội", true },
                    { "970437", "HDB",  "HDBank",      "Ngân hàng TMCP Phát triển Thành phố Hồ Chí Minh", true },
                    { "970419", "NCB",  "NCB",         "Ngân hàng TMCP Quốc Dân", true },
                    { "970449", "LPB",  "LPBank",      "Ngân hàng TMCP Lộc Phát Việt Nam", true },
                    { "970428", "NAB",  "NamABank",    "Ngân hàng TMCP Nam Á", true },
                    { "970440", "SEAB", "SeABank",     "Ngân hàng TMCP Đông Nam Á", true },
                    { "970409", "BAB",  "BacABank",    "Ngân hàng TMCP Bắc Á", true },
                });

            // Seed: settings singleton (id=1)
            migrationBuilder.InsertData(
                table: "settings",
                columns: new[] { "id", "display_name", "quotation_prefix", "default_validity_days", "default_deposit_percent", "default_terms", "updated_at" },
                values: new object[] { (short)1, "FreelanceFlow Studio", "BG", 7, 50, "", new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero) });

            // Seed: 4 default services
            migrationBuilder.InsertData(
                table: "services",
                columns: new[] { "id", "name", "description", "base_price", "default_included_rounds", "extra_round_price", "active", "created_at", "updated_at" },
                values: new object[,]
                {
                    { Guid.Parse("11111111-0000-0000-0000-000000000001"), "Thiết kế Logo",         "Thiết kế bộ nhận diện thương hiệu logo",        2000000L, 2, 500000L,  true, new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero) },
                    { Guid.Parse("11111111-0000-0000-0000-000000000002"), "Thiết kế Banner",       "Thiết kế banner quảng cáo",                      500000L, 1, 300000L,  true, new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero) },
                    { Guid.Parse("11111111-0000-0000-0000-000000000003"), "Thiết kế UI/UX",        "Thiết kế giao diện ứng dụng web/mobile",         5000000L, 3, 1000000L, true, new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero) },
                    { Guid.Parse("11111111-0000-0000-0000-000000000004"), "Social Media Package",  "Bộ ảnh social media (5 ảnh/tháng)",             1500000L, 2, 400000L,  true, new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero) },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop triggers before tables
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_activity_append_only ON activity_logs; DROP FUNCTION IF EXISTS fn_activity_append_only();");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_quotation_items_locked ON quotation_items; DROP FUNCTION IF EXISTS fn_quotation_items_locked();");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_quotation_locked ON quotations; DROP FUNCTION IF EXISTS fn_quotation_locked();");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_brief_immutable ON project_briefs; DROP FUNCTION IF EXISTS fn_brief_immutable();");

            // Drop circular FK before quotation_items is dropped
            migrationBuilder.DropForeignKey(
                name: "FK_revision_items_quotation_items_quotation_item_id",
                table: "revision_items");

            migrationBuilder.DropTable(
                name: "activity_logs");

            migrationBuilder.DropTable(
                name: "designer_accounts");

            migrationBuilder.DropTable(
                name: "project_briefs");

            migrationBuilder.DropTable(
                name: "quotation_items");

            migrationBuilder.DropTable(
                name: "quotation_sequences");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "task_checklist_items");

            migrationBuilder.DropTable(
                name: "quotations");

            migrationBuilder.DropTable(
                name: "banks");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "files");

            migrationBuilder.DropTable(
                name: "revision_items");

            migrationBuilder.DropTable(
                name: "revision_rounds");

            migrationBuilder.DropTable(
                name: "client_requests");

            migrationBuilder.DropTable(
                name: "deliverables");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "services");

            migrationBuilder.DropTable(
                name: "clients");
        }
    }
}
