using System.Runtime.CompilerServices;
using System.Threading.Tasks.Dataflow;
using Tpl.Dataflow.Builder.Abstractions;

namespace Tpl.Dataflow.Builder;

/// <summary>
/// Implementation of a dataflow pipeline that can produce output.
/// </summary>
/// <typeparam name="TInput">The input type of the pipeline.</typeparam>
/// <typeparam name="TOutput">The output type of the pipeline.</typeparam>
internal sealed class DataflowPipeline<TInput, TOutput> : IDataflowPipeline<TInput, TOutput>
{
    private readonly ITargetBlock<TInput> _head;
    private readonly IReceivableSourceBlock<TOutput> _tail;
    private readonly IReadOnlyDictionary<string, IDataflowBlock> _blocks;
    private bool _disposed;

    public DataflowPipeline(
        ITargetBlock<TInput> head,
        IReceivableSourceBlock<TOutput> tail,
        IReadOnlyDictionary<string, IDataflowBlock> blocks)
    {
        _head = head ?? throw new ArgumentNullException(nameof(head));
        _tail = tail ?? throw new ArgumentNullException(nameof(tail));
        _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
    }

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
    public IObservable<TOutput> AsObservable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _tail.AsObservable();
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<TOutput> ToAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await _tail.OutputAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            while (_tail.TryReceive(out var item))
            {
                yield return item;
            }
        }
    }

    /// <inheritdoc/>
    public Task<bool> OutputAvailableAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _tail.OutputAvailableAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TOutput> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _tail.ReceiveAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public bool TryReceive(out TOutput? item)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _tail.TryReceive(out item);
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
