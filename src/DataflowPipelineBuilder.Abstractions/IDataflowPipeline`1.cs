using System.Threading.Tasks.Dataflow;

namespace Tpl.Dataflow.Builder.Abstractions;

/// <summary>
/// Represents the base interface for a built dataflow pipeline with typed input.
/// This interface provides core pipeline functionality: posting items, completion, and diagnostics.
/// </summary>
/// <typeparam name="TInput">The type of data accepted by the pipeline head.</typeparam>
/// <example>
/// <code>
/// // Terminal pipeline (ends with ActionBlock)
/// var pipeline = new DataflowPipelineBuilder()
///     .AddBufferBlock&lt;string&gt;()
///     .AddTransformBlock&lt;int&gt;(int.Parse)
///     .AddActionBlock(Console.WriteLine)
///     .Build();
///
/// pipeline.Post("42");
/// pipeline.Complete();
/// await pipeline.Completion;
/// </code>
/// </example>
public interface IDataflowPipeline<in TInput> : IAsyncDisposable
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
