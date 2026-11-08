namespace FreelanceFlow.Api.Infrastructure.Images;

/// <summary>
/// Singleton limit on concurrent image decoding (a 50 MP decode is ~200 MB of pixels): extra uploads wait.
/// Storage:MaxConcurrentImageProcessing, default 2.
/// </summary>
public sealed class ImageProcessingGate(int permits)
{
    public const int DefaultPermits = 2;
    private readonly SemaphoreSlim _slots = new(Math.Max(1, permits));

    public async Task<T> RunAsync<T>(Func<T> work, CancellationToken ct)
    {
        await _slots.WaitAsync(ct);
        try
        {
            return await Task.Run(work, CancellationToken.None);
        }
        finally
        {
            _slots.Release();
        }
    }
}
