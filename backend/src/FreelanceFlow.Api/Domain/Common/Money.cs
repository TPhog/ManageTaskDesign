namespace FreelanceFlow.Api.Domain.Common;

/// <summary>Money is whole VND in <c>long</c> (BR-02); only percentages need rounding.</summary>
public static class Money
{
    /// <summary>
    /// BR-03: <paramref name="percent"/>% of <paramref name="amount"/>, rounded half-up (AwayFromZero) to the đồng.
    /// decimal is only the intermediate product so the midpoint is exact; amounts stay long.
    /// </summary>
    public static long Percent(long amount, int percent) =>
        (long)Math.Round(amount * (decimal)percent / 100m, MidpointRounding.AwayFromZero);
}
