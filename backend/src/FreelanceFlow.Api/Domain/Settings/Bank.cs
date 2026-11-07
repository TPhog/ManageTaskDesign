namespace FreelanceFlow.Api.Domain.Settings;

public class Bank
{
    public string Bin { get; set; } = "";
    public string Code { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
}
