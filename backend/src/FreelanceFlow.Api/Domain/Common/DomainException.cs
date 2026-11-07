namespace FreelanceFlow.Api.Domain.Common;

public sealed class DomainException : Exception
{
    public string Code { get; }
    public string? Detail { get; }

    public DomainException(string code, string? detail = null) : base(detail ?? code)
    {
        Code = code;
        Detail = detail;
    }
}
