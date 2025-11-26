using System.Threading.Tasks.Dataflow;
using Tpl.Dataflow.Builder.Abstractions;

namespace Tpl.Dataflow.Builder;

/// <summary>
/// Builder for chaining dataflow blocks with type safety.
/// </summary>
/// <typeparam name="TInput">The input type of the first block in the pipeline.</typeparam>
/// <typeparam name="TOutput">The current output type (output of the last added block).</typeparam>
public sealed class DataflowPipelineBuilder<TInput, TOutput>
{
    private readonly DataflowLinkOptions _defaultLinkOptions;
    private readonly CancellationToken _defaultCancellationToken;
    private readonly List<BlockDescriptor> _blocks;
    private readonly HashSet<string> _blockNames;

    internal DataflowPipelineBuilder(
        DataflowLinkOptions defaultLinkOptions,
        CancellationToken defaultCancellationToken,
        List<BlockDescriptor> blocks)
    {
        _defaultLinkOptions = defaultLinkOptions;
        _defaultCancellationToken = defaultCancellationToken;
        _blocks = blocks;
        _blockNames = new HashSet<string>(blocks.Select(b => b.Name));
    }

    /// <summary>
    /// Continues the pipeline with a BufferBlock.
    /// </summary>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="options">Optional block options.</param>
    /// <returns>A builder to continue building the pipeline.</returns>
    public DataflowPipelineBuilder<TInput, TOutput> AddBufferBlock(
        string? name = null,
        DataflowBlockOptions? options = null)
    {
        options = ApplyCancellationToken(options);
        var block = new BufferBlock<TOutput>(options ?? new DataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), typeof(TOutput),
            (target, linkOptions) => block.LinkTo((ITargetBlock<TOutput>)target, linkOptions));

