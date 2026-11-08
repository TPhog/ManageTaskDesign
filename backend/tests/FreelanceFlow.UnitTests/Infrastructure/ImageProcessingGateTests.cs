using FreelanceFlow.Api.Infrastructure.Images;

namespace FreelanceFlow.UnitTests.Infrastructure;

public class ImageProcessingGateTests
{
    [Fact]
    public async Task Never_runs_more_than_the_configured_permits_at_once()
    {
        var gate = new ImageProcessingGate(2);
        int running = 0, max = 0;
        var tasks = Enumerable.Range(0, 8).Select(_ => gate.RunAsync(() =>
        {
            var now = Interlocked.Increment(ref running);
            int current;
            while ((current = Volatile.Read(ref max)) < now && Interlocked.CompareExchange(ref max, now, current) != current) { }
            Thread.Sleep(50);
            Interlocked.Decrement(ref running);
            return 1;
        }, CancellationToken.None)).ToArray();
        await Task.WhenAll(tasks);
        Assert.Equal(2, max);
    }

    [Fact]
    public async Task Releases_the_permit_when_the_work_throws()
    {
        var gate = new ImageProcessingGate(1);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => gate.RunAsync<int>(() => throw new InvalidOperationException(), CancellationToken.None));
        Assert.Equal(7, await gate.RunAsync(() => 7, CancellationToken.None));
    }
}
