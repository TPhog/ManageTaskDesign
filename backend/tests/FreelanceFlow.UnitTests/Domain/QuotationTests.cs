using System.Text.Json;
using FluentAssertions;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Quotations;

namespace FreelanceFlow.UnitTests.Domain;

public class QuotationTests
{
    private static readonly DateOnly Today = new(2026, 11, 8);
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 3, 0, 0, TimeSpan.Zero);

    private static QuotationItemInput Line(string description, int quantity, long unitPrice, Guid? id = null) =>
        new(id, description, "", quantity, unitPrice, QuotationItemSources.Manual, null);

    private static Quotation Draft(params QuotationItemInput[] items) =>
        Quotation.CreateDraft(Guid.NewGuid(), QuotationKinds.Initial, "Báo giá test", 7, 50, "Thanh toán trong 7 ngày", items);

    private static QuotationDraftEdit Edit(Quotation q, long discount = 0, int vat = 0, int deposit = 50,
        IReadOnlyList<QuotationItemInput>? items = null) =>
        new(q.Title, q.ValidityDays, discount, vat, deposit, q.Terms, "", "",
            items ?? q.Items.Select(i => new QuotationItemInput(i.Id, i.Description, i.Details, i.Quantity, i.UnitPrice, i.SourceType, i.SourceId)).ToList());

    private static QuotationSnapshot Snap(Quotation q) => QuotationSnapshot.Build(q,
        new SnapshotDesigner("Cam Studio", "cam@example.com", "0900000000"),
        new SnapshotClient("Nguyễn Văn A", "ABC Coffee"),
        new SnapshotProject(q.ProjectId, "Nhận diện ABC"),
        new SnapshotBank("970436", "Vietcombank", "0123456789", "NGUYEN VAN B"));

    /// <summary>AC-B1 numbers: 2.000.000 + 500.000 − 100.000 → total 2.400.000, deposit (50%) 1.200.000.</summary>
    private static Quotation Sent(int depositPercent = 50)
    {
        var q = Draft(Line("Logo", 1, 2_000_000), Line("Banner", 1, 500_000));
        q.EditDraft(Edit(q, discount: 100_000, deposit: depositPercent));
        q.Send(Today, Now, "BG-2026-0007", Snap);
        return q;
    }

    private static Quotation Accepted(int depositPercent = 50)
    {
        var q = Sent(depositPercent);
        q.MarkAccepted(Today, null, null);
        return q;
    }

    // ---------- fix round: caps, CreateDraft validation, undo date ----------

    private static void AssertValidation(Action act) =>
        act.Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(10_001, 1000)]
    [InlineData(1, -1)]
    [InlineData(1, 1_000_000_000_001)]
    public void Line_quantity_and_price_outside_caps_are_rejected(int quantity, long unitPrice) =>
        AssertValidation(() => Draft(Line("Logo", quantity, unitPrice)));

    [Fact]
    public void Line_at_the_caps_is_accepted_when_subtotal_stays_safe()
    {
        var q = Draft(Line("Logo", 1, 1_000_000_000_000), Line("Banner", 10_000, 1_000_000_000));
        q.Subtotal.Should().Be(11_000_000_000_000);
    }

    [Fact]
    public void Subtotal_above_js_safe_integer_is_rejected() =>
        AssertValidation(() => Draft(Line("A", 10_000, 1_000_000_000_000)));   // 1e16 > 2^53-1

    [Fact]
    public void Total_above_js_safe_integer_after_vat_is_rejected()
    {
        var q = Draft(Line("A", 9_000, 1_000_000_000_000));                       // 9e15 <= 9.007e15
        AssertValidation(() => q.EditDraft(Edit(q, vat: 20)));                   // 1.08e16 total
    }

    [Theory]
    [InlineData("INITIAL", "", 7, 50)]
    [InlineData("BOGUS", "T", 7, 50)]
    [InlineData("INITIAL", "T", 0, 50)]
    [InlineData("INITIAL", "T", -3, 50)]
    [InlineData("INITIAL", "T", 366, 50)]
    [InlineData("INITIAL", "T", 7, -1)]
    [InlineData("INITIAL", "T", 7, 101)]
    public void CreateDraft_rejects_invalid_header(string kind, string title, int validity, int deposit) =>
        AssertValidation(() => Quotation.CreateDraft(Guid.NewGuid(), kind, title, validity, deposit, "t", [Line("Logo", 1, 1000)]));

    [Fact]
    public void Undo_to_deposit_paid_after_direct_paid_sets_deposit_paid_date()
    {
        var q = Accepted();
        q.ChangePaymentStatus(PaymentStatuses.Paid, Today, null);
        q.DepositPaidAt.Should().BeNull();

        q.ChangePaymentStatus(PaymentStatuses.DepositPaid, new DateOnly(2026, 11, 12), "Bấm nhầm");

        q.DepositPaidAt.Should().Be(new DateOnly(2026, 11, 12));
        q.PaidAt.Should().BeNull();
    }

    [Fact]
    public void Undo_to_deposit_paid_keeps_an_existing_deposit_paid_date()
    {
        var q = Accepted();
        q.ChangePaymentStatus(PaymentStatuses.DepositPaid, new DateOnly(2026, 11, 10), null);
        q.ChangePaymentStatus(PaymentStatuses.Paid, new DateOnly(2026, 11, 20), null);

        q.ChangePaymentStatus(PaymentStatuses.DepositPaid, new DateOnly(2026, 11, 25), "Bấm nhầm");

        q.DepositPaidAt.Should().Be(new DateOnly(2026, 11, 10));
    }

    // ---------- money (§8.3) ----------

    [Fact]
    public void CreateDraft_computes_money_from_items()
    {
        var q = Draft(Line("Logo", 2, 1_000_000), Line("Banner", 1, 500_000));

        q.Status.Should().Be(QuotationStatuses.Draft);
        q.Items.Should().OnlyContain(i => i.QuotationId == q.Id);
        q.Items.Select(i => i.Amount).Should().Equal(2_000_000, 500_000);
        q.Subtotal.Should().Be(2_500_000);
        q.Total.Should().Be(2_500_000);
        q.DepositAmount.Should().Be(1_250_000);
    }

    [Fact]
    public void EditDraft_applies_discount_and_deposit_like_AC_B1()
    {
        var q = Draft(Line("Logo", 1, 2_000_000), Line("Banner", 1, 500_000));

        q.EditDraft(Edit(q, discount: 100_000, deposit: 50));

        q.Subtotal.Should().Be(2_500_000);
        q.VatAmount.Should().Be(0);
        q.Total.Should().Be(2_400_000);
        q.DepositAmount.Should().Be(1_200_000);
    }

    [Fact]
    public void EditDraft_rounds_vat_half_up_like_AC_B2()
    {
        var q = Draft(Line("Gói thiết kế", 1, 1_234_567));

        q.EditDraft(Edit(q, vat: 8, deposit: 0));

        q.VatAmount.Should().Be(98_765);
        q.Total.Should().Be(1_333_332);
        q.DepositAmount.Should().Be(0);
    }

    [Fact]
    public void EditDraft_rejects_discount_above_subtotal()
    {
        var q = Draft(Line("Logo", 1, 1_000));

        var ex = Assert.Throws<DomainException>(() => q.EditDraft(Edit(q, discount: 1_001)));

        ex.Code.Should().Be("VALIDATION_FAILED");
    }

    [Theory]
    [InlineData(21, 50)]
    [InlineData(-1, 50)]
    [InlineData(0, 101)]
    public void EditDraft_rejects_out_of_range_percentages(int vat, int deposit)
    {
        var q = Draft(Line("Logo", 1, 1_000));

        Assert.Throws<DomainException>(() => q.EditDraft(Edit(q, vat: vat, deposit: deposit)))
            .Code.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public void EditDraft_updates_matching_rows_in_place_and_reports_added_and_removed()
    {
        var q = Draft(Line("Logo", 1, 2_000_000), Line("Banner", 1, 500_000));
        var logo = q.Items[0];
        var banner = q.Items[1];

        var changes = q.EditDraft(Edit(q, items: [Line("Logo v2", 2, 2_000_000, logo.Id), Line("Poster", 1, 800_000)]));

        q.Items.Should().HaveCount(2);
        q.Items[0].Should().BeSameAs(logo);
        logo.Description.Should().Be("Logo v2");
        logo.Amount.Should().Be(4_000_000);
        changes.Removed.Should().ContainSingle().Which.Should().BeSameAs(banner);
        changes.Added.Should().ContainSingle().Which.Description.Should().Be("Poster");
        q.Items.Select(i => i.SortOrder).Should().Equal(10, 20);
        q.Subtotal.Should().Be(4_800_000);
    }

    [Fact]
    public void EditDraft_on_sent_quotation_is_locked()
    {
        var q = Sent();

        Assert.Throws<DomainException>(() => q.EditDraft(Edit(q))).Code.Should().Be("QUOTATION_LOCKED");
    }

    // ---------- send (BR-31, BR-32) ----------

    [Fact]
    public void Send_assigns_number_dates_and_snapshot()
    {
        var q = Sent();

        q.Status.Should().Be(QuotationStatuses.Sent);
        q.Number.Should().Be("BG-2026-0007");
        q.IssueDate.Should().Be(Today);
        q.ExpiryDate.Should().Be(Today.AddDays(7));
        q.SentAt.Should().Be(Now);
        var snapshot = QuotationSnapshot.FromJson(q.Snapshot!);
        snapshot.SchemaVersion.Should().Be(1);
        snapshot.Number.Should().Be("BG-2026-0007");
        snapshot.Total.Should().Be(2_400_000);
        snapshot.Items.Select(i => i.Description).Should().Equal("Logo", "Banner");
        snapshot.Bank.AccountNo.Should().Be("0123456789");
    }

    [Fact]
    public void Snapshot_json_is_camel_case_and_has_no_internal_notes()
    {
        var json = Sent().Snapshot!;

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("bank").GetProperty("bin").GetString().Should().Be("970436");
        doc.RootElement.GetProperty("issueDate").GetString().Should().Be("2026-11-08");
        doc.RootElement.TryGetProperty("internalNotes", out _).Should().BeFalse();
    }

    [Fact]
    public void Send_without_items_is_a_business_rule_violation()
    {
        var q = Draft();

        Assert.Throws<DomainException>(() => q.Send(Today, Now, "BG-2026-0001", Snap)).Code.Should().Be("BUSINESS_RULE");
    }

    [Fact]
    public void Send_twice_is_an_invalid_transition()
    {
        var q = Sent();

        Assert.Throws<DomainException>(() => q.Send(Today, Now, "BG-2026-0008", Snap)).Code.Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public void FormatNumber_pads_sequence_to_four_digits() =>
        Quotation.FormatNumber("BG", 2026, 7).Should().Be("BG-2026-0007");

    // ---------- versions (BR-33, BR-34) ----------

    [Fact]
    public void CreateNextVersion_copies_a_draft_with_same_number()
    {
        var v1 = Sent();

        var v2 = v1.CreateNextVersion(2);

        v2.Status.Should().Be(QuotationStatuses.Draft);
        v2.Number.Should().Be(v1.Number);
        v2.Version.Should().Be(2);
        v2.PreviousVersionId.Should().Be(v1.Id);
        v2.Snapshot.Should().BeNull();
        v2.Total.Should().Be(v1.Total);
        v2.Items.Select(i => i.CopiedFromItemId).Should().Equal(v1.Items.OrderBy(i => i.SortOrder).Select(i => (Guid?)i.Id));
        v2.Items.Should().OnlyContain(i => i.QuotationId == v2.Id && !v1.Items.Select(x => x.Id).Contains(i.Id));
    }

    [Fact]
    public void CreateNextVersion_from_rejected_is_allowed()
    {
        var q = Sent();
        q.MarkRejected(Today, "Giá cao");

        q.CreateNextVersion(2).Status.Should().Be(QuotationStatuses.Draft);
    }

    [Fact]
    public void CreateNextVersion_from_draft_or_accepted_is_invalid()
    {
        Assert.Throws<DomainException>(() => Draft(Line("Logo", 1, 1)).CreateNextVersion(2)).Code.Should().Be("INVALID_TRANSITION");
        Assert.Throws<DomainException>(() => Accepted().CreateNextVersion(2)).Code.Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public void Sending_next_version_keeps_the_number()
    {
        var v2 = Sent().CreateNextVersion(2);

        v2.Send(Today, Now, null, Snap);

        v2.Number.Should().Be("BG-2026-0007");
        QuotationSnapshot.FromJson(v2.Snapshot!).Version.Should().Be(2);
    }

    [Fact]
    public void MarkSuperseded_only_from_sent()
    {
        var q = Sent();
        q.MarkSuperseded();
        q.Status.Should().Be(QuotationStatuses.Superseded);

        Assert.Throws<DomainException>(() => Accepted().MarkSuperseded()).Code.Should().Be("INVALID_TRANSITION");
    }

    // ---------- accept / reject (BR-36, BR-37) ----------

    [Fact]
    public void MarkAccepted_records_date_note_and_evidence()
    {
        var q = Sent();
        var evidence = Guid.NewGuid();

        q.MarkAccepted(new DateOnly(2026, 11, 9), "  Khách OK qua Zalo ", evidence);

        q.Status.Should().Be(QuotationStatuses.Accepted);
        q.AcceptedAt.Should().Be(new DateOnly(2026, 11, 9));
        q.AcceptedNote.Should().Be("Khách OK qua Zalo");
        q.EvidenceFileId.Should().Be(evidence);
    }

    [Fact]
    public void MarkAccepted_is_allowed_after_expiry()
    {
        var q = Sent();
        q.EffectiveStatus(Today.AddDays(30)).Should().Be(QuotationStatuses.Expired);

        q.MarkAccepted(Today.AddDays(30), null, null);

        q.Status.Should().Be(QuotationStatuses.Accepted);
    }

    [Fact]
    public void MarkAccepted_from_draft_is_invalid() =>
        Assert.Throws<DomainException>(() => Draft(Line("Logo", 1, 1)).MarkAccepted(Today, null, null))
            .Code.Should().Be("INVALID_TRANSITION");

    [Fact]
    public void MarkRejected_requires_reason()
    {
        Assert.Throws<DomainException>(() => Sent().MarkRejected(Today, "  ")).Code.Should().Be("VALIDATION_FAILED");

        var q = Sent();
        q.MarkRejected(Today, " Ngân sách không đủ ");
        q.Status.Should().Be(QuotationStatuses.Rejected);
        q.RejectedReason.Should().Be("Ngân sách không đủ");
        q.RejectedAt.Should().Be(Today);
    }

    [Fact]
    public void MarkRejected_from_accepted_is_invalid() =>
        Assert.Throws<DomainException>(() => Accepted().MarkRejected(Today, "x")).Code.Should().Be("INVALID_TRANSITION");

    // ---------- payment (BR-70..73) ----------

    [Fact]
    public void Payment_status_needs_an_accepted_quotation()   // AC-B15
    {
        Assert.Throws<DomainException>(() => Sent().ChangePaymentStatus(PaymentStatuses.Paid, Today, null))
            .Code.Should().Be("BUSINESS_RULE");
    }

    [Fact]
    public void Deposit_paid_needs_a_deposit()                 // AC-B14
    {
        Assert.Throws<DomainException>(() => Accepted(depositPercent: 0).ChangePaymentStatus(PaymentStatuses.DepositPaid, Today, null))
            .Code.Should().Be("BUSINESS_RULE");
    }

    [Fact]
    public void Forward_payment_steps_record_dates_without_reason()
    {
        var q = Accepted();

        q.ChangePaymentStatus(PaymentStatuses.DepositPaid, new DateOnly(2026, 11, 10), null).Should().BeFalse();
        q.DepositPaidAt.Should().Be(new DateOnly(2026, 11, 10));
        q.OutstandingAmount().Should().Be(1_200_000);

        q.ChangePaymentStatus(PaymentStatuses.Paid, new DateOnly(2026, 11, 20), null).Should().BeFalse();
        q.PaidAt.Should().Be(new DateOnly(2026, 11, 20));
        q.OutstandingAmount().Should().Be(0);
    }

    [Fact]
    public void Undo_requires_reason_and_clears_dates()        // AC-B16
    {
        var q = Accepted();
        q.ChangePaymentStatus(PaymentStatuses.Paid, Today, null);

        Assert.Throws<DomainException>(() => q.ChangePaymentStatus(PaymentStatuses.DepositPaid, Today, null))
            .Code.Should().Be("VALIDATION_FAILED");

        q.ChangePaymentStatus(PaymentStatuses.DepositPaid, Today, "Bấm nhầm").Should().BeTrue();
        q.PaymentStatus.Should().Be(PaymentStatuses.DepositPaid);
        q.PaidAt.Should().BeNull();

        q.ChangePaymentStatus(PaymentStatuses.Unpaid, Today, "Khách chưa chuyển").Should().BeTrue();
        q.DepositPaidAt.Should().BeNull();
    }

    [Fact]
    public void Same_payment_status_is_an_invalid_transition() =>
        Assert.Throws<DomainException>(() => Accepted().ChangePaymentStatus(PaymentStatuses.Unpaid, Today, "x"))
            .Code.Should().Be("INVALID_TRANSITION");

    [Fact]
    public void Outstanding_is_zero_unless_accepted()
    {
        Sent().OutstandingAmount().Should().Be(0);
        Accepted().OutstandingAmount().Should().Be(2_400_000);
    }

    // ---------- effective status & QR options (§8.1, BR-75) ----------

    [Fact]
    public void Expired_is_computed_only_after_expiry_date()
    {
        var q = Sent();   // expiry = Today + 7

        q.EffectiveStatus(Today.AddDays(7)).Should().Be(QuotationStatuses.Sent);
        q.EffectiveStatus(Today.AddDays(8)).Should().Be(QuotationStatuses.Expired);
        q.Status.Should().Be(QuotationStatuses.Sent);
        Draft(Line("Logo", 1, 1)).EffectiveStatus(Today.AddDays(100)).Should().Be(QuotationStatuses.Draft);
    }

    [Fact]
    public void Qr_options_follow_BR_75()
    {
        Draft(Line("Logo", 1, 1)).PaymentOptions(Today).Should().BeEmpty();

        Sent().PaymentOptions(Today).Select(o => (o.Purpose, o.Amount)).Should()
            .Equal((QrPurposes.Deposit, 1_200_000L), (QrPurposes.Full, 2_400_000L));
        Sent().PaymentOptions(Today.AddDays(30)).Should().HaveCount(2);                 // EXPIRED still shows QR
        Sent(depositPercent: 0).PaymentOptions(Today).Select(o => o.Purpose).Should().Equal(QrPurposes.Full);
        Accepted().PaymentOptions(Today).Should().HaveCount(2);                         // ACCEPTED + UNPAID

        var depositPaid = Accepted();
        depositPaid.ChangePaymentStatus(PaymentStatuses.DepositPaid, Today, null);
        depositPaid.PaymentOptions(Today).Select(o => (o.Purpose, o.Amount)).Should()
            .Equal((QrPurposes.Remaining, 1_200_000L));

        var paid = Accepted();
        paid.ChangePaymentStatus(PaymentStatuses.Paid, Today, null);
        paid.PaymentOptions(Today).Should().BeEmpty();

        var rejected = Sent();
        rejected.MarkRejected(Today, "x");
        rejected.PaymentOptions(Today).Should().BeEmpty();
    }

    [Fact]
    public void Qr_option_labels_are_vietnamese() =>
        Sent().PaymentOptions(Today).Select(o => o.Label).Should().Equal("Thanh toán cọc 50%", "Thanh toán toàn bộ");

    [Fact]
    public void Only_drafts_can_be_deleted()
    {
        Draft(Line("Logo", 1, 1)).EnsureDeletable();
        Assert.Throws<DomainException>(() => Sent().EnsureDeletable()).Code.Should().Be("QUOTATION_LOCKED");
    }
}
