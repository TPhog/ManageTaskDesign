namespace FreelanceFlow.Api.Domain.Quotations;

/// <summary>Plan 12 BR-77: "Tôi đã chuyển khoản" as the client said it — the designer still marks the payment (BR-70).</summary>
public sealed record ClientPaymentNotice(string Purpose, long Amount, DateTimeOffset At, string? Note, bool Pending);

/// <summary>Plan 12 BR-76/77. Purposes are the QR purposes of BR-75 / docs/06 (no new vocabulary).</summary>
public static class ClientStatements
{
    public const int MaxNote = 500;

    public static readonly string[] Purposes = [QrPurposes.Deposit, QrPurposes.Full, QrPurposes.Remaining];

    /// <summary>Still waiting for the designer: the payment status has not reached what the client reported.</summary>
    public static bool IsPending(string status, string paymentStatus, string? purpose) =>
        (status is QuotationStatuses.Sent or QuotationStatuses.Accepted) && purpose switch
        {
            QrPurposes.Deposit => paymentStatus == PaymentStatuses.Unpaid,
            QrPurposes.Full => paymentStatus != PaymentStatuses.Paid,
            QrPurposes.Remaining => paymentStatus == PaymentStatuses.DepositPaid,
            _ => false,
        };

    public static long Amount(string purpose, long total, long depositAmount) => purpose switch
    {
        QrPurposes.Deposit => depositAmount,
        QrPurposes.Remaining => total - depositAmount,
        _ => total,
    };

    public static string PurposeText(string purpose) => purpose switch
    {
        QrPurposes.Deposit => "tiền cọc",
        QrPurposes.Remaining => "phần còn lại",
        _ => "toàn bộ",
    };
}
