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
    /// - fn_quotation_locked is fail-closed: after DRAFT only status/accept/reject/payment/internal_notes/updated_at may change; blocks DELETE.
    /// - fn_quotation_items_locked also guards INSERT and returns OLD on DELETE (RETURN NEW silently skipped draft item deletes); UPDATE checks both old and new parent.
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
    -- Fail-closed: after DRAFT only the listed lifecycle columns may change; every other column
    -- (id, created_at, content, snapshot, and any future column) must equal its OLD value.
    IF OLD.status <> 'DRAFT' AND (
           NEW.status = 'DRAFT'
        OR (to_jsonb(NEW) - ARRAY['status','accepted_at','accepted_note','evidence_file_id','rejected_at','rejected_reason',
                                  'payment_status','deposit_paid_at','paid_at','internal_notes','updated_at'])
           IS DISTINCT FROM
           (to_jsonb(OLD) - ARRAY['status','accepted_at','accepted_note','evidence_file_id','rejected_at','rejected_reason',
                                  'payment_status','deposit_paid_at','paid_at','internal_notes','updated_at'])) THEN
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
    -- INSERT checks NEW parent, DELETE checks OLD parent, UPDATE checks both (an item may be moved between quotations).
    FOR q_id IN
        SELECT DISTINCT x FROM unnest(ARRAY[
            CASE WHEN TG_OP = 'INSERT' THEN NULL ELSE OLD.quotation_id END,
            CASE WHEN TG_OP = 'DELETE' THEN NULL ELSE NEW.quotation_id END]) AS x
        WHERE x IS NOT NULL
    LOOP
        SELECT status INTO q_status FROM quotations WHERE id = q_id;
        -- q_status IS NULL: parent row already gone (cascade from deleting a DRAFT quotation)
        IF q_status IS NOT NULL AND q_status <> 'DRAFT' THEN
            RAISE EXCEPTION 'quotation_items_locked: cannot modify items of quotation % (status=%)', q_id, q_status
                USING ERRCODE = 'check_violation';
        END IF;
    END LOOP;
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
