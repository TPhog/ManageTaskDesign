using System.Net;
using FreelanceFlow.IntegrationTests.Intake;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public async Task A_relative_keys_path_is_under_the_content_root_not_the_working_directory()
    {
        var slug = await EnableIntakeAsync();
        var relative = Path.Combine("data", "it-keys-" + Guid.NewGuid().ToString("N"));
        await using var host = Factory.WithWebHostBuilder(b => b.UseSetting("DataProtection:KeysPath", relative));
        var contentRoot = host.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
        var expected = Path.Combine(contentRoot, relative);
        try
        {
            await GetFormAsync(Anonymous(host), slug);   // issuing a token creates the first key

            Directory.GetFiles(expected, "key-*.xml").Should().NotBeEmpty();
            Path.GetFullPath(contentRoot).Should().NotBe(Path.GetFullPath(Directory.GetCurrentDirectory()),
                "otherwise this test proves nothing");
        }
        finally
        {
            if (Directory.Exists(expected)) Directory.Delete(expected, recursive: true);
            var wrong = Path.GetFullPath(relative);
            if (Directory.Exists(wrong)) Directory.Delete(wrong, recursive: true);
        }
    }

    private WebApplicationFactory<Program> ProductionHost(string keysPath) => Factory.WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Production");
        b.UseSetting("App:PublicBaseUrl", "https://studio.example.com");
        b.UseSetting("DataProtection:KeysPath", keysPath);
    });

    [Theory]   // AC-INT10, R2 M1: blank, or a folder that is not there (no keys volume) - never created silently
    [InlineData(false)]
    [InlineData(true)]
    public async Task Production_refuses_to_start_without_an_existing_keys_folder(bool setButMissing)
    {
        var path = setButMissing ? Path.Combine(Path.GetTempPath(), "ff-it-keys-missing", Guid.NewGuid().ToString("N")) : "";
        await using var host = ProductionHost(path);

        var start = () => host.Services;

        start.Should().Throw<InvalidOperationException>().WithMessage("*DataProtection:KeysPath*");
        if (setButMissing) Directory.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task Production_starts_with_an_existing_writable_keys_folder()
    {
        var keys = Path.Combine(Path.GetTempPath(), "ff-it-keys", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keys);
        try
        {
            await using var host = ProductionHost(keys);
            host.Services.Should().NotBeNull();
        }
        finally
        {
            Directory.Delete(keys, recursive: true);
        }
    }
}
