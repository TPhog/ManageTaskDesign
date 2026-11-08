using System.Globalization;
using FreelanceFlow.Api.Domain.Projects;
using FreelanceFlow.Api.Domain.Quotations;
using FreelanceFlow.Api.Domain.Revisions;

namespace FreelanceFlow.Api.Domain.Flow;

public static class FlowStages
{
    public const string Prepare = "PREPARE";
    public const string Quote = "QUOTE";
    public const string Execute = "EXECUTE";
    public const string Review = "REVIEW";
    public const string Close = "CLOSE";
    /// <summary>Not a step: the stage reported for a CANCELLED project.</summary>
    public const string Cancelled = "CANCELLED";
    public static readonly string[] Ordered = [Prepare, Quote, Execute, Review, Close];
}

public static class FlowStepStatuses
{
    public const string Done = "done";
    public const string Current = "current";
    public const string Upcoming = "upcoming";
}

public static class FlowSeverities
{
    public const string Warning = "warning";
    public const string Info = "info";
}

public static class FlowActionKinds
{
    public const string AddDeliverable = "ADD_DELIVERABLE";
    public const string ConfirmBrief = "CONFIRM_BRIEF";
    public const string CreateQuotation = "CREATE_QUOTATION";
    public const string SendQuotation = "SEND_QUOTATION";
    public const string CopyQuotationMessage = "COPY_QUOTATION_MESSAGE";
    public const string RecordAcceptance = "RECORD_ACCEPTANCE";
    public const string StartProject = "START_PROJECT";
    public const string SubmitReview = "SUBMIT_REVIEW";
    public const string ConfirmApproval = "CONFIRM_APPROVAL";
    public const string TriageRequest = "TRIAGE_REQUEST";
    public const string Deliver = "DELIVER";
    public const string CreateAdditionalQuotation = "CREATE_ADDITIONAL_QUOTATION";
    public const string CompleteProject = "COMPLETE_PROJECT";
    public const string CopyPaymentReminder = "COPY_PAYMENT_REMINDER";
    public const string ConfirmClientAcceptance = "CONFIRM_CLIENT_ACCEPTANCE";
    public const string ConfirmClientPayment = "CONFIRM_CLIENT_PAYMENT";
}

public static class FlowWarningKinds
{
    public const string BriefNotConfirmed = "BRIEF_NOT_CONFIRMED";
    public const string QuotationExpiring = "QUOTATION_EXPIRING";
    public const string QuotationExpired = "QUOTATION_EXPIRED";
    public const string DepositUnpaid = "DEPOSIT_UNPAID";
    public const string TasksOverdue = "TASKS_OVERDUE";
    public const string QuotaExceeded = "QUOTA_EXCEEDED";
    public const string BalanceDue = "BALANCE_DUE";
    public const string DeliveredWithOpenRevisions = "DELIVERED_WITH_OPEN_REVISIONS";
    public const string ReviewWithOpenRevisions = "REVIEW_WITH_OPEN_REVISIONS";
}

/// <summary>
/// A deliverable as the flow sees it (TaskTotal/TaskDone from TaskProgress, UsedRounds from RevisionQuota).
/// HasNewRequest (Plan 12 BR-48): a NEW client request is tied to it.
/// </summary>
public sealed record FlowDeliverable(
    Guid Id, string Name, string Status, int TaskTotal, int TaskDone, string? ClientDecision, int IncludedRounds, int UsedRounds,
    int OpenRevisionItems = 0, bool HasNewRequest = false);

/// <summary>A non-SUPERSEDED quotation of the project.</summary>
public sealed record FlowQuotation(
    Guid Id, string Kind, string Status, string? Number, int Version, DateOnly? ExpiryDate,
    string PaymentStatus, int DepositPercent, long Total, long DepositAmount, DateTimeOffset CreatedAt,
    DateTimeOffset? ClientAcceptedAt = null, string? PaymentNoticePurpose = null);

/// <summary>Everything Evaluate needs, loaded by Features/Projects/ProjectFlowReads. NewRequestIds: oldest first.</summary>
public sealed record FlowFacts(
    string ProjectStatus,
    bool BriefConfirmed,
    IReadOnlyList<FlowDeliverable> Deliverables,
    IReadOnlyList<FlowQuotation> Quotations,
    int OverdueTasks,
    IReadOnlyList<Guid> NewRequestIds,
    int UnquotedChargeableItems);

public sealed record FlowTarget(Guid? DeliverableId = null, Guid? QuotationId = null, Guid? RequestId = null);

public sealed record FlowAction(string Kind, string Label, bool Primary, FlowTarget Target);

public sealed record FlowWarning(string Kind, string Message, string Severity);

public sealed record FlowStep(string Stage, string Status);

