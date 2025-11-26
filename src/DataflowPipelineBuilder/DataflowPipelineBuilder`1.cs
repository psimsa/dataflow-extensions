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
    private readonly CancellationToken _defaultCancellationToken;
    private readonly List<BlockDescriptor> _blocks;

    internal DataflowPipelineBuilder(
        DataflowLinkOptions defaultLinkOptions,
        CancellationToken defaultCancellationToken,
        List<BlockDescriptor> blocks)
    {
        _defaultLinkOptions = defaultLinkOptions;
        _defaultCancellationToken = defaultCancellationToken;
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

        LinkBlocks();

        var head = (ITargetBlock<TInput>)_blocks[0].Block;
        var tail = _blocks[^1].Block;
        var blocksDictionary = _blocks.ToDictionary(b => b.Name, b => b.Block);

        return new DataflowPipeline<TInput>(head, tail, blocksDictionary);
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
}
