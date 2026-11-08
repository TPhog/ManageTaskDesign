using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FreelanceFlow.IntegrationTests.Smoke;

public class PublicBaseUrlStartupTests
{
    [Theory]
    [InlineData("")]
    [InlineData("http://example.com")]
    [InlineData("not-a-url")]
    public void Production_startup_fails_without_absolute_https_public_base_url(string value)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=unused");
            b.UseSetting("App:PublicBaseUrl", value);
        });

        var act = () => factory.CreateClient();

        act.Should().Throw<InvalidOperationException>().WithMessage("*PublicBaseUrl*");
    }
}
