using System.Runtime.CompilerServices;
using System.Threading.Tasks.Dataflow;
using DataflowPipelineBuilder.Abstractions;

namespace DataflowPipelineBuilder;

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
    private bool _hasTerminalBlock;

    internal DataflowPipelineBuilder(
        DataflowLinkOptions defaultLinkOptions,
        CancellationToken defaultCancellationToken,
        List<BlockDescriptor> blocks)
    {
        _defaultLinkOptions = defaultLinkOptions;
        _defaultCancellationToken = defaultCancellationToken;
        _blocks = blocks;
        _blockNames = new HashSet<string>(blocks.Select(b => b.Name));
        _hasTerminalBlock = blocks.Count > 0 && blocks[^1].IsTerminal;
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
        ThrowIfTerminated();
        options = ApplyCancellationToken(options);
        var block = new BufferBlock<TOutput>(options ?? new DataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), typeof(TOutput));

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
        ThrowIfTerminated();
        ArgumentNullException.ThrowIfNull(transform);

        options = ApplyCancellationToken(options);
        var block = new TransformBlock<TOutput, TNewOutput>(transform, options ?? new ExecutionDataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), typeof(TNewOutput));

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
        ThrowIfTerminated();
        ArgumentNullException.ThrowIfNull(transform);

        options = ApplyCancellationToken(options);
        var block = new TransformBlock<TOutput, TNewOutput>(transform, options ?? new ExecutionDataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), typeof(TNewOutput));

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
        ThrowIfTerminated();
        ArgumentNullException.ThrowIfNull(transform);

        options = ApplyCancellationToken(options);
        var block = new TransformManyBlock<TOutput, TNewOutput>(transform, options ?? new ExecutionDataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), typeof(TNewOutput));

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
        ThrowIfTerminated();
        ArgumentNullException.ThrowIfNull(transform);

        options = ApplyCancellationToken(options);
        var block = new TransformManyBlock<TOutput, TNewOutput>(transform, options ?? new ExecutionDataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), typeof(TNewOutput));

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
        ThrowIfTerminated();
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
        }

        options = ApplyCancellationToken(options);
        var block = new BatchBlock<TOutput>(batchSize, options ?? new GroupingDataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), typeof(TOutput[]));

        return new DataflowPipelineBuilder<TInput, TOutput[]>(_defaultLinkOptions, _defaultCancellationToken, _blocks);
    }

    /// <summary>
    /// Continues the pipeline with a BroadcastBlock.
    /// </summary>
    /// <param name="cloningFunction">Optional function to clone items. If null, items are passed by reference.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="options">Optional block options.</param>
    /// <returns>A builder to continue building the pipeline.</returns>
    public DataflowPipelineBuilder<TInput, TOutput> AddBroadcastBlock(
        Func<TOutput, TOutput>? cloningFunction = null,
        string? name = null,
        DataflowBlockOptions? options = null)
    {
        ThrowIfTerminated();
        options = ApplyCancellationToken(options);
        var block = new BroadcastBlock<TOutput>(cloningFunction, options ?? new DataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), typeof(TOutput));

        return this;
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
        ThrowIfTerminated();
        ArgumentNullException.ThrowIfNull(block);

        AddBlock(name, block, typeof(TOutput), typeof(TNewOutput));

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
        ThrowIfTerminated();
        ArgumentNullException.ThrowIfNull(factory);

        var block = factory();
        AddBlock(name, block, typeof(TOutput), typeof(TNewOutput));

        return new DataflowPipelineBuilder<TInput, TNewOutput>(_defaultLinkOptions, _defaultCancellationToken, _blocks);
    }

    /// <summary>
    /// Terminates the pipeline with an ActionBlock.
    /// </summary>
    /// <param name="action">The action to execute for each item.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="options">Optional execution options.</param>
    /// <returns>The same builder for calling BuildTerminal().</returns>
    public DataflowPipelineBuilder<TInput, TOutput> AddActionBlock(
        Action<TOutput> action,
        string? name = null,
        ExecutionDataflowBlockOptions? options = null)
    {
        ThrowIfTerminated();
        ArgumentNullException.ThrowIfNull(action);

        options = ApplyCancellationToken(options);
        var block = new ActionBlock<TOutput>(action, options ?? new ExecutionDataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), outputType: null);
        _hasTerminalBlock = true;

        return this;
    }

    /// <summary>
    /// Terminates the pipeline with an async ActionBlock.
    /// </summary>
    /// <param name="action">The async action to execute for each item.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="options">Optional execution options.</param>
    /// <returns>The same builder for calling BuildTerminal().</returns>
    public DataflowPipelineBuilder<TInput, TOutput> AddActionBlock(
        Func<TOutput, Task> action,
        string? name = null,
        ExecutionDataflowBlockOptions? options = null)
    {
        ThrowIfTerminated();
        ArgumentNullException.ThrowIfNull(action);

        options = ApplyCancellationToken(options);
        var block = new ActionBlock<TOutput>(action, options ?? new ExecutionDataflowBlockOptions());
        AddBlock(name, block, typeof(TOutput), outputType: null);
        _hasTerminalBlock = true;

        return this;
    }

    /// <summary>
    /// Builds the pipeline with output capability (must NOT end with ActionBlock).
    /// </summary>
    /// <returns>A pipeline that can produce output via AsObservable(), ToAsyncEnumerable(), or ReceiveAsync().</returns>
    /// <exception cref="InvalidOperationException">Thrown when the pipeline is empty or ends with ActionBlock.</exception>
    public IDataflowPipeline<TInput, TOutput> Build()
    {
        if (_blocks.Count == 0)
        {
            throw new InvalidOperationException("Cannot build an empty pipeline. Add at least one block.");
        }

        if (_hasTerminalBlock)
        {
            throw new InvalidOperationException(
                "Cannot build a pipeline with output when it ends with ActionBlock. Use BuildTerminal() instead.");
        }

        LinkBlocks();

        var head = (ITargetBlock<TInput>)_blocks[0].Block;
        var tail = (IReceivableSourceBlock<TOutput>)_blocks[^1].Block;
        var blocksDictionary = _blocks.ToDictionary(b => b.Name, b => b.Block);

        return new DataflowPipeline<TInput, TOutput>(head, tail, blocksDictionary);
    }

    /// <summary>
    /// Builds the pipeline as a terminal pipeline (must end with ActionBlock).
    /// </summary>
    /// <returns>A pipeline that processes items but produces no output.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the pipeline is empty or doesn't end with ActionBlock.</exception>
    public ITerminalDataflowPipeline<TInput> BuildTerminal()
    {
        if (_blocks.Count == 0)
        {
            throw new InvalidOperationException("Cannot build an empty pipeline. Add at least one block.");
        }

        if (!_hasTerminalBlock)
        {
            throw new InvalidOperationException(
                "Cannot build a terminal pipeline when it doesn't end with ActionBlock. Use Build() instead, or add an ActionBlock.");
        }

        LinkBlocks();

        var head = (ITargetBlock<TInput>)_blocks[0].Block;
        var tail = _blocks[^1].Block;
        var blocksDictionary = _blocks.ToDictionary(b => b.Name, b => b.Block);

        return new TerminalDataflowPipeline<TInput>(head, tail, blocksDictionary);
    }

    private void LinkBlocks()
    {
        for (var i = 0; i < _blocks.Count - 1; i++)
        {
            var source = _blocks[i];
            var target = _blocks[i + 1];

            LinkBlocksDynamic(source.Block, target.Block, source.OutputType!, target.InputType);
        }
    }

    private void LinkBlocksDynamic(IDataflowBlock source, IDataflowBlock target, Type outputType, Type inputType)
    {
        var sourceType = typeof(ISourceBlock<>).MakeGenericType(outputType);
        var targetType = typeof(ITargetBlock<>).MakeGenericType(outputType);

        var linkToMethod = sourceType.GetMethod(
            nameof(ISourceBlock<object>.LinkTo),
            [targetType, typeof(DataflowLinkOptions)]);

        linkToMethod!.Invoke(source, [target, _defaultLinkOptions]);
    }

    private void AddBlock(string? name, IDataflowBlock block, Type inputType, Type? outputType)
    {
        var index = _blocks.Count;
        var actualName = name ?? GenerateBlockName(block, index);

        if (!_blockNames.Add(actualName))
        {
            throw new ArgumentException($"A block with the name '{actualName}' already exists in the pipeline.", nameof(name));
        }

        _blocks.Add(new BlockDescriptor
        {
            Name = actualName,
            Block = block,
            InputType = inputType,
            OutputType = outputType,
            Index = index
        });
    }

    private static string GenerateBlockName(IDataflowBlock block, int index)
    {
        var blockTypeName = block.GetType().Name;
        var genericArgsIndex = blockTypeName.IndexOf('`');
        if (genericArgsIndex > 0)
        {
            blockTypeName = blockTypeName[..genericArgsIndex];
        }

        return $"{blockTypeName}_{index}";
    }

    private void ThrowIfTerminated([CallerMemberName] string? caller = null)
    {
        if (_hasTerminalBlock)
        {
            throw new InvalidOperationException(
                $"Cannot call {caller} after adding a terminal ActionBlock. The pipeline has been terminated.");
        }
    }

    private DataflowBlockOptions? ApplyCancellationToken(DataflowBlockOptions? options)
    {
        if (_defaultCancellationToken == default)
        {
            return options;
        }

        options ??= new DataflowBlockOptions();
        if (options.CancellationToken == default)
        {
            options.CancellationToken = _defaultCancellationToken;
        }

        return options;
    }

    private ExecutionDataflowBlockOptions? ApplyCancellationToken(ExecutionDataflowBlockOptions? options)
    {
        if (_defaultCancellationToken == default)
        {
            return options;
        }

        options ??= new ExecutionDataflowBlockOptions();
        if (options.CancellationToken == default)
        {
            options.CancellationToken = _defaultCancellationToken;
        }

        return options;
    }

    private GroupingDataflowBlockOptions? ApplyCancellationToken(GroupingDataflowBlockOptions? options)
    {
        if (_defaultCancellationToken == default)
        {
            return options;
        }

        options ??= new GroupingDataflowBlockOptions();
        if (options.CancellationToken == default)
        {
            options.CancellationToken = _defaultCancellationToken;
        }

        return options;
    }
}
