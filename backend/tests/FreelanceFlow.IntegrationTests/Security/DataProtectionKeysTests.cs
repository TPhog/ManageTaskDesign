using System.Net;
using FreelanceFlow.IntegrationTests.Intake;
using Microsoft.AspNetCore.Hosting;

namespace FreelanceFlow.IntegrationTests.Security;

/// <summary>
/// Data Protection keys live in DataProtection:KeysPath (prod: the /data/keys volume), so an API restart keeps designer and
/// portal cookies and open public-form tokens valid. Two hosts sharing one folder stand for "before / after a restart".
/// </summary>
public class DataProtectionKeysTests(AppFactory factory) : IntakeTestBase(factory)
{
    [Fact]
    public async Task A_form_token_issued_before_a_restart_still_validates_after_it()
    {
        var slug = await EnableIntakeAsync();
        var keys = Path.Combine(Path.GetTempPath(), "ff-it-keys", Guid.NewGuid().ToString("N"));
        try
        {
            string token;
            await using (var before = Factory.WithWebHostBuilder(b => b.UseSetting("DataProtection:KeysPath", keys)))
                token = (await GetFormAsync(Anonymous(before), slug)).GetProperty("formToken").GetString()!;

            await using var after = Factory.WithWebHostBuilder(b => b.UseSetting("DataProtection:KeysPath", keys));
            var resp = await Anonymous(after).PostAsync($"/api/public/intake/{slug}", IntakeForm(token, note: "Sau khi khởi động lại"));

            resp.StatusCode.Should().Be(HttpStatusCode.NoContent, await resp.Content.ReadAsStringAsync());
            Directory.GetFiles(keys, "key-*.xml").Should().NotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(keys)) Directory.Delete(keys, recursive: true);
        }
    }
}
