using System.Threading.Channels;
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
///     .Build();
/// </code>
/// </example>
public sealed class DataflowPipelineBuilder
{
    private readonly DataflowLinkOptions _defaultLinkOptions;
    private readonly CancellationToken _defaultCancellationToken;
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataflowPipelineBuilder"/> class.
    /// </summary>
    /// <param name="defaultLinkOptions">Default link options applied to all block connections. If null, PropagateCompletion is enabled by default.</param>
    /// <param name="defaultCancellationToken">Default cancellation token applied to all blocks that support cancellation.</param>
    /// <param name="serviceProvider">Optional service provider for resolving custom blocks via dependency injection.</param>
    public DataflowPipelineBuilder(
        DataflowLinkOptions? defaultLinkOptions = null,
        CancellationToken defaultCancellationToken = default,
        IServiceProvider? serviceProvider = null)
    {
        _defaultLinkOptions = defaultLinkOptions ?? new DataflowLinkOptions { PropagateCompletion = true };
        _defaultCancellationToken = defaultCancellationToken;
        _serviceProvider = serviceProvider;
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
        options = DataflowBuilderHelpers.ApplyCancellationToken(options, _defaultCancellationToken);
        var block = new BufferBlock<T>(options ?? new DataflowBlockOptions());
        var descriptor = DataflowBuilderHelpers.CreateDescriptor(name, block, typeof(T), typeof(T), 0,
            (target, linkOptions) => block.LinkTo((ITargetBlock<T>)target, linkOptions));

        return new DataflowPipelineBuilder<T, T>(_defaultLinkOptions, _defaultCancellationToken, [descriptor], _serviceProvider);
    }

    /// <summary>
    /// Starts the pipeline with a TransformBlock.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <param name="transform">The transformation function.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="ensureOrdered">Whether to preserve input order in output. Default is false for better parallel performance.</param>
    /// <param name="options">Optional execution options.</param>
    /// <returns>A builder to continue building the pipeline.</returns>
    public DataflowPipelineBuilder<TInput, TOutput> AddTransformBlock<TInput, TOutput>(
        Func<TInput, TOutput> transform,
        string? name = null,
        bool ensureOrdered = false,
        ExecutionDataflowBlockOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(transform);

        var effectiveOptions = DataflowBuilderHelpers.ApplyExecutionOptions(options, _defaultCancellationToken, ensureOrdered);
        var block = new TransformBlock<TInput, TOutput>(transform, effectiveOptions);
        var descriptor = DataflowBuilderHelpers.CreateDescriptor(name, block, typeof(TInput), typeof(TOutput), 0,
            (target, linkOptions) => block.LinkTo((ITargetBlock<TOutput>)target, linkOptions));

        return new DataflowPipelineBuilder<TInput, TOutput>(_defaultLinkOptions, _defaultCancellationToken, [descriptor], _serviceProvider);
    }

    /// <summary>
    /// Starts the pipeline with an async TransformBlock.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <param name="transform">The async transformation function.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="ensureOrdered">Whether to preserve input order in output. Default is false for better parallel performance.</param>
    /// <param name="options">Optional execution options.</param>
    /// <returns>A builder to continue building the pipeline.</returns>
    public DataflowPipelineBuilder<TInput, TOutput> AddTransformBlock<TInput, TOutput>(
        Func<TInput, Task<TOutput>> transform,
        string? name = null,
        bool ensureOrdered = false,
        ExecutionDataflowBlockOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(transform);

        var effectiveOptions = DataflowBuilderHelpers.ApplyExecutionOptions(options, _defaultCancellationToken, ensureOrdered);
        var block = new TransformBlock<TInput, TOutput>(transform, effectiveOptions);
        var descriptor = DataflowBuilderHelpers.CreateDescriptor(name, block, typeof(TInput), typeof(TOutput), 0,
            (target, linkOptions) => block.LinkTo((ITargetBlock<TOutput>)target, linkOptions));

        return new DataflowPipelineBuilder<TInput, TOutput>(_defaultLinkOptions, _defaultCancellationToken, [descriptor], _serviceProvider);
    }

    /// <summary>
    /// Starts the pipeline from a Channel, consuming items from the channel reader.
    /// </summary>
    /// <typeparam name="T">The type of items in the channel.</typeparam>
    /// <param name="channel">The channel to consume from.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="options">Optional block options.</param>
    /// <returns>A builder to continue building the pipeline.</returns>
    /// <remarks>
    /// A background task will pump items from the channel reader into the pipeline.
    /// When the channel completes, the pipeline head block is completed.
    /// </remarks>
    /// <example>
    /// <code>
    /// var channel = Channel.CreateUnbounded&lt;string&gt;();
    /// var pipeline = new DataflowPipelineBuilder()
    ///     .FromChannelSource(channel)
    ///     .AddTransformBlock(int.Parse)
    ///     .AddActionBlock(Console.WriteLine)
    ///     .Build();
    /// </code>
    /// </example>
    public DataflowPipelineBuilder<T, T> FromChannelSource<T>(
        Channel<T> channel,
        string? name = null,
        DataflowBlockOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(channel);
        return FromChannelSource(channel.Reader, name, options);
    }

    /// <summary>
    /// Starts the pipeline from a ChannelReader, consuming items from the reader.
    /// </summary>
    /// <typeparam name="T">The type of items in the channel.</typeparam>
    /// <param name="reader">The channel reader to consume from.</param>
    /// <param name="name">Optional name for the block. If null, auto-generated.</param>
    /// <param name="options">Optional block options.</param>
    /// <returns>A builder to continue building the pipeline.</returns>
    /// <remarks>
    /// A background task will pump items from the channel reader into the pipeline.
    /// When the channel completes, the pipeline head block is completed.
    /// </remarks>
    /// <example>
    /// <code>
    /// var channel = Channel.CreateUnbounded&lt;string&gt;();
    /// var pipeline = new DataflowPipelineBuilder()
    ///     .FromChannelSource(channel.Reader)
    ///     .AddTransformBlock(int.Parse)
    ///     .AddActionBlock(Console.WriteLine)
    ///     .Build();
    /// </code>
    /// </example>
    public DataflowPipelineBuilder<T, T> FromChannelSource<T>(
        ChannelReader<T> reader,
        string? name = null,
        DataflowBlockOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(reader);

        options = DataflowBuilderHelpers.ApplyCancellationToken(options, _defaultCancellationToken);
        var block = new BufferBlock<T>(options ?? new DataflowBlockOptions());
        var descriptor = DataflowBuilderHelpers.CreateDescriptor(name, block, typeof(T), typeof(T), 0,
            (target, linkOptions) => block.LinkTo((ITargetBlock<T>)target, linkOptions));

        var cancellationToken = options?.CancellationToken ?? _defaultCancellationToken;
        DataflowBuilderHelpers.StartChannelPumpingTask(reader, block, cancellationToken);

        return new DataflowPipelineBuilder<T, T>(_defaultLinkOptions, _defaultCancellationToken, [descriptor], _serviceProvider);
    }
}
