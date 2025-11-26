using System.Threading.Tasks.Dataflow;

namespace DataflowPipelineBuilder.Abstractions;

/// <summary>
/// Represents a built dataflow pipeline with typed input and output.
/// </summary>
/// <typeparam name="TInput">The type of data accepted by the pipeline head.</typeparam>
/// <typeparam name="TOutput">The type of data produced by the pipeline tail.</typeparam>
/// <example>
/// <code>
/// var pipeline = new DataflowPipelineBuilder()
///     .AddTransformBlock&lt;string, int&gt;(int.Parse)
///     .AddTransformBlock&lt;int, string&gt;(x => $"Result: {x}")
///     .Build();
///
/// pipeline.Post("42");
///
/// await foreach (var result in pipeline.ToAsyncEnumerable())
/// {
///     Console.WriteLine(result);
/// }
/// </code>
/// </example>
public interface IDataflowPipeline<in TInput, TOutput> : IAsyncDisposable
{
    /// <summary>
    /// Posts an item to the pipeline head synchronously.
    /// </summary>
    /// <param name="item">The item to post.</param>
    /// <returns>True if the item was accepted; otherwise, false.</returns>
    bool Post(TInput item);

    /// <summary>
    /// Sends an item to the pipeline head asynchronously.
    /// </summary>
    /// <param name="item">The item to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the item was accepted; otherwise, false.</returns>
    Task<bool> SendAsync(TInput item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals the pipeline to complete and stop accepting new items.
    /// </summary>
    void Complete();

    /// <summary>
    /// Gets a task representing the completion of the entire pipeline.
    /// </summary>
    Task Completion { get; }

    /// <summary>
    /// Gets the named blocks in the pipeline for diagnostics and monitoring.
    /// </summary>
    IReadOnlyDictionary<string, IDataflowBlock> Blocks { get; }

    /// <summary>
    /// Creates an observable sequence from the pipeline output.
    /// Users can add the System.Reactive package for full Rx operator support.
    /// </summary>
    /// <returns>An IObservable that emits pipeline output items.</returns>
    IObservable<TOutput> AsObservable();

    /// <summary>
    /// Creates an async enumerable sequence from the pipeline output.
    /// This is the preferred modern way to consume async data streams.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An IAsyncEnumerable that yields pipeline output items.</returns>
    /// <example>
    /// <code>
    /// await foreach (var item in pipeline.ToAsyncEnumerable())
    /// {
    ///     Console.WriteLine(item);
    /// }
    /// </code>
    /// </example>
    IAsyncEnumerable<TOutput> ToAsyncEnumerable(CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives an item from the pipeline output asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next available output item.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the pipeline has completed with no more items available.</exception>
    Task<TOutput> ReceiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to receive an item from the pipeline output.
    /// </summary>
    /// <param name="item">The received item, if available.</param>
    /// <returns>True if an item was received; otherwise, false.</returns>
    bool TryReceive(out TOutput? item);
}
