using FluentAssertions;
using FreelanceFlow.Api.Infrastructure.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FreelanceFlow.UnitTests.Infrastructure.Storage;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ff-unit-" + Guid.NewGuid().ToString("N"));

    private LocalFileStorage Storage(string? rootPath = null) =>
        new(Options.Create(new StorageOptions { Local = new LocalStorageOptions { RootPath = rootPath ?? _root } }),
            new TestHostEnvironment(_root));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Put_open_and_delete_round_trip()
    {
        var storage = Storage();
        const string key = "projects/p1/f1/original.webp";

        await storage.PutAsync(key, [1, 2, 3], "image/webp");
        await using (var stream = await storage.OpenReadAsync(key))
        {
            stream.Should().NotBeNull();
            using var copy = new MemoryStream();
            await stream!.CopyToAsync(copy);
            copy.ToArray().Should().Equal(1, 2, 3);
        }
        (await storage.TotalBytesAsync("")).Should().Be(3);
        Directory.EnumerateFiles(_root, "*.tmp-*", SearchOption.AllDirectories).Should().BeEmpty("writes are temp-then-rename");

        await storage.DeleteAsync(key);
        (await storage.OpenReadAsync(key)).Should().BeNull();
        Directory.Exists(Path.Combine(_root, "projects", "p1", "f1")).Should().BeFalse("the empty image folder is removed");
        await storage.DeleteAsync(key); // idempotent
    }

    [Fact]
    public async Task Put_replaces_an_existing_file_atomically()
    {
        var storage = Storage();
        await storage.PutAsync("projects/p/f/thumb.webp", [1, 1, 1, 1], "image/webp");
        await storage.PutAsync("projects/p/f/thumb.webp", [2], "image/webp");

        (await File.ReadAllBytesAsync(Path.Combine(_root, "projects", "p", "f", "thumb.webp"))).Should().Equal(2);
    }

    [Fact]
    public async Task Total_bytes_counts_a_prefix_only_and_ignores_temp_files()
    {
        var storage = Storage();
        await storage.PutAsync("projects/a/f/thumb.webp", new byte[10], "image/webp");
        await storage.PutAsync("projects/b/f/thumb.webp", new byte[5], "image/webp");
        await File.WriteAllBytesAsync(Path.Combine(_root, "projects", "b", "f", "thumb.webp.tmp-abc"), new byte[100]);

        (await storage.TotalBytesAsync("projects/a/")).Should().Be(10);
        (await storage.TotalBytesAsync("projects/")).Should().Be(15);
        (await storage.TotalBytesAsync("projects/missing/")).Should().Be(0);
    }

    [Fact]
    public void Relative_root_resolves_against_the_content_root_and_is_created() =>
        Storage("./data/files").RootPath.Should().Be(Path.GetFullPath(Path.Combine(_root, "./data/files")))
            .And.Match(p => Directory.Exists(p));

    [Theory]
    [InlineData("../escape.webp")]
    [InlineData("projects/../../escape.webp")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("Projects/UPPER.webp")]
    [InlineData("projects\\p\\f.webp")]
    [InlineData("")]
    public async Task Keys_outside_the_root_or_with_odd_characters_are_rejected(string key)
    {
        var put = () => Storage().PutAsync(key, [1], "image/webp");
        await put.Should().ThrowAsync<ArgumentException>();
        var read = () => Storage().OpenReadAsync(key);
        await read.Should().ThrowAsync<ArgumentException>();
    }
}

internal sealed class TestHostEnvironment(string contentRoot) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Testing";
    public string ApplicationName { get; set; } = "FreelanceFlow.Api";
    public string ContentRootPath { get; set; } = contentRoot;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
