using System.Net;
using System.Net.Http.Json;

namespace FreelanceFlow.IntegrationTests.Settings;

public class SettingsTests(AppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetBanks_Unauthenticated_Returns401()
    {
        var resp = await Client.GetAsync("/api/admin/banks");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBanks_Authenticated_Returns21Banks()
    {
        await LoginAsync();
        var resp = await Client.GetAsync("/api/admin/banks");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var banks = await resp.Content.ReadFromJsonAsync<List<BankDto>>();
        banks.Should().NotBeNull();
        banks!.Count.Should().BeGreaterThanOrEqualTo(21);
    }

    [Fact]
    public async Task GetSettings_Authenticated_ReturnsDefaultPrefix()
    {
        await LoginAsync();
        var resp = await Client.GetAsync("/api/admin/settings");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<SettingsResponse>();
        body.Should().NotBeNull();
        body!.QuotationPrefix.Should().Be("BG");
    }

    [Fact]
    public async Task UpdateSettings_ValidData_Returns204()
    {
        await LoginAsync();

        // GET to get current rowVersion
        var getResp = await Client.GetAsync("/api/admin/settings");
        var current = await getResp.Content.ReadFromJsonAsync<SettingsResponse>();

        var payload = new
        {
            displayName = "Test Studio",
            contactEmail = (string?)null,
            contactPhone = (string?)null,
            logoFileId = (Guid?)null,
            bankBin = (string?)null,
            bankAccountNo = (string?)null,
            bankAccountName = (string?)null,
            quotationPrefix = "TT",
            defaultValidityDays = 14,
            defaultDepositPercent = 30,
            defaultTerms = "",
            rowVersion = current!.RowVersion
        };

        var resp = await Client.PutAsJsonAsync("/api/admin/settings", payload);
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateSettings_WrongRowVersion_Returns409()
    {
        await LoginAsync();

        var payload = new
        {
            displayName = "Test Studio",
            contactEmail = (string?)null,
            contactPhone = (string?)null,
            logoFileId = (Guid?)null,
            bankBin = (string?)null,
            bankAccountNo = (string?)null,
            bankAccountName = (string?)null,
            quotationPrefix = "BG",
            defaultValidityDays = 7,
            defaultDepositPercent = 50,
            defaultTerms = "",
            rowVersion = 0L  // wrong rowVersion
        };

        var resp = await Client.PutAsJsonAsync("/api/admin/settings", payload);
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private record BankDto(string Bin, string Code, string ShortName, string Name);
    private record SettingsResponse(string DisplayName, string QuotationPrefix, long RowVersion);
}
