using FluentAssertions;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Requests;

namespace FreelanceFlow.UnitTests.Domain;

public class ClientRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 3, 0, 0, TimeSpan.Zero);

    private static ClientRequest NewRequest() =>
        ClientRequest.CreateByDesigner(Guid.NewGuid(), null, "  Đổi màu logo  ", "ZALO");

    [Fact]
    public void Designer_request_is_new_trimmed_and_marked_as_designer()   // REQ-02
    {
        var r = NewRequest();

        r.Status.Should().Be("NEW");
        r.Content.Should().Be("Đổi màu logo");
        r.CreatedBy.Should().Be("DESIGNER");
        r.Source.Should().Be("ZALO");
    }

    [Theory]
    [InlineData("   ", "ZALO")]
    [InlineData("Nội dung", "PORTAL")]   // PORTAL is the client's own channel (ck_requests_portal)
    [InlineData("Nội dung", "FAX")]
    public void Designer_request_rejects_blank_content_and_bad_source(string content, string source) =>
        FluentActions.Invoking(() => ClientRequest.CreateByDesigner(Guid.NewGuid(), null, content, source))
            .Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");

    [Fact]
    public void Triage_marks_a_new_request_triaged()
    {
        var r = NewRequest();

        r.MarkTriaged(Now);

        r.Status.Should().Be("TRIAGED");
        r.TriagedAt.Should().Be(Now);
    }

    [Fact]
    public void Triaged_request_cannot_be_triaged_or_declined_again()   // AC-R3, BR-50
    {
        var r = NewRequest();
        r.MarkTriaged(Now);

        FluentActions.Invoking(() => r.MarkTriaged(Now))
            .Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_TRANSITION");
        FluentActions.Invoking(() => r.Decline("Không làm"))
            .Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public void Decline_requires_a_reason()   // BR-52
    {
        var r = NewRequest();

        FluentActions.Invoking(() => r.Decline("  "))
            .Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
        r.Decline(" Ngoài phạm vi hợp đồng ");

        r.Status.Should().Be("DECLINED");
        r.DeclineReason.Should().Be("Ngoài phạm vi hợp đồng");
    }
}

public class RevisionItemTests
{
    private static RevisionRound DeliverableRound() =>
        RevisionRound.Create(Guid.NewGuid(), Guid.NewGuid(), 1, new DateOnly(2026, 11, 8), null);

    private static RevisionRound ProjectRound() =>
        RevisionRound.Create(Guid.NewGuid(), null, 1, new DateOnly(2026, 11, 8), null);

    private static RevisionItem Chargeable() => DeliverableRound().AddItem("Thêm tagline", "MAJOR", "CHARGEABLE", 10);

    [Fact]
    public void Add_item_fills_round_and_starts_todo()
    {
        var round = DeliverableRound();

        var item = round.AddItem("  Đổi màu  ", "MINOR", "IN_QUOTA", 10);

        item.RoundId.Should().Be(round.Id);
        item.Description.Should().Be("Đổi màu");
        item.WorkStatus.Should().Be("TODO");
        item.SortOrder.Should().Be(10);
    }

    [Fact]
    public void Round_number_must_be_positive() =>
        FluentActions.Invoking(() => RevisionRound.Create(Guid.NewGuid(), null, 0, new DateOnly(2026, 11, 8), null))
            .Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");

    [Fact]
    public void Project_level_round_rejects_in_quota_item()   // AC-Q5, BR-41
    {
        FluentActions.Invoking(() => ProjectRound().AddItem("Sửa font", "MINOR", "IN_QUOTA", 10))
            .Should().Throw<DomainException>().Which.Code.Should().Be("BUSINESS_RULE");
        ProjectRound().AddItem("Sửa font", "MINOR", "FREE_FIX", 10).Billing.Should().Be("FREE_FIX");
    }

    [Theory]
    [InlineData("", "MINOR", "IN_QUOTA")]
    [InlineData("x", "HUGE", "IN_QUOTA")]
    [InlineData("x", "MINOR", "FREE")]
    public void Add_item_validates_fields(string description, string type, string billing) =>
        FluentActions.Invoking(() => DeliverableRound().AddItem(description, type, billing, 10))
            .Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");