public sealed record FlowState(string Stage, IReadOnlyList<FlowStep> Steps, IReadOnlyList<FlowAction> Actions, IReadOnlyList<FlowWarning> Warnings);

/// <summary>
/// Plan 06b F1/F6: the project stage is derived, never stored. Pure — no DB, no clock (today is passed in).
/// Only suggests (BR-01): every action is a button the designer presses; warnings never block (F4).
/// </summary>
public static class ProjectFlow
{
    public const int ExpiringWithinDays = 3;

    public static FlowState Evaluate(FlowFacts facts, DateOnly today)
    {
        var active = facts.Deliverables.Where(d => d.Status != "CANCELLED").ToList();
        var initial = facts.Quotations.Where(q => q.Kind == "INITIAL").ToList();
        bool[] complete =
        [
            active.Count > 0 && facts.BriefConfirmed,
            initial.Any(q => q.Status == "ACCEPTED"),
            active.Count > 0 && active.All(d => d.Status is "IN_REVIEW" or "APPROVED" or "DELIVERED"),
            active.Count > 0 && active.All(d => d.Status == "DELIVERED"),
            facts.ProjectStatus == "COMPLETED",
        ];
        var firstOpen = Array.IndexOf(complete, false);
        var b = new Builder();

        if (facts.ProjectStatus == "CANCELLED")
            return b.State(FlowStages.Cancelled, Steps(firstOpen, markCurrent: false));

        if (facts.ProjectStatus == "COMPLETED")
        {
            ClientStatementActions(b, facts);
            DeliveredWithOpenRevisions(b, active);
            Balance(b, facts);
            return b.State(FlowStages.Close, Steps(-1, markCurrent: false));
        }

        var stage = FlowStages.Ordered[firstOpen];
        if (stage is FlowStages.Execute or FlowStages.Review or FlowStages.Close)
            DeliveredWithOpenRevisions(b, active);
        ClientStatementActions(b, facts);
        switch (stage)
        {
            case FlowStages.Prepare: Prepare(b, facts, active, initial); break;
            case FlowStages.Quote: Quote(b, initial, today); break;
            case FlowStages.Execute: Execute(b, facts, active, initial); break;
            case FlowStages.Review: Review(b, facts, active); break;
            default: Close(b, facts); break;
        }
        return b.State(stage, Steps(firstOpen, markCurrent: true));
    }

    /// <summary>BR-73 on the facts — same rule as Quotation.OutstandingAmount.</summary>
    public static long Outstanding(FlowQuotation q) => q.Status != "ACCEPTED" ? 0 : q.PaymentStatus switch
    {
        "PAID" => 0,
        "DEPOSIT_PAID" => q.Total - q.DepositAmount,
        _ => q.Total,
    };

    private static void Prepare(Builder b, FlowFacts f, List<FlowDeliverable> active, List<FlowQuotation> initial)
    {
        if (active.Count == 0) b.Act(FlowActionKinds.AddDeliverable, "Thêm hạng mục");
        if (!f.BriefConfirmed)
        {
            b.Act(FlowActionKinds.ConfirmBrief, "Xác nhận brief");
            b.Warn(FlowWarningKinds.BriefNotConfirmed, "Brief chưa được xác nhận", FlowSeverities.Info);
        }
        if (!initial.Any(q => q.Status is "DRAFT" or "SENT" or "ACCEPTED"))
            b.Act(FlowActionKinds.CreateQuotation, "Tạo báo giá");
    }

    private static void Quote(Builder b, List<FlowQuotation> initial, DateOnly today)
    {
        var draft = Newest(initial, "DRAFT");
        var sent = Newest(initial, "SENT");
        if (draft != null)
            b.Act(FlowActionKinds.SendQuotation, "Gửi báo giá", new FlowTarget(QuotationId: draft.Id));
        if (sent != null)
        {
            if (sent.ClientAcceptedAt is null)
                b.Act(FlowActionKinds.RecordAcceptance, "Ghi nhận khách chấp nhận báo giá", new FlowTarget(QuotationId: sent.Id));
            b.Act(FlowActionKinds.CopyQuotationMessage, "Sao chép tin nhắn báo giá", new FlowTarget(QuotationId: sent.Id));
            if (sent.ExpiryDate is { } expiry)
            {
                var days = expiry.DayNumber - today.DayNumber;
                if (days < 0)
                    b.Warn(FlowWarningKinds.QuotationExpired,
                        $"Báo giá {Ref(sent)} đã hết hạn ngày {expiry.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}",
                        FlowSeverities.Warning);
                else if (days <= ExpiringWithinDays)
                    b.Warn(FlowWarningKinds.QuotationExpiring,
                        days == 0 ? $"Báo giá {Ref(sent)} hết hạn hôm nay" : $"Báo giá {Ref(sent)} còn {days} ngày là hết hạn",
                        FlowSeverities.Warning);
            }
        }
        if (draft == null && sent == null) b.Act(FlowActionKinds.CreateQuotation, "Tạo báo giá");
    }

