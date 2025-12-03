using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks.Dataflow;

namespace Tpl.Dataflow.Builder.Abstractions;

/// <summary>
/// Abstract base class for creating custom propagator blocks with asynchronous transform logic.
/// Wraps a TransformBlock internally, allowing derived classes to implement only the transform logic.
/// </summary>
/// <typeparam name="TInput">The type of input messages.</typeparam>
/// <typeparam name="TOutput">The type of output messages.</typeparam>
/// <remarks>
/// This class simplifies creating custom dataflow blocks by handling all the IPropagatorBlock
/// interface plumbing. Derived classes only need to implement the <see cref="TransformAsync"/> method.
/// For synchronous transforms, use <see cref="PropagatorBlock{TInput, TOutput}"/> instead.
/// </remarks>
/// <example>
/// <code>
/// public class MyAsyncProcessor : AsyncPropagatorBlock&lt;string, string&gt;
/// {
///     private readonly HttpClient _httpClient;
///     
///     public MyAsyncProcessor(HttpClient httpClient) => _httpClient = httpClient;
///     
///     protected override async Task&lt;string&gt; TransformAsync(string input)
///     {
///         var response = await _httpClient.GetStringAsync(input);
///         return response;
///     }
/// }
/// 
/// // Usage in pipeline:
/// var pipeline = new DataflowPipelineBuilder()
///     .AddBufferBlock&lt;string&gt;()
///     .AddCustomBlock(new MyAsyncProcessor(httpClient))
///     .AddActionBlock(Console.WriteLine)
///     .Build();
/// </code>
/// </example>
public abstract class AsyncPropagatorBlock<TInput, TOutput> : IPropagatorBlock<TInput, TOutput>, IReceivableSourceBlock<TOutput>
{
    private readonly TransformBlock<TInput, TOutput> _innerBlock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncPropagatorBlock{TInput, TOutput}"/> class
    /// with default options.
    /// </summary>
    protected AsyncPropagatorBlock()
        : this(new ExecutionDataflowBlockOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncPropagatorBlock{TInput, TOutput}"/> class
    /// with the specified options.
    /// </summary>
    /// <param name="options">The options for configuring the underlying TransformBlock.</param>
    protected AsyncPropagatorBlock(ExecutionDataflowBlockOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _innerBlock = new TransformBlock<TInput, TOutput>(TransformAsync, options);
    }

    /// <summary>
    /// Gets a Task that represents the completion of the block.
    /// </summary>
    public Task Completion => _innerBlock.Completion;

    /// <summary>
    /// Gets the number of input items waiting to be processed.
    /// </summary>
    public int InputCount => _innerBlock.InputCount;

    /// <summary>
    /// Gets the number of output items available to be received.
    /// </summary>
    public int OutputCount => _innerBlock.OutputCount;

    /// <summary>
    /// Signals that no more items will be sent.
    /// </summary>
    public void Complete() => _innerBlock.Complete();

    /// <summary>
    /// Causes the block to complete in a faulted state.
    /// </summary>
    /// <param name="exception">The exception that caused the fault.</param>
    public void Fault(Exception exception) => ((IDataflowBlock)_innerBlock).Fault(exception);

    /// <summary>
    /// Links this block to a target block.
    /// </summary>
    /// <param name="target">The target block to link to.</param>
    /// <param name="linkOptions">Options for the link.</param>
    /// <returns>An IDisposable that can be used to unlink the blocks.</returns>
    public IDisposable LinkTo(ITargetBlock<TOutput> target, DataflowLinkOptions linkOptions)
        => _innerBlock.LinkTo(target, linkOptions);

    /// <summary>
    /// Implement this method to provide asynchronous transformation logic.
    /// </summary>
    /// <param name="input">The input message to transform.</param>
    /// <returns>A task representing the asynchronous transform operation that yields the transformed output.</returns>
    public abstract Task<TOutput> TransformAsync(TInput input);
    
    /// <inheritdoc/>
    public bool TryReceive(Predicate<TOutput>? filter, [MaybeNullWhen(false)] out TOutput item) => _innerBlock.TryReceive(filter, out item);

    /// <inheritdoc/>
    public bool TryReceiveAll([NotNullWhen(true)] out IList<TOutput>? items) => _innerBlock.TryReceiveAll(out items);

    #region Explicit IPropagatorBlock Implementation

    TOutput? ISourceBlock<TOutput>.ConsumeMessage(
        DataflowMessageHeader messageHeader,
        ITargetBlock<TOutput> target,
        out bool messageConsumed)
        => ((ISourceBlock<TOutput>)_innerBlock).ConsumeMessage(messageHeader, target, out messageConsumed);

    DataflowMessageStatus ITargetBlock<TInput>.OfferMessage(
        DataflowMessageHeader messageHeader,
        TInput messageValue,
        ISourceBlock<TInput>? source,
        bool consumeToAccept)
        => ((ITargetBlock<TInput>)_innerBlock).OfferMessage(messageHeader, messageValue, source, consumeToAccept);

    void ISourceBlock<TOutput>.ReleaseReservation(
        DataflowMessageHeader messageHeader,
        ITargetBlock<TOutput> target)
        => ((ISourceBlock<TOutput>)_innerBlock).ReleaseReservation(messageHeader, target);

    bool ISourceBlock<TOutput>.ReserveMessage(
        DataflowMessageHeader messageHeader,
        ITargetBlock<TOutput> target)
        => ((ISourceBlock<TOutput>)_innerBlock).ReserveMessage(messageHeader, target);

    #endregion
}
