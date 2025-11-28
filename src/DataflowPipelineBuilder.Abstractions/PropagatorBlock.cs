using System.Threading.Tasks.Dataflow;

namespace Tpl.Dataflow.Builder.Abstractions;

/// <summary>
/// Abstract base class for creating custom propagator blocks with synchronous transform logic.
/// Wraps a TransformBlock internally, allowing derived classes to implement only the transform logic.
/// </summary>
/// <typeparam name="TInput">The type of input messages.</typeparam>
/// <typeparam name="TOutput">The type of output messages.</typeparam>
/// <remarks>
/// This class simplifies creating custom dataflow blocks by handling all the IPropagatorBlock
/// interface plumbing. Derived classes only need to implement the <see cref="Transform"/> method.
/// For async transforms, use <see cref="AsyncPropagatorBlock{TInput, TOutput}"/> instead.
/// </remarks>
/// <example>
/// <code>
/// public class MyMultiplier : PropagatorBlock&lt;int, int&gt;
/// {
///     private readonly int _factor;
///     
///     public MyMultiplier(int factor) => _factor = factor;
///     
///     protected override int Transform(int input) => input * _factor;
/// }
/// 
/// // Usage in pipeline:
/// var pipeline = new DataflowPipelineBuilder()
///     .AddBufferBlock&lt;int&gt;()
///     .AddCustomBlock(new MyMultiplier(10))
///     .AddActionBlock(Console.WriteLine)
///     .Build();
/// </code>
/// </example>
public abstract class PropagatorBlock<TInput, TOutput> : IPropagatorBlock<TInput, TOutput>
{
    private readonly TransformBlock<TInput, TOutput> _innerBlock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PropagatorBlock{TInput, TOutput}"/> class
    /// with default options.
    /// </summary>
    protected PropagatorBlock()
        : this(new ExecutionDataflowBlockOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PropagatorBlock{TInput, TOutput}"/> class
    /// with the specified options.
    /// </summary>
    /// <param name="options">The options for configuring the underlying TransformBlock.</param>
    protected PropagatorBlock(ExecutionDataflowBlockOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _innerBlock = new TransformBlock<TInput, TOutput>(Transform, options);
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
    /// Implement this method to provide synchronous transformation logic.
    /// </summary>
    /// <param name="input">The input message to transform.</param>
    /// <returns>The transformed output message.</returns>
    protected abstract TOutput Transform(TInput input);

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
