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
        RootPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(env.ContentRootPath, root)));
        Directory.CreateDirectory(RootPath);
        EnsureWritable(RootPath);
    }

    /// <summary>Fail at startup (not on the first upload) when the volume is not writable by the app user.</summary>
    private static void EnsureWritable(string root)
    {
        var probe = Path.Combine(root, ".write-probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllBytes(probe, [0]);
            File.Delete(probe);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"Storage root '{root}' is not writable by the app user. Fix the volume ownership (e.g. chown -R 1001:1001) or Storage:Local:RootPath.", ex);
        }
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

    public Task<IReadOnlyList<StoredObject>> ListAsync(string prefix, CancellationToken ct = default)
    {
        var folder = prefix.Length == 0 ? RootPath : PathOf(prefix.TrimEnd('/'));
        if (!Directory.Exists(folder)) return Task.FromResult<IReadOnlyList<StoredObject>>([]);
        var objects = new List<StoredObject>();
        foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            if (file.Name.Contains(TempMarker, StringComparison.Ordinal)) continue;
            var key = Path.GetRelativePath(RootPath, file.FullName).Replace(Path.DirectorySeparatorChar, '/');
            if (!KeyPattern().IsMatch(key)) continue;   // e.g. the startup write probe — never a key the app wrote
            objects.Add(new StoredObject(key, file.Length, new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero)));
        }
        return Task.FromResult<IReadOnlyList<StoredObject>>(objects);
    }

    public async Task<IReadOnlyList<string>> DeleteManyAsync(IReadOnlyCollection<string> keys, CancellationToken ct = default)
    {
        var failed = new List<string>();
        foreach (var key in keys)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await DeleteAsync(key, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed.Add(key);
            }
        }
        return failed;
    }

    public Task<int> PurgeIncompleteAsync(DateTimeOffset olderThan, CancellationToken ct = default)
    {
        var cutoff = olderThan.UtcDateTime;
        var root = new DirectoryInfo(RootPath);
        var removed = 0;
        foreach (var temp in root.EnumerateFiles("*" + TempMarker + "*", SearchOption.AllDirectories).ToList())
        {
            ct.ThrowIfCancellationRequested();
            if (temp.LastWriteTimeUtc >= cutoff) continue;
            try
            {
                temp.Delete();
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // still held by a writer — next run
            }
        }
        // Empty image folders (projects/{p}/{f}); deepest first; never the root, "projects" or a project folder
        var folders = root.EnumerateDirectories("*", SearchOption.AllDirectories)
            .Where(d => Path.GetRelativePath(RootPath, d.FullName).Split(Path.DirectorySeparatorChar).Length >= 3)
            .OrderByDescending(d => d.FullName.Length)
            .ToList();
        foreach (var folder in folders)
        {
            try
            {
                folder.Refresh();
                if (folder.Exists && folder.LastWriteTimeUtc < cutoff && !folder.EnumerateFileSystemInfos().Any()) folder.Delete();
            }
            catch (IOException)
            {
                // a concurrent write re-used the folder — leave it
            }
        }
        return Task.FromResult(removed);
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
