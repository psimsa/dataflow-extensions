using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;
using Tpl.Dataflow.Builder.Abstractions;

namespace Tpl.Dataflow.Builder;

/// <summary>
/// Implementation of a dataflow pipeline that outputs to a Channel.
/// </summary>
/// <typeparam name="TInput">The input type of the pipeline.</typeparam>
/// <typeparam name="TOutput">The output type written to the channel.</typeparam>
internal sealed class DataflowChannelPipeline<TInput, TOutput> : IDataflowChannelPipeline<TInput, TOutput>
{
    private readonly ITargetBlock<TInput> _head;
    private readonly IDataflowBlock _tail;
    private readonly IReadOnlyDictionary<string, IDataflowBlock> _blocks;
    private readonly ChannelReader<TOutput> _output;
    private bool _disposed;

    public DataflowChannelPipeline(
        ITargetBlock<TInput> head,
        IDataflowBlock tail,
        IReadOnlyDictionary<string, IDataflowBlock> blocks,
        ChannelReader<TOutput> output)
    {
        _head = head ?? throw new ArgumentNullException(nameof(head));
        _tail = tail ?? throw new ArgumentNullException(nameof(tail));
        _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    /// <inheritdoc/>
    public ChannelReader<TOutput> Output => _output;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, IDataflowBlock> Blocks => _blocks;

    /// <inheritdoc/>
    public Task Completion => _tail.Completion;

    /// <inheritdoc/>
    public bool Post(TInput item)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _head.Post(item);
    }

    /// <inheritdoc/>
    public Task<bool> SendAsync(TInput item, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _head.SendAsync(item, cancellationToken);
    }

    /// <inheritdoc/>
    public void Complete()
    {
        _head.Complete();
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _head.Complete();

        return ValueTask.CompletedTask;
    }
}