    private static void Execute(Builder b, FlowFacts f, List<FlowDeliverable> active, List<FlowQuotation> initial)
    {
        if (f.ProjectStatus is "DRAFT" or "QUOTED") b.Act(FlowActionKinds.StartProject, "Bắt đầu dự án");
        foreach (var d in active.Where(d => d.Status == "IN_PROGRESS" && d.TaskTotal > 0 && d.TaskDone == d.TaskTotal))
            b.Act(FlowActionKinds.SubmitReview, $"Gửi duyệt: {d.Name}", new FlowTarget(DeliverableId: d.Id));
        RevisionFollowUps(b, f, active);
        ReviewBlockedByRevisions(b, active);

        var unpaid = initial.Where(q => q.Status == "ACCEPTED" && q.PaymentStatus == "UNPAID")
            .OrderBy(q => q.CreatedAt).FirstOrDefault();
        if (unpaid != null)
        {
            b.Warn(FlowWarningKinds.DepositUnpaid,
                unpaid.DepositPercent > 0
                    ? $"Chưa nhận cọc {RevisionQuota.Vnd(unpaid.DepositAmount)} của báo giá {Ref(unpaid)}"
                    : $"Chưa nhận thanh toán báo giá {Ref(unpaid)}",
                FlowSeverities.Warning);
            b.Act(FlowActionKinds.CopyPaymentReminder, "Sao chép tin nhắc thanh toán", new FlowTarget(QuotationId: unpaid.Id));
        }
        if (f.OverdueTasks > 0)
            b.Warn(FlowWarningKinds.TasksOverdue, $"{f.OverdueTasks} công việc quá hạn", FlowSeverities.Warning);
        QuotaWarnings(b, active);
    }

    private static void Review(Builder b, FlowFacts f, List<FlowDeliverable> active)
    {
        // Plan 12 BR-48: same single state as the portal — an approval followed by an open change is not confirmable
        foreach (var d in active.Where(d => ClientReviewStates.Of(d.Status, d.ClientDecision,
                     d.OpenRevisionItems > 0 || d.HasNewRequest) == ClientReviewStates.ClientApproved))
            b.Act(FlowActionKinds.ConfirmApproval, $"Xác nhận khách đã duyệt: {d.Name}", new FlowTarget(DeliverableId: d.Id));
        var approved = active.Where(d => d.Status == "APPROVED").ToList();
        foreach (var d in approved)
            b.Act(FlowActionKinds.Deliver, $"Bàn giao: {d.Name}", new FlowTarget(DeliverableId: d.Id));
        RevisionFollowUps(b, f, active);
        ReviewBlockedByRevisions(b, active);
        QuotaWarnings(b, active);
        if (approved.Count > 0) Balance(b, f);   // F4: money still due when handing over
    }

    private static void Close(Builder b, FlowFacts f)
    {
        if (f.ProjectStatus == "IN_PROGRESS") b.Act(FlowActionKinds.CompleteProject, "Hoàn thành dự án");
        Balance(b, f);
    }

    /// <summary>Revision loop (IN_REVIEW → request → revision → IN_PROGRESS → IN_REVIEW): shown in EXECUTE and REVIEW.</summary>
    private static void RevisionFollowUps(Builder b, FlowFacts f, List<FlowDeliverable> active)
    {
        if (f.NewRequestIds.Count > 0)
            b.Act(FlowActionKinds.TriageRequest,
                f.NewRequestIds.Count == 1 ? "Xử lý yêu cầu mới của khách" : $"Xử lý {f.NewRequestIds.Count} yêu cầu mới của khách",
                new FlowTarget(RequestId: f.NewRequestIds[0]));
        if (f.UnquotedChargeableItems > 0)
            b.Act(FlowActionKinds.CreateAdditionalQuotation, $"Tạo báo giá phát sinh ({f.UnquotedChargeableItems} mục tính phí)");
    }

