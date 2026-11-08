using FluentAssertions;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FreelanceFlow.UnitTests.Infrastructure.Storage;

public class StorageRegistrationTests
{
    [Fact]
    public void Binds_the_storage_section_and_registers_local_storage()
    {
        var root = Path.Combine(Path.GetTempPath(), "ff-reg-" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Local:RootPath"] = root,
            ["Storage:QuotaBytes"] = "5000",
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(Path.GetTempPath()));

        services.AddFileStorage(config);
        using var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
        options.QuotaBytes.Should().Be(5000);
        options.ReconcileIntervalHours.Should().Be(24);
        var storage = sp.GetRequiredService<IFileStorage>().Should().BeOfType<LocalFileStorage>().Subject;
        storage.RootPath.Should().Be(Path.GetFullPath(root));
        Directory.Delete(root);
    }

    [Fact]
    public void Defaults_apply_without_a_storage_section()
    {
        var services = new ServiceCollection();
        services.AddFileStorage(new ConfigurationBuilder().Build());
        using var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
        options.QuotaBytes.Should().Be(10_737_418_240);
        options.Local.RootPath.Should().Be("./data/files");
    }
}
