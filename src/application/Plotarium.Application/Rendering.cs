using System.Text.Json;
using Plotarium.Domain;
namespace Plotarium.Application;

public interface IChartRenderer
{
    Task<RenderArtifact> RenderAsync(RenderRequest request, CancellationToken cancellationToken);
}
public sealed class BoundedRenderGate(int concurrency, int queueCapacity) : IDisposable
{
    private readonly SemaphoreSlim permits = new(concurrency > 0 ? concurrency : throw new ArgumentOutOfRangeException(nameof(concurrency)));
    private readonly int capacity = checked(concurrency + (queueCapacity >= 0 ? queueCapacity : throw new ArgumentOutOfRangeException(nameof(queueCapacity))));
    private int admitted;
    public async Task<IDisposable> EnterAsync(CancellationToken token)
    {
        if (Interlocked.Increment(ref admitted) > capacity)
        {
            Interlocked.Decrement(ref admitted);
            throw new RenderFailure("busy", "Render capacity is exhausted.", 429);
        }
        try { await permits.WaitAsync(token); return new Lease(this); }
        catch { Interlocked.Decrement(ref admitted); throw; }
    }
    private sealed class Lease(BoundedRenderGate gate) : IDisposable
    {
        private BoundedRenderGate? owner = gate;
        public void Dispose()
        {
            var current = Interlocked.Exchange(ref owner, null);
            if (current is null) return;
            current.permits.Release(); Interlocked.Decrement(ref current.admitted);
        }
    }
    public void Dispose() => permits.Dispose();
}
public sealed class RenderService(IChartRenderer renderer, BoundedRenderGate gate, TimeSpan timeout)
{
    public async Task<RenderArtifact> RenderAsync(RenderRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Format == "json")
        {
            var definition = new Definition("1", "envelope-only", "structural-only", Contract.EngineVersion(request.Engine), request);
            return new(JsonSerializer.SerializeToUtf8Bytes(definition, Contract.Json), "application/json", "json");
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            using var lease = await gate.EnterAsync(deadline.Token);
            var artifact = await renderer.RenderAsync(request, deadline.Token);
            if (artifact.Bytes.Length > Contract.MaxOutputBytes) throw new RenderFailure("output_limit", "Rendered output is too large.", 413);
            if (request.Format == "svg") SvgOutputPolicy.Validate(artifact.Bytes);
            return artifact;
        }
        catch(OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new RenderFailure("timeout", "Render deadline exceeded.", 504); }
    }
}
