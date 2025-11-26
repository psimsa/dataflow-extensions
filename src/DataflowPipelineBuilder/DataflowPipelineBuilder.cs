using System.Threading.Tasks.Dataflow;

namespace Tpl.Dataflow.Builder;

/// <summary>
/// Entry point for building dataflow pipelines using a fluent API.
/// </summary>
/// <example>
/// <code>
/// var pipeline = new DataflowPipelineBuilder()
///     .AddBufferBlock&lt;string&gt;()
///     .AddTransformBlock&lt;string, int&gt;(int.Parse, name: "Parser")
///     .AddActionBlock&lt;int&gt;(Console.WriteLine)
///     .BuildTerminal();
/// </code>
/// </example>
public sealed class DataflowPipelineBuilder
{
    private readonly DataflowLinkOptions _defaultLinkOptions;
    private readonly CancellationToken _defaultCancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataflowPipelineBuilder"/> class.
    /// </summary>
    /// <param name="defaultLinkOptions">Default link options applied to all block connections. If null, PropagateCompletion is enabled by default.</param>
    /// <param name="defaultCancellationToken">Default cancellation token applied to all blocks that support cancellation.</param>
    public DataflowPipelineBuilder(
        DataflowLinkOptions? defaultLinkOptions = null,
        CancellationToken defaultCancellationToken = default)
    {
        _defaultLinkOptions = defaultLinkOptions ?? new DataflowLinkOptions { PropagateCompletion = true };
        _defaultCancellationToken = defaultCancellationToken;
    }

    /// <summary>
    /// Starts the pipeline with a BufferBlock.
    /// </summary>
    /// <typeparam name="T">The type of items in the buffer.</typeparam>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="options">Optional block options.</param>
    /// <returns>A builder to continue building the pipeline.</returns>
    public DataflowPipelineBuilder<T, T> AddBufferBlock<T>(
        string? name = null,
        DataflowBlockOptions? options = null)
    {
        options = ApplyCancellationToken(options);
        var block = new BufferBlock<T>(options ?? new DataflowBlockOptions());
        var descriptor = CreateDescriptor(name, block, typeof(T), typeof(T), 0);

        return new DataflowPipelineBuilder<T, T>(_defaultLinkOptions, _defaultCancellationToken, [descriptor]);
    }

    /// <summary>
    /// Starts the pipeline with a TransformBlock.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <param name="transform">The transformation function.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="options">Optional execution options.</param>
    /// <returns>A builder to continue building the pipeline.</returns>
    public DataflowPipelineBuilder<TInput, TOutput> AddTransformBlock<TInput, TOutput>(
        Func<TInput, TOutput> transform,
        string? name = null,
        ExecutionDataflowBlockOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(transform);

        options = ApplyCancellationToken(options);
        var block = new TransformBlock<TInput, TOutput>(transform, options ?? new ExecutionDataflowBlockOptions());
        var descriptor = CreateDescriptor(name, block, typeof(TInput), typeof(TOutput), 0);

        return new DataflowPipelineBuilder<TInput, TOutput>(_defaultLinkOptions, _defaultCancellationToken, [descriptor]);
    }

    /// <summary>
    /// Starts the pipeline with an async TransformBlock.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <param name="transform">The async transformation function.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="options">Optional execution options.</param>
    /// <returns>A builder to continue building the pipeline.</returns>
    public DataflowPipelineBuilder<TInput, TOutput> AddTransformBlock<TInput, TOutput>(
        Func<TInput, Task<TOutput>> transform,
        string? name = null,
        ExecutionDataflowBlockOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(transform);

        options = ApplyCancellationToken(options);
        var block = new TransformBlock<TInput, TOutput>(transform, options ?? new ExecutionDataflowBlockOptions());
        var descriptor = CreateDescriptor(name, block, typeof(TInput), typeof(TOutput), 0);

        return new DataflowPipelineBuilder<TInput, TOutput>(_defaultLinkOptions, _defaultCancellationToken, [descriptor]);
    }

    private static BlockDescriptor CreateDescriptor(string? name, IDataflowBlock block, Type inputType, Type? outputType, int index)
    {
        var blockTypeName = block.GetType().Name;
        var genericArgsIndex = blockTypeName.IndexOf('`');
        if (genericArgsIndex > 0)
        {
            blockTypeName = blockTypeName[..genericArgsIndex];
        }

        return new BlockDescriptor
        {
            Name = name ?? $"{blockTypeName}_{index}",
            Block = block,
            InputType = inputType,
            OutputType = outputType,
            Index = index
        };
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
}
