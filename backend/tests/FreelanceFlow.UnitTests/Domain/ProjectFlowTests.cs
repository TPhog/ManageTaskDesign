using FluentAssertions;
using FreelanceFlow.Api.Domain.Flow;

namespace FreelanceFlow.UnitTests.Domain;

public class ProjectFlowTests
{
    private static readonly DateOnly Today = new(2026, 11, 8);

    private static FlowDeliverable D(string status, string name = "Logo", int total = 0, int done = 0,
        string? decision = null, int included = 2, int used = 0, int openRevisions = 0) =>
        new(Guid.NewGuid(), name, status, total, done, decision, included, used, openRevisions);

    private static FlowQuotation Q(string status, string kind = "INITIAL", string payment = "UNPAID",
        DateOnly? expiry = null, int depositPercent = 50, long total = 2_000_000, long deposit = 1_000_000,
        string? number = "BG-2026-0001", int minutes = 0) =>
        new(Guid.NewGuid(), kind, status, number, 1, expiry, payment, depositPercent, total, deposit,
            DateTimeOffset.UnixEpoch.AddMinutes(minutes));

    private static FlowFacts Facts(string projectStatus = "DRAFT", bool brief = false,
        FlowDeliverable[]? deliverables = null, FlowQuotation[]? quotations = null,
        int overdue = 0, Guid[]? requests = null, int chargeable = 0, int openTasks = 0) =>
        new(projectStatus, brief, deliverables ?? [], quotations ?? [], overdue, requests ?? [], chargeable, openTasks);

    private static FlowState Eval(FlowFacts f) => ProjectFlow.Evaluate(f, Today);

    private static List<string> Kinds(FlowState s) => s.Actions.Select(a => a.Kind).ToList();

    private static List<string> StepStatuses(FlowState s) => s.Steps.Select(x => x.Status).ToList();

    private static void OnlyFirstIsPrimary(FlowState s) =>
        s.Actions.Select(a => a.Primary).Should().Equal(s.Actions.Select((_, i) => i == 0));

    [Fact]
    public void Delivered_with_open_revisions_warns_in_execute_review_close_but_not_completed_nor_without_open_items()
    {
        var paid = new[] { Q("ACCEPTED", payment: "PAID") };
        var execute = Eval(Facts("IN_PROGRESS", true, [D("DELIVERED", openRevisions: 2), D("IN_PROGRESS")], paid));
        var review = Eval(Facts("IN_PROGRESS", true, [D("DELIVERED", openRevisions: 1), D("IN_REVIEW")], paid));
        var close = Eval(Facts("IN_PROGRESS", true, [D("DELIVERED", openRevisions: 1)], paid));
        var completed = Eval(Facts("COMPLETED", true, [D("DELIVERED", openRevisions: 1)], paid));
        var clean = Eval(Facts("IN_PROGRESS", true, [D("DELIVERED")], paid));

        (execute.Stage, review.Stage, close.Stage).Should().Be(("EXECUTE", "REVIEW", "CLOSE"));
        execute.Warnings.Should().Contain(new FlowWarning("DELIVERED_WITH_OPEN_REVISIONS",
            "Logo đã bàn giao nhưng còn 2 mục sửa chưa xong — chuyển về Đang làm", "warning"));
        foreach (var s in new[] { review, close })
            s.Warnings.Select(w => w.Kind).Should().Contain("DELIVERED_WITH_OPEN_REVISIONS");
        completed.Warnings.Select(w => w.Kind).Should().NotContain("DELIVERED_WITH_OPEN_REVISIONS");
        clean.Warnings.Select(w => w.Kind).Should().NotContain("DELIVERED_WITH_OPEN_REVISIONS");
    }

    [Fact]
    public void New_project_is_in_prepare_and_suggests_adding_a_deliverable_first()
    {
        var s = Eval(Facts());

        s.Stage.Should().Be("PREPARE");
        StepStatuses(s).Should().Equal("current", "upcoming", "upcoming", "upcoming", "upcoming");
        s.Steps.Select(x => x.Stage).Should().Equal("PREPARE", "QUOTE", "EXECUTE", "REVIEW", "CLOSE");
        Kinds(s).Should().Equal("ADD_DELIVERABLE", "CONFIRM_BRIEF");
        s.Actions[0].Label.Should().Be("Thêm hạng mục");
        OnlyFirstIsPrimary(s);
        s.Warnings.Should().Equal(new FlowWarning("BRIEF_NOT_CONFIRMED", "Brief chưa được xác nhận", "info"));
    }

