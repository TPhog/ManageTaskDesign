using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreelanceFlow.Api.Data.Migrations
{
    /// <summary>
    /// Aligns quotation guards with docs/04 §2. SQL only — the EF model is unchanged, so there is no
    /// Designer file and the model snapshot stays as is.
    /// - ux_quotations_one_live is keyed by number (BR-35), not project_id: a project may hold several live chains (D1, ADDITIONAL).
    /// - fn_quotation_locked freezes only content columns after DRAFT (status/accept/reject/payment stay writable) and blocks DELETE.
    /// - fn_quotation_items_locked also guards INSERT and returns OLD on DELETE (RETURN NEW silently skipped draft item deletes).
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261108100000_FixQuotationLocksAndLiveIndex")]
    public partial class FixQuotationLocksAndLiveIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_quotations_one_live;");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_quotations_one_live ON quotations (number) WHERE status IN ('SENT', 'ACCEPTED');");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_quotation_locked ON quotations;");
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_quotation_locked() RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        IF OLD.status <> 'DRAFT' THEN
            RAISE EXCEPTION 'quotation_locked: quotation % with status % cannot be deleted', OLD.id, OLD.status
                USING ERRCODE = 'check_violation';
        END IF;
        RETURN OLD;
    END IF;
    IF OLD.status <> 'DRAFT' AND (
           NEW.status = 'DRAFT'
        OR NEW.project_id          IS DISTINCT FROM OLD.project_id
        OR NEW.kind                IS DISTINCT FROM OLD.kind
        OR NEW.number              IS DISTINCT FROM OLD.number
        OR NEW.version             IS DISTINCT FROM OLD.version
        OR NEW.previous_version_id IS DISTINCT FROM OLD.previous_version_id
        OR NEW.title               IS DISTINCT FROM OLD.title
        OR NEW.issue_date          IS DISTINCT FROM OLD.issue_date
        OR NEW.expiry_date         IS DISTINCT FROM OLD.expiry_date
        OR NEW.validity_days       IS DISTINCT FROM OLD.validity_days
        OR NEW.subtotal            IS DISTINCT FROM OLD.subtotal
        OR NEW.discount_amount     IS DISTINCT FROM OLD.discount_amount
        OR NEW.vat_percent         IS DISTINCT FROM OLD.vat_percent
        OR NEW.vat_amount          IS DISTINCT FROM OLD.vat_amount
        OR NEW.total               IS DISTINCT FROM OLD.total
        OR NEW.deposit_percent     IS DISTINCT FROM OLD.deposit_percent
        OR NEW.deposit_amount      IS DISTINCT FROM OLD.deposit_amount
        OR NEW.terms               IS DISTINCT FROM OLD.terms
        OR NEW.notes_to_client     IS DISTINCT FROM OLD.notes_to_client
        OR NEW.snapshot            IS DISTINCT FROM OLD.snapshot
        OR NEW.sent_at             IS DISTINCT FROM OLD.sent_at) THEN
        RAISE EXCEPTION 'quotation_locked: content of quotation % (status %) cannot be modified', OLD.id, OLD.status
            USING ERRCODE = 'check_violation';
    END IF;
    RETURN NEW;
END; $$;");
            migrationBuilder.Sql(
                "CREATE TRIGGER trg_quotation_locked BEFORE UPDATE OR DELETE ON quotations FOR EACH ROW EXECUTE FUNCTION fn_quotation_locked();");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_quotation_items_locked ON quotation_items;");
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_quotation_items_locked() RETURNS TRIGGER LANGUAGE plpgsql AS $$
DECLARE
    q_id UUID;
    q_status TEXT;
BEGIN
    IF TG_OP = 'INSERT' THEN q_id := NEW.quotation_id; ELSE q_id := OLD.quotation_id; END IF;
    SELECT status INTO q_status FROM quotations WHERE id = q_id;
    -- q_status IS NULL: parent row already gone (cascade from deleting a DRAFT quotation)
    IF q_status IS NOT NULL AND q_status <> 'DRAFT' THEN
        RAISE EXCEPTION 'quotation_items_locked: cannot modify items of quotation % (status=%)', q_id, q_status
            USING ERRCODE = 'check_violation';
    END IF;
    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
    RETURN NEW;
END; $$;");
            migrationBuilder.Sql(
                "CREATE TRIGGER trg_quotation_items_locked BEFORE INSERT OR UPDATE OR DELETE ON quotation_items FOR EACH ROW EXECUTE FUNCTION fn_quotation_items_locked();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_quotation_items_locked ON quotation_items;");
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
END; $$;");
            migrationBuilder.Sql(
                "CREATE TRIGGER trg_quotation_items_locked BEFORE UPDATE OR DELETE ON quotation_items FOR EACH ROW EXECUTE FUNCTION fn_quotation_items_locked();");

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_quotation_locked ON quotations;");
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_quotation_locked() RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.status <> 'DRAFT' THEN
        RAISE EXCEPTION 'quotation_locked: quotation % with status % cannot be modified', OLD.id, OLD.status;
    END IF;
    RETURN NEW;
END; $$;");
            migrationBuilder.Sql(
                "CREATE TRIGGER trg_quotation_locked BEFORE UPDATE ON quotations FOR EACH ROW EXECUTE FUNCTION fn_quotation_locked();");

            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_quotations_one_live;");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_quotations_one_live ON quotations (project_id) WHERE status IN ('SENT', 'ACCEPTED');");
        }
    }
}
