using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;

namespace Tpl.Dataflow.Builder.Abstractions;

/// <summary>
/// Represents a dataflow pipeline that outputs processed items to a Channel.
/// Extends <see cref="IDataflowPipeline{TInput}"/> with channel-based output consumption.
/// </summary>
/// <typeparam name="TInput">The type of data accepted by the pipeline head.</typeparam>
/// <typeparam name="TOutput">The type of data produced to the output channel.</typeparam>
/// <example>
/// <code>
/// var pipeline = new DataflowPipelineBuilder()
///     .AddBufferBlock&lt;string&gt;()
///     .AddTransformBlock(int.Parse)
///     .BuildAsChannel();
///
/// pipeline.Post("42");
/// pipeline.Complete();
///
/// await foreach (var item in pipeline.Output.ReadAllAsync())
/// {
///     Console.WriteLine(item);
/// }
/// </code>
/// </example>
public interface IDataflowChannelPipeline<in TInput, TOutput> : IDataflowPipeline<TInput>
{
    /// <summary>
    /// Gets the channel reader for consuming pipeline output.
    /// Items processed by the pipeline are written to this channel.
    /// </summary>
    /// <remarks>
    /// The channel is managed internally by the pipeline.
    /// When the pipeline completes, the channel writer is automatically completed.
    /// </remarks>
    ChannelReader<TOutput> Output { get; }
}
