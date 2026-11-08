using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreelanceFlow.Api.Data.Migrations
{
    /// <summary>
    /// Plan 12 (BR-76/77, CF-13): the client's statements on a quotation ("Đồng ý báo giá", "Tôi đã chuyển khoản") and
    /// the designer's expected response time shown on the portal. fn_quotation_locked is rewritten from its current body
    /// (20261108100000_FixQuotationLocksAndLiveIndex) adding only the client_* columns to the whitelist.
    /// </summary>
    public partial class AddPortalClientStatements : Migration
    {
        // fn_quotation_locked is fail-closed (20261108100000): the client statement columns must be whitelisted or every
        // portal accept / payment notice on a SENT quotation is rejected by the trigger.
        // LockFunction("") is the 20261108100000 body byte for byte (Down); Up appends the Plan 12 columns.
        private const string Plan12Columns =
            ",\n                                  'client_accepted_at','client_payment_notice_purpose','client_payment_notice_at','client_payment_notice_note'";

        private static string LockFunction(string extraColumns) => $@"
CREATE OR REPLACE FUNCTION fn_quotation_locked() RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        IF OLD.status <> 'DRAFT' THEN
            RAISE EXCEPTION 'quotation_locked: quotation % with status % cannot be deleted', OLD.id, OLD.status
                USING ERRCODE = 'check_violation';
        END IF;
        RETURN OLD;
    END IF;
    -- Fail-closed: after DRAFT only the listed lifecycle columns may change; every other column
    -- (id, created_at, content, snapshot, and any future column) must equal its OLD value.
    IF OLD.status <> 'DRAFT' AND (
           NEW.status = 'DRAFT'
        OR (to_jsonb(NEW) - ARRAY['status','accepted_at','accepted_note','evidence_file_id','rejected_at','rejected_reason',
                                  'payment_status','deposit_paid_at','paid_at','internal_notes','updated_at'{extraColumns}])
           IS DISTINCT FROM
           (to_jsonb(OLD) - ARRAY['status','accepted_at','accepted_note','evidence_file_id','rejected_at','rejected_reason',
                                  'payment_status','deposit_paid_at','paid_at','internal_notes','updated_at'{extraColumns}])) THEN
        RAISE EXCEPTION 'quotation_locked: content of quotation % (status %) cannot be modified', OLD.id, OLD.status
            USING ERRCODE = 'check_violation';
    END IF;
    RETURN NEW;
END; $$;";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "portal_response_time",
                table: "settings",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "trong 24 giờ");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "client_accepted_at",
                table: "quotations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "client_payment_notice_at",
                table: "quotations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "client_payment_notice_note",
                table: "quotations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "client_payment_notice_purpose",
                table: "quotations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_quotations_payment_notice_at",
                table: "quotations",
                sql: "(client_payment_notice_purpose IS NULL) = (client_payment_notice_at IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quotations_payment_notice_purpose",
                table: "quotations",
                sql: "client_payment_notice_purpose IS NULL OR client_payment_notice_purpose IN ('deposit','full','remaining')");

            migrationBuilder.Sql(LockFunction(Plan12Columns));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(LockFunction(""));

            migrationBuilder.DropCheckConstraint(
                name: "ck_quotations_payment_notice_at",
                table: "quotations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_quotations_payment_notice_purpose",
                table: "quotations");

            migrationBuilder.DropColumn(
                name: "portal_response_time",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "client_accepted_at",
                table: "quotations");

            migrationBuilder.DropColumn(
                name: "client_payment_notice_at",
                table: "quotations");

            migrationBuilder.DropColumn(
                name: "client_payment_notice_note",
                table: "quotations");

            migrationBuilder.DropColumn(
                name: "client_payment_notice_purpose",
                table: "quotations");
        }
    }
}