    /// <summary>Plan 12 BR-76/77: what the client stated on the portal waits for the designer's confirmation — first, so it is primary.</summary>
    private static void ClientStatementActions(Builder b, FlowFacts f)
    {
        foreach (var q in f.Quotations.Where(q => q.Status == "SENT" && q.ClientAcceptedAt != null).OrderBy(q => q.CreatedAt))
            b.Act(FlowActionKinds.ConfirmClientAcceptance, $"Khách đã đồng ý báo giá {Ref(q)} — xác nhận",
                new FlowTarget(QuotationId: q.Id));
        foreach (var q in f.Quotations.Where(q => ClientStatements.IsPending(q.Status, q.PaymentStatus, q.PaymentNoticePurpose))
                     .OrderBy(q => q.CreatedAt))
        {
            var amount = RevisionQuota.Vnd(ClientStatements.Amount(q.PaymentNoticePurpose!, q.Total, q.DepositAmount));
            var what = q.PaymentNoticePurpose switch
            {
                QrPurposes.Deposit => $"cọc {amount}",
                QrPurposes.Remaining => $"phần còn lại {amount}",
                _ => $"đủ {amount}",
            };
            b.Act(FlowActionKinds.ConfirmClientPayment, $"Khách báo đã chuyển {what} cho {Ref(q)} — kiểm tra & xác nhận",
                new FlowTarget(QuotationId: q.Id));
        }
    }

    /// <summary>Plan 12 BR-48: the portal blocks "Duyệt" while revision items of the deliverable are still open.</summary>
    private static void ReviewBlockedByRevisions(Builder b, List<FlowDeliverable> active)
    {
        foreach (var d in active.Where(d => d.Status == "IN_REVIEW" && d.OpenRevisionItems > 0))
            b.Warn(FlowWarningKinds.ReviewWithOpenRevisions,
                $"{d.Name} đang chờ khách duyệt nhưng còn {d.OpenRevisionItems} mục sửa chưa xong — khách chưa duyệt được trên portal",
                FlowSeverities.Warning);
    }

    /// <summary>D-11: a DELIVERED deliverable that still has TODO / IN_PROGRESS revision items should go back to IN_PROGRESS.</summary>
    private static void DeliveredWithOpenRevisions(Builder b, List<FlowDeliverable> active)
    {
        foreach (var d in active.Where(d => d.Status == "DELIVERED" && d.OpenRevisionItems > 0))
            b.Warn(FlowWarningKinds.DeliveredWithOpenRevisions,
                $"{d.Name} đã bàn giao nhưng còn {d.OpenRevisionItems} mục sửa chưa xong — chuyển về Đang làm",
                FlowSeverities.Warning);
    }

    private static void QuotaWarnings(Builder b, List<FlowDeliverable> active)
    {
        foreach (var d in active.Where(d => d.UsedRounds > 0 && d.UsedRounds >= d.IncludedRounds))
            b.Warn(FlowWarningKinds.QuotaExceeded, $"{d.Name} đã dùng hết {d.IncludedRounds} vòng sửa trong quota", FlowSeverities.Info);
    }

    private static void Balance(Builder b, FlowFacts f)
    {
        var due = f.Quotations.Where(q => Outstanding(q) > 0).OrderBy(q => q.CreatedAt).ToList();
        if (due.Count == 0) return;
        b.Warn(FlowWarningKinds.BalanceDue, $"Khách còn nợ {RevisionQuota.Vnd(due.Sum(Outstanding))}", FlowSeverities.Warning);
        b.Act(FlowActionKinds.CopyPaymentReminder, "Sao chép tin nhắc thanh toán", new FlowTarget(QuotationId: due[0].Id));
    }

    private static string Ref(FlowQuotation q) => q.Number is null ? "nháp" : $"{q.Number} (v{q.Version})";

    private static FlowQuotation? Newest(List<FlowQuotation> quotations, string status) =>
        quotations.Where(q => q.Status == status).OrderByDescending(q => q.CreatedAt).FirstOrDefault();

    /// <summary>firstOpen = -1 → every step done.</summary>
    private static IReadOnlyList<FlowStep> Steps(int firstOpen, bool markCurrent) =>
        FlowStages.Ordered.Select((stage, i) => new FlowStep(stage,
            firstOpen < 0 || i < firstOpen ? FlowStepStatuses.Done
            : i == firstOpen && markCurrent ? FlowStepStatuses.Current
            : FlowStepStatuses.Upcoming)).ToList();

    private sealed class Builder
    {
        private readonly List<(string Kind, string Label, FlowTarget Target)> _actions = [];
        private readonly List<FlowWarning> _warnings = [];

        public void Act(string kind, string label, FlowTarget? target = null) =>
            _actions.Add((kind, label, target ?? new FlowTarget()));

        public void Warn(string kind, string message, string severity) =>
            _warnings.Add(new FlowWarning(kind, message, severity));

        /// <summary>Exactly one primary action: the first one.</summary>
        public FlowState State(string stage, IReadOnlyList<FlowStep> steps) => new(
            stage,
            steps,
            _actions.Select((a, i) => new FlowAction(a.Kind, a.Label, i == 0, a.Target)).ToList(),
            _warnings);
    }
}
