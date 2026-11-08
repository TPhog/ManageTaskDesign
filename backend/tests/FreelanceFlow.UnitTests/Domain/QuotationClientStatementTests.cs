using FluentAssertions;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Quotations;

namespace FreelanceFlow.UnitTests.Domain;

/// <summary>Plan 12 BR-76/77: the client's statements are recorded, never applied (BR-01).</summary>
public class QuotationClientStatementTests
{
    private static readonly DateOnly Today = new(2026, 11, 8);
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 3, 0, 0, TimeSpan.Zero);

    private static QuotationSnapshot Snap(Quotation q) => QuotationSnapshot.Build(q,
        new SnapshotDesigner("Cam Studio", "cam@example.com", "0900000000"),
        new SnapshotClient("Nguyễn Văn A", "ABC Coffee"),
        new SnapshotProject(q.ProjectId, "Nhận diện ABC"),
        new SnapshotBank("970436", "Vietcombank", "0123456789", "NGUYEN VAN B"));

    /// <summary>total 2.400.000, deposit (50%) 1.200.000, expiry = Today + 7.</summary>
    private static Quotation Sent(int depositPercent = 50)
    {
        var q = Quotation.CreateDraft(Guid.NewGuid(), QuotationKinds.Initial, "Báo giá test", 7, depositPercent, "",
            [new QuotationItemInput(null, "Logo", "", 1, 2_400_000, QuotationItemSources.Manual, null)]);
        q.Send(Today, Now, "BG-2026-0007", Snap);
        return q;
    }

    private static void Code(Action act, string code) =>
        act.Should().Throw<DomainException>().Which.Code.Should().Be(code);

    [Fact]
    public void Client_acceptance_is_recorded_once_and_never_changes_the_status()
    {
        var q = Sent();

        q.RecordClientAcceptance(Today, Now).Should().BeTrue();
        q.RecordClientAcceptance(Today, Now.AddMinutes(5)).Should().BeFalse();

        q.Status.Should().Be(QuotationStatuses.Sent);
        q.ClientAcceptedAt.Should().Be(Now);
        q.ClientAcceptancePending().Should().BeTrue();
        q.MarkAccepted(Today, null, null);
        q.ClientAcceptancePending().Should().BeFalse();
        q.ClientAcceptedAt.Should().Be(Now, "kept as history");
    }

    [Fact]
    public void Client_acceptance_needs_a_sent_quotation_that_has_not_expired()
    {
        var accepted = Sent();
        accepted.MarkAccepted(Today, null, null);
        Code(() => accepted.RecordClientAcceptance(Today, Now), "INVALID_TRANSITION");
        Code(() => Sent().RecordClientAcceptance(Today.AddDays(8), Now), "QUOTATION_EXPIRED");
    }

    [Fact]
    public void Payment_notice_must_match_an_amount_that_is_due_now()
    {
        var q = Sent();
        Code(() => q.RecordClientPaymentNotice("remaining", null, Today, Now), "PAYMENT_NOT_APPLICABLE");
        Code(() => q.RecordClientPaymentNotice("cash", null, Today, Now), "VALIDATION_FAILED");
        Code(() => q.RecordClientPaymentNotice("deposit", new string('a', 501), Today, Now), "VALIDATION_FAILED");

        q.RecordClientPaymentNotice("deposit", "  chuyển từ TK chồng  ", Today, Now).Should().BeTrue();

        q.PaymentNotice().Should().Be(new ClientPaymentNotice("deposit", 1_200_000, Now, "chuyển từ TK chồng", true));
        q.PaymentStatus.Should().Be(PaymentStatuses.Unpaid);
    }

    [Fact]
    public void Repeating_a_pending_notice_is_idempotent_and_another_purpose_replaces_it()
    {
        var q = Sent();
        q.RecordClientPaymentNotice("deposit", null, Today, Now);

        q.RecordClientPaymentNotice("deposit", "lần 2", Today, Now.AddMinutes(1)).Should().BeFalse();
        q.PaymentNotice()!.At.Should().Be(Now);

        q.RecordClientPaymentNotice("full", null, Today, Now.AddMinutes(2)).Should().BeTrue();
        q.PaymentNotice().Should().Be(new ClientPaymentNotice("full", 2_400_000, Now.AddMinutes(2), null, true));
    }

    [Fact]
    public void Notice_stays_pending_until_the_designer_marks_the_matching_payment()
    {
        var q = Sent();
        q.RecordClientPaymentNotice("deposit", null, Today, Now);
        q.MarkAccepted(Today, null, null);
        q.PaymentNoticePending().Should().BeTrue();

        q.ChangePaymentStatus(PaymentStatuses.DepositPaid, Today, null);
        q.PaymentNoticePending().Should().BeFalse();

        q.RecordClientPaymentNotice("remaining", null, Today, Now.AddDays(1)).Should().BeTrue();
        q.PaymentNotice()!.Amount.Should().Be(1_200_000);
        q.ChangePaymentStatus(PaymentStatuses.Paid, Today, null);
        q.PaymentNoticePending().Should().BeFalse();
    }

    [Theory]
    [InlineData("SENT", "UNPAID", "deposit", true)]
    [InlineData("ACCEPTED", "DEPOSIT_PAID", "deposit", false)]
    [InlineData("ACCEPTED", "DEPOSIT_PAID", "full", true)]
    [InlineData("ACCEPTED", "PAID", "full", false)]
    [InlineData("ACCEPTED", "DEPOSIT_PAID", "remaining", true)]
    [InlineData("REJECTED", "UNPAID", "deposit", false)]
    [InlineData("SENT", "UNPAID", null, false)]
    public void Pending_rule(string status, string payment, string? purpose, bool pending) =>
        ClientStatements.IsPending(status, payment, purpose).Should().Be(pending);
}