        return this;
    }

    /// <summary>
    /// Continues the pipeline with a TransformBlock.
    /// </summary>
    /// <typeparam name="TNewOutput">The new output type after transformation.</typeparam>
    /// <param name="transform">The transformation function.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="options">Optional execution options.</param>
    /// <returns>A builder to continue building the pipeline.</returns>
    public DataflowPipelineBuilder<TInput, TNewOutput> AddTransformBlock<TNewOutput>(
        Func<TOutput, TNewOutput> transform,
        string? name = null,
        ExecutionDataflowBlockOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(transform);

        options = ApplyCancellationToken(options);
        var block = new TransformBlock<TOutput, TNewOutput>(transform, options ?? new ExecutionDataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), typeof(TNewOutput),
            (target, linkOptions) => block.LinkTo((ITargetBlock<TNewOutput>)target, linkOptions));

        return new DataflowPipelineBuilder<TInput, TNewOutput>(_defaultLinkOptions, _defaultCancellationToken, _blocks);
    }

    /// <summary>
    /// Continues the pipeline with an async TransformBlock.
    /// </summary>
    /// <typeparam name="TNewOutput">The new output type after transformation.</typeparam>
    /// <param name="transform">The async transformation function.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="options">Optional execution options.</param>
    /// <returns>A builder to continue building the pipeline.</returns>
    public DataflowPipelineBuilder<TInput, TNewOutput> AddTransformBlock<TNewOutput>(
        Func<TOutput, Task<TNewOutput>> transform,
        string? name = null,
        ExecutionDataflowBlockOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(transform);

        options = ApplyCancellationToken(options);
        var block = new TransformBlock<TOutput, TNewOutput>(transform, options ?? new ExecutionDataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), typeof(TNewOutput),
            (target, linkOptions) => block.LinkTo((ITargetBlock<TNewOutput>)target, linkOptions));

        return new DataflowPipelineBuilder<TInput, TNewOutput>(_defaultLinkOptions, _defaultCancellationToken, _blocks);
    }

    /// <summary>
    /// Continues the pipeline with a TransformManyBlock (1:N transformation).
    /// </summary>
    /// <typeparam name="TNewOutput">The new output type after transformation.</typeparam>
    /// <param name="transform">The transformation function that returns multiple outputs.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="options">Optional execution options.</param>
    /// <returns>A builder to continue building the pipeline.</returns>
    public DataflowPipelineBuilder<TInput, TNewOutput> AddTransformManyBlock<TNewOutput>(
        Func<TOutput, IEnumerable<TNewOutput>> transform,
        string? name = null,
        ExecutionDataflowBlockOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(transform);

        options = ApplyCancellationToken(options);
        var block = new TransformManyBlock<TOutput, TNewOutput>(transform, options ?? new ExecutionDataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), typeof(TNewOutput),
            (target, linkOptions) => block.LinkTo((ITargetBlock<TNewOutput>)target, linkOptions));

        return new DataflowPipelineBuilder<TInput, TNewOutput>(_defaultLinkOptions, _defaultCancellationToken, _blocks);
    }

    /// <summary>
    /// Continues the pipeline with an async TransformManyBlock.
    /// </summary>
    /// <typeparam name="TNewOutput">The new output type after transformation.</typeparam>
    /// <param name="transform">The async transformation function that returns multiple outputs.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="options">Optional execution options.</param>
    /// <returns>A builder to continue building the pipeline.</returns>
    public DataflowPipelineBuilder<TInput, TNewOutput> AddTransformManyBlock<TNewOutput>(
        Func<TOutput, Task<IEnumerable<TNewOutput>>> transform,
        string? name = null,
        ExecutionDataflowBlockOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(transform);

        options = ApplyCancellationToken(options);
        var block = new TransformManyBlock<TOutput, TNewOutput>(transform, options ?? new ExecutionDataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), typeof(TNewOutput),
            (target, linkOptions) => block.LinkTo((ITargetBlock<TNewOutput>)target, linkOptions));

        return new DataflowPipelineBuilder<TInput, TNewOutput>(_defaultLinkOptions, _defaultCancellationToken, _blocks);
    }

    /// <summary>
    /// Continues the pipeline with a BatchBlock that groups items.
    /// </summary>
    /// <param name="batchSize">The number of items per batch.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="options">Optional grouping options.</param>
    /// <returns>A builder to continue building the pipeline.</returns>
    public DataflowPipelineBuilder<TInput, TOutput[]> AddBatchBlock(
        int batchSize,
        string? name = null,
        GroupingDataflowBlockOptions? options = null)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
        }

        options = ApplyCancellationToken(options);
        var block = new BatchBlock<TOutput>(batchSize, options ?? new GroupingDataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), typeof(TOutput[]),
            (target, linkOptions) => block.LinkTo((ITargetBlock<TOutput[]>)target, linkOptions));

        return new DataflowPipelineBuilder<TInput, TOutput[]>(_defaultLinkOptions, _defaultCancellationToken, _blocks);
    }

    /// <summary>
    /// Adds a custom propagator block to the pipeline.
    /// </summary>
    /// <typeparam name="TNewOutput">The output type of the custom block.</typeparam>
    /// <param name="block">The custom block instance.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <returns>A builder to continue building the pipeline.</returns>
    public DataflowPipelineBuilder<TInput, TNewOutput> AddCustomBlock<TNewOutput>(
        IPropagatorBlock<TOutput, TNewOutput> block,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(block);

        AddBlock(name, block, typeof(TOutput), typeof(TNewOutput),
            (target, linkOptions) => block.LinkTo((ITargetBlock<TNewOutput>)target, linkOptions));

        return new DataflowPipelineBuilder<TInput, TNewOutput>(_defaultLinkOptions, _defaultCancellationToken, _blocks);
    }

    /// <summary>
    /// Adds a custom propagator block to the pipeline using a factory.
    /// </summary>
    /// <typeparam name="TNewOutput">The output type of the custom block.</typeparam>
    /// <param name="factory">Factory function to create the custom block.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <returns>A builder to continue building the pipeline.</returns>
    public DataflowPipelineBuilder<TInput, TNewOutput> AddCustomBlock<TNewOutput>(
        Func<IPropagatorBlock<TOutput, TNewOutput>> factory,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var block = factory();
        AddBlock(name, block, typeof(TOutput), typeof(TNewOutput),
            (target, linkOptions) => block.LinkTo((ITargetBlock<TNewOutput>)target, linkOptions));

        return new DataflowPipelineBuilder<TInput, TNewOutput>(_defaultLinkOptions, _defaultCancellationToken, _blocks);
    }

    /// <summary>
    /// Terminates the pipeline with an ActionBlock.
    /// </summary>
    /// <param name="action">The action to execute for each item.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="options">Optional execution options.</param>
    /// <returns>A terminal builder that can only call Build().</returns>
    public DataflowPipelineBuilder<TInput> AddActionBlock(
        Action<TOutput> action,
        string? name = null,
        ExecutionDataflowBlockOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        options = ApplyCancellationToken(options);
        var block = new ActionBlock<TOutput>(action, options ?? new ExecutionDataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), outputType: null, linkToNext: null);

        return new DataflowPipelineBuilder<TInput>(_defaultLinkOptions, _defaultCancellationToken, _blocks);
    }

    /// <summary>
    /// Terminates the pipeline with an async ActionBlock.
    /// </summary>
    /// <param name="action">The async action to execute for each item.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="options">Optional execution options.</param>
    /// <returns>A terminal builder that can only call Build().</returns>
    public DataflowPipelineBuilder<TInput> AddActionBlock(
        Func<TOutput, Task> action,
        string? name = null,
        ExecutionDataflowBlockOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        options = ApplyCancellationToken(options);
        var block = new ActionBlock<TOutput>(action, options ?? new ExecutionDataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), outputType: null, linkToNext: null);

        return new DataflowPipelineBuilder<TInput>(_defaultLinkOptions, _defaultCancellationToken, _blocks);
    }

    /// <summary>
    /// Builds the pipeline with output capability.
    /// </summary>
    /// <returns>A pipeline that can produce output via AsObservable(), ToAsyncEnumerable(), or ReceiveAsync().</returns>
    /// <exception cref="InvalidOperationException">Thrown when the pipeline is empty.</exception>
    public IDataflowPipeline<TInput, TOutput> Build()
    {
        if (_blocks.Count == 0)
        {
            throw new InvalidOperationException("Cannot build an empty pipeline. Add at least one block.");
        }

        DataflowBuilderHelpers.LinkBlocks(_blocks, _defaultLinkOptions);

        var head = (ITargetBlock<TInput>)_blocks[0].Block;
        var tail = (IReceivableSourceBlock<TOutput>)_blocks[^1].Block;
        var blocksDictionary = _blocks.ToDictionary(b => b.Name, b => b.Block);

        return new DataflowPipeline<TInput, TOutput>(head, tail, blocksDictionary);
    }

    private void AddBlock(
        string? name,
        IDataflowBlock block,
        Type inputType,
        Type? outputType,
        Action<IDataflowBlock, DataflowLinkOptions>? linkToNext = null)
    {
        DataflowBuilderHelpers.AddBlock(_blocks, _blockNames, name, block, inputType, outputType, linkToNext);
    }

    private DataflowBlockOptions? ApplyCancellationToken(DataflowBlockOptions? options) =>
        DataflowBuilderHelpers.ApplyCancellationToken(options, _defaultCancellationToken);

    private ExecutionDataflowBlockOptions? ApplyCancellationToken(ExecutionDataflowBlockOptions? options) =>
        DataflowBuilderHelpers.ApplyCancellationToken(options, _defaultCancellationToken);

    private GroupingDataflowBlockOptions? ApplyCancellationToken(GroupingDataflowBlockOptions? options) =>
        DataflowBuilderHelpers.ApplyCancellationToken(options, _defaultCancellationToken);
}
