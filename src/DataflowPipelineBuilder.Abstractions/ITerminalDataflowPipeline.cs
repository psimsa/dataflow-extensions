using System.Threading.Tasks.Dataflow;

namespace DataflowPipelineBuilder.Abstractions;

/// <summary>
/// Represents a built dataflow pipeline with typed input but no output (terminal ActionBlock).
/// </summary>
/// <typeparam name="TInput">The type of data accepted by the pipeline head.</typeparam>
/// <example>
/// <code>
/// var pipeline = new DataflowPipelineBuilder()
///     .AddBufferBlock&lt;string&gt;()
///     .AddTransformBlock&lt;string, int&gt;(int.Parse)
///     .AddActionBlock&lt;int&gt;(Console.WriteLine)
///     .BuildTerminal();
///
/// pipeline.Post("42");
/// pipeline.Complete();
/// await pipeline.Completion;
/// </code>
/// </example>
public interface ITerminalDataflowPipeline<in TInput> : IAsyncDisposable
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
}