    [Theory]
    [InlineData("TODO", "DONE")]
    [InlineData("DONE", "CANCELLED")]
    [InlineData("CANCELLED", "TODO")]
    [InlineData("TODO", "TODO")]
    public void Invalid_work_status_transition_throws(string from, string to)
    {
        var item = DeliverableRound().AddItem("x", "MINOR", "IN_QUOTA", 10);
        item.WorkStatus = from;

        FluentActions.Invoking(() => item.ChangeWorkStatus(to, null, false))
            .Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public void In_quota_item_moves_without_reason()   // AC-S6
    {
        var item = DeliverableRound().AddItem("x", "MINOR", "IN_QUOTA", 10);

        item.ChangeWorkStatus("IN_PROGRESS", null, false).Should().BeFalse();
        item.ChangeWorkStatus("DONE", null, false).Should().BeFalse();
        item.ChangeWorkStatus("IN_PROGRESS", null, false).Should().BeFalse();   // reopen

        item.WorkStatus.Should().Be("IN_PROGRESS");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("gấp")]   // shorter than 5 characters
    public void Chargeable_item_without_accepted_quotation_hits_scope_guard(string? reason)   // AC-S1, AC-S3
    {
        var item = Chargeable();

        FluentActions.Invoking(() => item.ChangeWorkStatus("IN_PROGRESS", reason, linkedToAcceptedQuotation: false))
            .Should().Throw<DomainException>().Which.Code.Should().Be("SCOPE_GUARD");
        item.WorkStatus.Should().Be("TODO");
    }

    [Fact]
    public void Scope_guard_override_keeps_the_reason()   // AC-S2
    {
        var item = Chargeable();

        item.ChangeWorkStatus("IN_PROGRESS", "  Khách đã đồng ý qua Zalo  ", false).Should().BeTrue();

        item.ScopeOverrideReason.Should().Be("Khách đã đồng ý qua Zalo");
        item.WorkStatus.Should().Be("IN_PROGRESS");
    }

    [Fact]
    public void Accepted_quotation_lifts_the_scope_guard()   // AC-S4
    {
        var item = Chargeable();

        item.ChangeWorkStatus("IN_PROGRESS", null, linkedToAcceptedQuotation: true).Should().BeFalse();

        item.ScopeOverrideReason.Should().BeNull();
    }

    [Fact]
    public void Cancelling_a_chargeable_item_needs_no_reason() =>
        Chargeable().ChangeWorkStatus("CANCELLED", null, false).Should().BeFalse();

    [Theory]
    [InlineData("SENT")]
    [InlineData("ACCEPTED")]
    public void Billing_is_locked_while_linked_to_a_sent_or_accepted_quotation(string status)   // AC-S5, BR-61
    {
        var item = Chargeable();
        item.QuotationItemId = Guid.NewGuid();

        FluentActions.Invoking(() => item.Edit("Thêm tagline", "MAJOR", "IN_QUOTA", "", true, status))
            .Should().Throw<DomainException>().Which.Code.Should().Be("BILLING_LOCKED");
        item.Edit("Thêm tagline dưới logo", "MAJOR", "CHARGEABLE", "ghi chú", true, status);   // same billing → allowed

        item.Description.Should().Be("Thêm tagline dưới logo");
        item.InternalNotes.Should().Be("ghi chú");
        item.QuotationItemId.Should().NotBeNull();
    }

    [Fact]
    public void Leaving_chargeable_while_linked_to_a_draft_unlinks()   // ck_items_quotation
    {
        var item = Chargeable();
        item.QuotationItemId = Guid.NewGuid();

        item.Edit("Thêm tagline", "MAJOR", "FREE_FIX", "", true, "DRAFT");

        item.Billing.Should().Be("FREE_FIX");
        item.QuotationItemId.Should().BeNull();
    }

    [Fact]
    public void Edit_rejects_in_quota_on_a_project_level_round() =>   // BR-41
        FluentActions.Invoking(() => Chargeable().Edit("x", "MINOR", "IN_QUOTA", "", roundHasDeliverable: false, null))
            .Should().Throw<DomainException>().Which.Code.Should().Be("BUSINESS_RULE");

    [Fact]
    public void Link_requires_an_unquoted_live_chargeable_item()   // BR-46
    {
        var item = Chargeable();
        var line = Guid.NewGuid();

        item.LinkQuotationLine(line);

        item.QuotationItemId.Should().Be(line);
        FluentActions.Invoking(() => item.LinkQuotationLine(Guid.NewGuid()))
            .Should().Throw<DomainException>().Which.Code.Should().Be("BUSINESS_RULE");
        FluentActions.Invoking(() => DeliverableRound().AddItem("x", "MINOR", "IN_QUOTA", 10).LinkQuotationLine(line))
            .Should().Throw<DomainException>().Which.Code.Should().Be("BUSINESS_RULE");
    }

    [Fact]
    public void Follow_quotation_line_moves_to_the_copied_line_or_unlinks()   // BR-47
    {
        var oldLine = Guid.NewGuid();
        var newLine = Guid.NewGuid();
        var kept = Chargeable();
        kept.QuotationItemId = oldLine;
        var dropped = Chargeable();
        dropped.QuotationItemId = Guid.NewGuid();
        var map = new Dictionary<Guid, Guid> { [oldLine] = newLine };

        kept.FollowQuotationLine(map).Should().BeTrue();
        dropped.FollowQuotationLine(map).Should().BeFalse();

        kept.QuotationItemId.Should().Be(newLine);
        dropped.QuotationItemId.Should().BeNull();
    }
}
