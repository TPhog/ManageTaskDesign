namespace FreelanceFlow.Api.Domain.Quotations;

public static class QuotationStatuses
{
    public const string Draft = "DRAFT";
    public const string Sent = "SENT";
    public const string Accepted = "ACCEPTED";
    public const string Rejected = "REJECTED";
    public const string Superseded = "SUPERSEDED";

    /// <summary>Never stored: effective status of a SENT quotation past its expiry_date (§8.1, ADR-009).</summary>
    public const string Expired = "EXPIRED";
}

public static class PaymentStatuses
{
    public const string Unpaid = "UNPAID";
    public const string DepositPaid = "DEPOSIT_PAID";
    public const string Paid = "PAID";
    public static readonly string[] All = [Unpaid, DepositPaid, Paid];
}

public static class QuotationKinds
{
    public const string Initial = "INITIAL";
    public const string Additional = "ADDITIONAL";
}

public static class QuotationItemSources
{
    public const string Manual = "MANUAL";
    public const string Service = "SERVICE";
    public const string Deliverable = "DELIVERABLE";
    public const string Revision = "REVISION";
    public static readonly string[] All = [Manual, Service, Deliverable, Revision];
}

/// <summary>QR purposes of docs/05 §3.10 (<c>/qr/{purpose}</c>).</summary>
public static class QrPurposes
{
    public const string Deposit = "deposit";
    public const string Full = "full";
    public const string Remaining = "remaining";
}
