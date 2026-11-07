using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace FreelanceFlow.IntegrationTests;

public sealed class AppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestEmail = "designer@test.local";
    public const string TestPassword = "testpassword1234";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("freelanceflow_test")
        .WithUsername("ff")
        .WithPassword("changeme")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _db.GetConnectionString(),
                ["Seed:DesignerEmail"] = TestEmail,
                ["Seed:DesignerPassword"] = TestPassword,
                ["App:PublicBaseUrl"] = "http://localhost",
            }));

    public async Task InitializeAsync() => await _db.StartAsync();

    public new async Task DisposeAsync()
    {
        await _db.StopAsync();
        await base.DisposeAsync();
    }
}