    [Fact]
    public void Prepare_does_not_suggest_a_quotation_that_already_exists()
    {
        var s = Eval(Facts(deliverables: [D("NOT_STARTED")], quotations: [Q("DRAFT", number: null)]));

        s.Stage.Should().Be("PREPARE");
        Kinds(s).Should().Equal("CONFIRM_BRIEF");
    }

    [Fact]
    public void Finished_preparation_moves_to_quote_and_suggests_creating_the_quotation()
    {
        var s = Eval(Facts(brief: true, deliverables: [D("NOT_STARTED")]));

        s.Stage.Should().Be("QUOTE");
        StepStatuses(s).Should().Equal("done", "current", "upcoming", "upcoming", "upcoming");
        Kinds(s).Should().Equal("CREATE_QUOTATION");
        s.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Quote_with_a_draft_suggests_sending_it()
    {
        var draft = Q("DRAFT", number: null);

        var s = Eval(Facts(brief: true, deliverables: [D("NOT_STARTED")], quotations: [draft]));

        s.Actions.Should().ContainSingle()
            .Which.Should().Be(new FlowAction("SEND_QUOTATION", "Gửi báo giá", true, new FlowTarget(QuotationId: draft.Id)));
    }

    [Fact]
    public void Quote_with_a_sent_quotation_suggests_recording_the_acceptance_and_copying_the_message()
    {
        var sent = Q("SENT", expiry: Today.AddDays(10));

        var s = Eval(Facts(brief: true, deliverables: [D("NOT_STARTED")], quotations: [sent]));

        Kinds(s).Should().Equal("RECORD_ACCEPTANCE", "COPY_QUOTATION_MESSAGE", "MARK_QUOTED");
        s.Actions.Where(a => a.Kind != "MARK_QUOTED").Should().OnlyContain(a => a.Target.QuotationId == sent.Id);
        s.Actions[0].Label.Should().Be("Ghi nhận khách chấp nhận báo giá");
        OnlyFirstIsPrimary(s);
        s.Warnings.Should().BeEmpty();
    }

    [Theory]
    [InlineData(3, "QUOTATION_EXPIRING", "Báo giá BG-2026-0001 (v1) còn 3 ngày là hết hạn")]
    [InlineData(0, "QUOTATION_EXPIRING", "Báo giá BG-2026-0001 (v1) hết hạn hôm nay")]
    [InlineData(-1, "QUOTATION_EXPIRED", "Báo giá BG-2026-0001 (v1) đã hết hạn ngày 07/11/2026")]
    public void Sent_quotation_close_to_or_past_expiry_warns(int daysLeft, string kind, string message)
    {
        var s = Eval(Facts(brief: true, deliverables: [D("NOT_STARTED")],
            quotations: [Q("SENT", expiry: Today.AddDays(daysLeft))]));

        s.Warnings.Should().Equal(new FlowWarning(kind, message, "warning"));
        Kinds(s).Should().Contain("RECORD_ACCEPTANCE");
    }

    [Fact]
    public void Accepted_additional_quotation_does_not_finish_the_quote_stage()
    {
        var s = Eval(Facts(brief: true, deliverables: [D("NOT_STARTED")], quotations: [Q("ACCEPTED", kind: "ADDITIONAL")]));

        s.Stage.Should().Be("QUOTE");
        Kinds(s).Should().Equal("CREATE_QUOTATION");
    }

    [Fact]
    public void Accepted_initial_quotation_moves_to_execute_with_start_and_a_deposit_warning()
    {
        var accepted = Q("ACCEPTED");

        var s = Eval(Facts(brief: true, deliverables: [D("NOT_STARTED")], quotations: [accepted]));

        s.Stage.Should().Be("EXECUTE");
        StepStatuses(s).Should().Equal("done", "done", "current", "upcoming", "upcoming");
        Kinds(s).Should().Equal("START_PROJECT", "RECORD_DEPOSIT", "COPY_PAYMENT_REMINDER");
        s.Actions[1].Target.QuotationId.Should().Be(accepted.Id);
        s.Actions[2].Target.QuotationId.Should().Be(accepted.Id);
        OnlyFirstIsPrimary(s);
        s.Warnings.Should().Equal(new FlowWarning("DEPOSIT_UNPAID",
            "Chưa nhận cọc 1.000.000 ₫ của báo giá BG-2026-0001 (v1)", "warning"));
    }

    [Fact]
    public void Unpaid_quotation_without_deposit_asks_for_the_payment()
    {
        var s = Eval(Facts(brief: true, deliverables: [D("NOT_STARTED")],
            quotations: [Q("ACCEPTED", depositPercent: 0, deposit: 0)]));

        s.Warnings.Single().Message.Should().Be("Chưa nhận thanh toán báo giá BG-2026-0001 (v1)");
    }

    [Fact]
    public void Paid_deposit_has_no_warning_and_a_started_project_no_start_action()
    {
        var s = Eval(Facts("IN_PROGRESS", brief: true, deliverables: [D("IN_PROGRESS")],
            quotations: [Q("ACCEPTED", payment: "DEPOSIT_PAID")]));

        s.Stage.Should().Be("EXECUTE");
        s.Actions.Should().BeEmpty();
        s.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Submit_review_is_suggested_only_when_every_task_of_the_deliverable_is_done()
    {
        var logo = D("IN_PROGRESS", "Logo", total: 3, done: 3);
        var banner = D("IN_PROGRESS", "Banner", total: 3, done: 2);
        var poster = D("IN_PROGRESS", "Poster");

        var s = Eval(Facts("IN_PROGRESS", brief: true, deliverables: [logo, banner, poster],
            quotations: [Q("ACCEPTED", payment: "PAID")], overdue: 2));

        s.Actions.Should().Equal(new FlowAction("SUBMIT_REVIEW", "Gửi duyệt: Logo", true, new FlowTarget(DeliverableId: logo.Id)));
        s.Warnings.Should().Equal(new FlowWarning("TASKS_OVERDUE", "2 công việc quá hạn", "warning"));
    }

    [Fact]
    public void Every_active_deliverable_in_review_or_later_moves_to_review()
    {
        var approvedOnPortal = D("IN_REVIEW", "Logo", decision: "APPROVED");
        var waiting = D("IN_REVIEW", "Poster");
        var approved = D("APPROVED", "Banner");

        var s = Eval(Facts("IN_PROGRESS", brief: true, deliverables: [approvedOnPortal, waiting, approved, D("CANCELLED", "Bỏ")],
            quotations: [Q("ACCEPTED", payment: "PAID")]));

        s.Stage.Should().Be("REVIEW");
        StepStatuses(s).Should().Equal("done", "done", "done", "current", "upcoming");
        s.Actions.Should().Equal(
            new FlowAction("CONFIRM_APPROVAL", "Xác nhận khách đã duyệt: Logo", true, new FlowTarget(DeliverableId: approvedOnPortal.Id)),
            new FlowAction("DELIVER", "Bàn giao: Banner", false, new FlowTarget(DeliverableId: approved.Id)));
        s.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Handing_over_with_money_due_warns_balance_due()
    {
        var accepted = Q("ACCEPTED", payment: "DEPOSIT_PAID");

        var s = Eval(Facts("IN_PROGRESS", brief: true, deliverables: [D("APPROVED")], quotations: [accepted]));

        Kinds(s).Should().Equal("DELIVER", "COPY_PAYMENT_REMINDER");
        s.Actions[1].Target.QuotationId.Should().Be(accepted.Id);
        s.Warnings.Should().Equal(new FlowWarning("BALANCE_DUE", "Khách còn nợ 1.000.000 ₫", "warning"));
    }

    [Fact]
    public void New_requests_unquoted_chargeable_items_and_used_up_quota_show_up_during_review()
    {
        var first = Guid.NewGuid();

        var s = Eval(Facts("IN_PROGRESS", brief: true, deliverables: [D("IN_REVIEW", included: 2, used: 2)],
            quotations: [Q("ACCEPTED", payment: "PAID")], requests: [first, Guid.NewGuid()], chargeable: 3));

        s.Actions.Should().Equal(
            new FlowAction("TRIAGE_REQUEST", "Xử lý 2 yêu cầu mới của khách", true, new FlowTarget(RequestId: first)),
            new FlowAction("CREATE_ADDITIONAL_QUOTATION", "Tạo báo giá phát sinh (3 mục tính phí)", false, new FlowTarget()));
        s.Warnings.Should().Equal(new FlowWarning("QUOTA_EXCEEDED", "Logo đã dùng hết 2 vòng sửa trong quota", "info"));
    }

    [Fact]
    public void Execute_warnings_are_ordered_deposit_unpaid_then_tasks_overdue_then_quota_exceeded()
    {
        var s = Eval(Facts("IN_PROGRESS", brief: true, deliverables: [D("IN_PROGRESS", included: 1, used: 1)],
            quotations: [Q("ACCEPTED")], overdue: 2));

        s.Stage.Should().Be("EXECUTE");
        s.Warnings.Select(w => w.Kind).Should().Equal("DEPOSIT_UNPAID", "TASKS_OVERDUE", "QUOTA_EXCEEDED");
    }

    [Fact]
    public void One_new_request_has_a_singular_label()
    {
        var s = Eval(Facts("IN_PROGRESS", brief: true, deliverables: [D("IN_PROGRESS")],
            quotations: [Q("ACCEPTED", payment: "PAID")], requests: [Guid.NewGuid()]));

        s.Actions.Single().Label.Should().Be("Xử lý yêu cầu mới của khách");
    }

    [Fact]
    public void Everything_delivered_in_a_running_project_suggests_completing_it()
    {
        var s = Eval(Facts("IN_PROGRESS", brief: true, deliverables: [D("DELIVERED"), D("CANCELLED", "Bỏ")],
            quotations: [Q("ACCEPTED", payment: "PAID")]));

        s.Stage.Should().Be("CLOSE");
        StepStatuses(s).Should().Equal("done", "done", "done", "done", "current");
        s.Actions.Should().Equal(new FlowAction("COMPLETE_PROJECT", "Hoàn thành dự án", true, new FlowTarget()));
        s.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Completed_project_is_all_done_and_still_warns_about_money_due()
    {
        var accepted = Q("ACCEPTED");

        var s = Eval(Facts("COMPLETED", brief: false, deliverables: [D("DELIVERED")], quotations: [accepted]));

        s.Stage.Should().Be("CLOSE");
        StepStatuses(s).Should().Equal("done", "done", "done", "done", "done");
        s.Actions.Should().Equal(new FlowAction("COPY_PAYMENT_REMINDER", "Sao chép tin nhắn nhắc thanh toán", true,
            new FlowTarget(QuotationId: accepted.Id)));
        s.Warnings.Should().Equal(new FlowWarning("BALANCE_DUE", "Khách còn nợ 2.000.000 ₫", "warning"));
    }

    [Fact]
    public void Cancelled_project_has_no_suggestion_and_no_current_step()
    {
        var s = Eval(Facts("CANCELLED", brief: true, deliverables: [D("NOT_STARTED")], requests: [Guid.NewGuid()]));

        s.Stage.Should().Be("CANCELLED");
        StepStatuses(s).Should().Equal("done", "upcoming", "upcoming", "upcoming", "upcoming");
        s.Actions.Should().BeEmpty();
        s.Warnings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("UNPAID", 2_000_000)]
    [InlineData("DEPOSIT_PAID", 1_000_000)]
    [InlineData("PAID", 0)]
    public void Outstanding_follows_br_73(string payment, long expected) =>
        ProjectFlow.Outstanding(Q("ACCEPTED", payment: payment)).Should().Be(expected);

    [Fact]
    public void Client_acceptance_replaces_record_acceptance_with_a_confirmation_first()
    {
        var sent = Q("SENT") with { ClientAcceptedAt = DateTimeOffset.UnixEpoch };

        var s = Eval(Facts("QUOTED", true, [D("NOT_STARTED")], [sent]));

        s.Stage.Should().Be("QUOTE");
        Kinds(s).Should().Equal("CONFIRM_CLIENT_ACCEPTANCE", "COPY_QUOTATION_MESSAGE");
        s.Actions[0].Label.Should().Be("Khách đã đồng ý báo giá BG-2026-0001 (v1) — xác nhận");
        s.Actions[0].Target.QuotationId.Should().Be(sent.Id);
        OnlyFirstIsPrimary(s);
    }

    [Fact]
    public void Pending_payment_notice_is_the_first_action_until_the_designer_marks_it()
    {
        var accepted = Q("ACCEPTED") with { PaymentNoticePurpose = "deposit" };

        var execute = Eval(Facts("IN_PROGRESS", true, [D("IN_PROGRESS")], [accepted]));
        var marked = Eval(Facts("IN_PROGRESS", true, [D("IN_PROGRESS")], [accepted with { PaymentStatus = "DEPOSIT_PAID" }]));
        var completed = Eval(Facts("COMPLETED", true, [D("DELIVERED")],
            [Q("ACCEPTED", payment: "DEPOSIT_PAID") with { PaymentNoticePurpose = "remaining" }]));

        execute.Actions[0].Kind.Should().Be("CONFIRM_CLIENT_PAYMENT");
        execute.Actions[0].Label.Should().Be("Khách báo đã chuyển cọc 1.000.000 ₫ cho BG-2026-0001 (v1) — kiểm tra & xác nhận");
        OnlyFirstIsPrimary(execute);
        Kinds(marked).Should().NotContain("CONFIRM_CLIENT_PAYMENT");
        completed.Actions[0].Label.Should().Be("Khách báo đã chuyển phần còn lại 1.000.000 ₫ cho BG-2026-0001 (v1) — kiểm tra & xác nhận");
    }

    [Theory]
    [InlineData(0, false, true)]
    [InlineData(2, false, false)]
    [InlineData(0, true, false)]
    [InlineData(1, true, false)]
    public void Confirm_approval_is_offered_only_while_the_client_has_no_open_change(int openRevisions, bool newRequest, bool offered)
    {
        var d = D("IN_REVIEW", decision: "APPROVED", openRevisions: openRevisions) with { HasNewRequest = newRequest };

        var s = Eval(Facts("IN_PROGRESS", true, [d], [Q("ACCEPTED", payment: "PAID")]));

        s.Stage.Should().Be("REVIEW");
        Kinds(s).Contains("CONFIRM_APPROVAL").Should().Be(offered);
    }

    [Fact]
    public void Deliverable_in_review_with_open_revision_items_warns_the_designer()
    {
        var s = Eval(Facts("IN_PROGRESS", true, [D("IN_REVIEW", openRevisions: 2)], [Q("ACCEPTED", payment: "PAID")]));

        s.Stage.Should().Be("REVIEW");
        s.Warnings.Should().Contain(new FlowWarning("REVIEW_WITH_OPEN_REVISIONS",
            "Logo đang chờ khách duyệt nhưng còn 2 mục sửa chưa xong — khách chưa duyệt được trên portal", "warning"));
    }

    [Fact]
    public void Prepare_without_deliverables_does_not_offer_to_create_a_quotation()
    {
        var s = Eval(Facts());
        Kinds(s).Should().Equal("ADD_DELIVERABLE", "CONFIRM_BRIEF");
        s.Actions[0].Primary.Should().BeTrue();
    }

    [Fact]
    public void Prepare_with_a_deliverable_still_offers_the_quotation()
    {
        var s = Eval(Facts(deliverables: [D("NOT_STARTED")]));
        Kinds(s).Should().Equal("CONFIRM_BRIEF", "CREATE_QUOTATION");
    }

    [Theory]
    [InlineData("DRAFT", true)]
    [InlineData("QUOTED", false)]
    public void Sent_quotation_offers_mark_quoted_only_while_project_is_draft(string projectStatus, bool offered)
    {
        var s = Eval(Facts(projectStatus, brief: true, deliverables: [D("NOT_STARTED")], quotations: [Q("SENT")]));
        s.Stage.Should().Be("QUOTE");
        Kinds(s).Contains("MARK_QUOTED").Should().Be(offered);
        if (offered) s.Actions.Single(a => a.Kind == "MARK_QUOTED").Primary.Should().BeFalse();
    }

    [Fact]
    public void Started_project_offers_view_tasks_after_submit_review_actions()
    {
        var s = Eval(Facts("IN_PROGRESS", true,
            [D("IN_PROGRESS", total: 2, done: 2), D("IN_PROGRESS", "Banner", total: 3, done: 1)],
            [Q("ACCEPTED", payment: "PAID")], openTasks: 4));
        Kinds(s).Should().Equal("SUBMIT_REVIEW", "VIEW_TASKS");
        OnlyFirstIsPrimary(s);
    }

    [Fact]
    public void Started_project_without_open_tasks_has_no_view_tasks()
    {
        var s = Eval(Facts("IN_PROGRESS", true, [D("IN_PROGRESS")], [Q("ACCEPTED", payment: "PAID")], openTasks: 0));
        Kinds(s).Should().NotContain("VIEW_TASKS");
    }

    [Fact]
    public void Unpaid_deposit_offers_record_deposit_before_the_reminder_with_the_fixed_label()
    {
        var accepted = Q("ACCEPTED", depositPercent: 30);
        var s = Eval(Facts("IN_PROGRESS", true, [D("IN_PROGRESS")], [accepted], openTasks: 1));
        Kinds(s).Should().Equal("VIEW_TASKS", "RECORD_DEPOSIT", "COPY_PAYMENT_REMINDER");
        s.Actions.Single(a => a.Kind == "RECORD_DEPOSIT").Label.Should().Be("Đánh dấu đã nhận cọc");
        s.Actions.Single(a => a.Kind == "RECORD_DEPOSIT").Target.QuotationId.Should().Be(accepted.Id);
        s.Actions.Single(a => a.Kind == "COPY_PAYMENT_REMINDER").Label.Should().Be("Sao chép tin nhắn nhắc thanh toán");
    }

    [Fact]
    public void Unpaid_without_deposit_percent_says_payment_not_deposit()
    {
        var s = Eval(Facts("IN_PROGRESS", true, [D("IN_PROGRESS")], [Q("ACCEPTED", depositPercent: 0, deposit: 0)]));
        s.Actions.Single(a => a.Kind == "RECORD_DEPOSIT").Label.Should().Be("Đánh dấu đã nhận thanh toán");
    }

    [Fact]
    public void Completed_project_never_suggests_going_back_and_lists_open_items_as_info()
    {
        var paid = new[] { Q("ACCEPTED", payment: "PAID") };
        var s = Eval(Facts("COMPLETED", true, [D("DELIVERED", openRevisions: 2)], paid, openTasks: 4));
        s.Warnings.Select(w => w.Kind).Should().NotContain("DELIVERED_WITH_OPEN_REVISIONS");
        s.Warnings.Should().ContainSingle(w => w.Kind == "COMPLETED_WITH_OPEN_ITEMS" && w.Severity == "info"
            && w.Message == "Dự án đã hoàn thành nhưng còn 4 công việc và 2 mục sửa chưa xong — mở lại dự án nếu cần làm tiếp");
        s.Warnings.Select(w => w.Message).Should().NotContain(m => m.Contains("chuyển về Đang làm"));
    }

    [Fact]
    public void Completed_project_with_nothing_open_has_no_open_items_warning()
    {
        var s = Eval(Facts("COMPLETED", true, [D("DELIVERED")], [Q("ACCEPTED", payment: "PAID")]));
        s.Warnings.Select(w => w.Kind).Should().NotContain("COMPLETED_WITH_OPEN_ITEMS");
    }

    [Theory]   // P09: an accepted NEW_DELIVERABLE in a COMPLETED project adds a NOT_STARTED deliverable (no tasks)
    [InlineData("NOT_STARTED", 0, "Dự án đã hoàn thành nhưng còn 1 hạng mục chưa xong — mở lại dự án nếu cần làm tiếp")]
    [InlineData("IN_PROGRESS", 3, "Dự án đã hoàn thành nhưng còn 1 hạng mục và 3 công việc chưa xong — mở lại dự án nếu cần làm tiếp")]
    public void Completed_project_counts_a_deliverable_that_is_not_delivered(string status, int openTasks, string message)
    {
        var s = Eval(Facts("COMPLETED", true, [D("DELIVERED"), D(status, "Sticker"), D("CANCELLED", "Bỏ")],
            [Q("ACCEPTED", payment: "PAID")], openTasks: openTasks));
        s.Warnings.Should().ContainSingle(w => w.Kind == "COMPLETED_WITH_OPEN_ITEMS")
            .Which.Should().Be(new FlowWarning("COMPLETED_WITH_OPEN_ITEMS", message, "info"));
    }

    [Fact]
    public void Completed_project_joins_three_kinds_of_open_items()
    {
        var s = Eval(Facts("COMPLETED", true, [D("IN_PROGRESS", openRevisions: 1)], [Q("ACCEPTED", payment: "PAID")], openTasks: 2));
        s.Warnings.Single(w => w.Kind == "COMPLETED_WITH_OPEN_ITEMS").Message.Should()
            .Be("Dự án đã hoàn thành nhưng còn 1 hạng mục, 2 công việc và 1 mục sửa chưa xong — mở lại dự án nếu cần làm tiếp");
    }
}
