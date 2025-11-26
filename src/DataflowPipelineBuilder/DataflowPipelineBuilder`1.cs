using System.Threading.Tasks.Dataflow;
using Tpl.Dataflow.Builder.Abstractions;

namespace Tpl.Dataflow.Builder;

/// <summary>
/// Builder for terminal pipelines (ending with ActionBlock).
/// Only exposes Build() which returns IDataflowPipeline (base interface).
/// </summary>
/// <typeparam name="TInput">The input type of the first block in the pipeline.</typeparam>
public sealed class DataflowPipelineBuilder<TInput>
{
    private readonly DataflowLinkOptions _defaultLinkOptions;
    private readonly List<BlockDescriptor> _blocks;

    internal DataflowPipelineBuilder(
        DataflowLinkOptions defaultLinkOptions,
        CancellationToken defaultCancellationToken,
        List<BlockDescriptor> blocks)
    {
        _defaultLinkOptions = defaultLinkOptions;
        _blocks = blocks;
    }

    /// <summary>
    /// Builds the terminal pipeline.
    /// </summary>
    /// <returns>A pipeline that processes items but produces no output.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the pipeline is empty.</exception>
    public IDataflowPipeline<TInput> Build()
    {
        if (_blocks.Count == 0)
        {
            throw new InvalidOperationException("Cannot build an empty pipeline. Add at least one block.");
        }

        DataflowBuilderHelpers.LinkBlocks(_blocks, _defaultLinkOptions);

        var head = (ITargetBlock<TInput>)_blocks[0].Block;
        var tail = _blocks[^1].Block;
        var blocksDictionary = _blocks.ToDictionary(b => b.Name, b => b.Block);

        return new DataflowPipeline<TInput>(head, tail, blocksDictionary);
    }
}
