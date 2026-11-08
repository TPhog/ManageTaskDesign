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
    public async Task Root_with_trailing_separator_still_accepts_keys()
    {
        var storage = Storage(_root + Path.DirectorySeparatorChar);

        await storage.PutAsync("projects/p1/f1/thumb.webp", [1], "image/webp");

        await using (var stream = await storage.OpenReadAsync("projects/p1/f1/thumb.webp"))
            stream.Should().NotBeNull();
        storage.RootPath.Should().Be(_root);
    }

    [Fact]
    public void Unwritable_root_fails_fast_with_a_clear_message()
    {
        var file = Path.Combine(Path.GetTempPath(), "ff-unit-file-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(file, "x");
        try
        {
            var act = () => Storage(file);
            act.Should().Throw<Exception>();
        }
        finally { File.Delete(file); }
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

    [Fact]
    public async Task List_returns_committed_objects_with_sizes_and_skips_temp_files()
    {
        var storage = Storage();
        await storage.PutAsync("projects/p1/f1/original.webp", [1, 2, 3], "image/webp");
        await storage.PutAsync("projects/p1/f1/thumb.webp", [1], "image/webp");
        await storage.PutAsync("other/x.webp", [9], "image/webp");
        await File.WriteAllBytesAsync(Path.Combine(_root, "projects", "p1", "f1", "preview.webp.tmp-abc"), [7]);

        var listed = await storage.ListAsync("projects/");

        listed.Select(o => (o.Key, o.SizeBytes)).Should().BeEquivalentTo(new[]
        {
            ("projects/p1/f1/original.webp", 3L),
            ("projects/p1/f1/thumb.webp", 1L),
        });
        listed.Should().OnlyContain(o => o.LastModified > DateTimeOffset.UtcNow.AddMinutes(-5));
        (await storage.ListAsync("missing/")).Should().BeEmpty();
        (await storage.ListAsync("")).Should().HaveCount(3);
    }

    [Fact]
    public async Task Delete_many_removes_every_key_and_is_idempotent()
    {
        var storage = Storage();
        await storage.PutAsync("projects/p1/f1/original.webp", [1], "image/webp");
        await storage.PutAsync("projects/p1/f1/thumb.webp", [1], "image/webp");

        var failed = await storage.DeleteManyAsync(["projects/p1/f1/original.webp", "projects/p1/f1/thumb.webp", "projects/p1/f9/thumb.webp"]);

        failed.Should().BeEmpty();
        (await storage.ListAsync("projects/")).Should().BeEmpty();
        Directory.Exists(Path.Combine(_root, "projects", "p1", "f1")).Should().BeFalse("the image folder goes with its last variant");
        (await storage.DeleteManyAsync(["projects/p1/f1/original.webp"])).Should().BeEmpty();
    }

    [Fact]
    public async Task Purge_incomplete_removes_old_temp_files_and_old_empty_image_folders_only()
    {
        var storage = Storage();
        var f1 = Path.Combine(_root, "projects", "p1", "f1");
        var f2 = Path.Combine(_root, "projects", "p1", "f2");
        var f3 = Path.Combine(_root, "projects", "p1", "f3");
        Directory.CreateDirectory(f1);
        Directory.CreateDirectory(f2);
        Directory.CreateDirectory(f3);
        var oldTemp = Path.Combine(f1, "original.webp.tmp-old");
        await File.WriteAllBytesAsync(oldTemp, [1]);
        File.SetLastWriteTimeUtc(oldTemp, DateTime.UtcNow.AddHours(-30));
        var freshTemp = Path.Combine(f2, "thumb.webp.tmp-new");
        await File.WriteAllBytesAsync(freshTemp, [1]);
        Directory.SetLastWriteTimeUtc(f3, DateTime.UtcNow.AddHours(-30));
        var emptyProject = Path.Combine(_root, "projects", "p2");
        Directory.CreateDirectory(emptyProject);
        Directory.SetLastWriteTimeUtc(emptyProject, DateTime.UtcNow.AddHours(-30));

        var removed = await storage.PurgeIncompleteAsync(DateTimeOffset.UtcNow.AddHours(-24));

        removed.Should().Be(1);
        File.Exists(oldTemp).Should().BeFalse();
        File.Exists(freshTemp).Should().BeTrue("a write may still be in progress");
        Directory.Exists(f3).Should().BeFalse("old empty image folder");
        Directory.Exists(f1).Should().BeTrue("changed just now by the purge itself");
        Directory.Exists(emptyProject).Should().BeTrue("project folders are never removed");
    }
}

internal sealed class TestHostEnvironment(string contentRoot) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Testing";
    public string ApplicationName { get; set; } = "FreelanceFlow.Api";
    public string ContentRootPath { get; set; } = contentRoot;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
