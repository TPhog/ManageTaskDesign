using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace FreelanceFlow.Api.Infrastructure.Storage;

/// <summary>
/// Files under Storage:Local:RootPath (outside the web root, 07-security §5; never served statically).
/// Writes go to a temp file then rename, so a reader never sees half a file; keys cannot escape the root.
/// </summary>
public sealed partial class LocalFileStorage : IFileStorage
{
    private const string TempMarker = ".tmp-";

    public LocalFileStorage(IOptions<StorageOptions> options, IHostEnvironment env)
    {
        var root = options.Value.Local.RootPath;
        RootPath = Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(env.ContentRootPath, root));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public async Task PutAsync(string key, byte[] content, string contentType, CancellationToken ct = default)
    {
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + TempMarker + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(temp, content, ct);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var path = PathOf(key);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = PathOf(key);
        if (File.Exists(path)) File.Delete(path);
        // Remove the image's own folder once its last variant is gone (never the project folder or the root)
        var folder = Path.GetDirectoryName(path)!;
        try
        {
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
        }
        catch (IOException)
        {
            // a concurrent write re-used the folder — leave it
        }
        return Task.CompletedTask;
    }

    public Task<long> TotalBytesAsync(string prefix, CancellationToken ct = default)
    {
        var folder = prefix.Length == 0 ? RootPath : PathOf(prefix.TrimEnd('/'));
        if (!Directory.Exists(folder)) return Task.FromResult(0L);
        var total = new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(f => !f.Name.Contains(TempMarker, StringComparison.Ordinal))
            .Sum(f => f.Length);
        return Task.FromResult(total);
    }

    private string PathOf(string key)
    {
        if (!KeyPattern().IsMatch(key) || key.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Invalid storage key", nameof(key));
        var full = Path.GetFullPath(Path.Combine(RootPath, key));
        if (!full.StartsWith(RootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Invalid storage key", nameof(key));
        return full;
    }

    /// <summary>Lowercase letters, digits, '/', '.', '_' and '-' only; must start with a letter or digit.</summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9/._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
