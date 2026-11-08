using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FreelanceFlow.IntegrationTests.Smoke;

// Regression guard: settings added through ConfigureAppConfiguration arrived after Program.cs had already
// read the connection string, so the suite silently ran against the dev database. AppFactory now uses
// builder.UseSetting; this test fails if the host ever talks to anything but the Testcontainers database.
public class AppFactoryGuardTests(AppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Host_uses_testcontainers_database_not_dev_database()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var actual = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        var expected = new NpgsqlConnectionStringBuilder(Factory.DatabaseConnectionString);
        actual.Host.Should().Be(expected.Host);
        actual.Port.Should().Be(expected.Port);
        actual.Database.Should().Be(AppFactory.TestDatabaseName);

        var current = await db.Database
            .SqlQuery<string>($"SELECT current_database() AS \"Value\"")
            .SingleAsync();
        current.Should().Be(AppFactory.TestDatabaseName);
    }
}
