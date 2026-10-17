using System.Threading.Channels;
using GoMigo.Application.Research;

namespace GoMigo.Infrastructure.Queue;

/// <summary>In-process queue of research runs. Durable state lives in the repository; this only signals work.</summary>
public sealed class ChannelResearchQueue : IResearchQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(500)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true
    });

    public ValueTask EnqueueAsync(Guid runId, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(runId, cancellationToken);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
