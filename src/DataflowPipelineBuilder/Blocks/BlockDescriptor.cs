using System.Threading.Tasks.Dataflow;

namespace Tpl.Dataflow.Builder;

/// <summary>
/// Describes a block in the dataflow pipeline with its metadata.
/// </summary>
internal sealed class BlockDescriptor
{
    /// <summary>
    /// Gets the unique name of the block within the pipeline.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the dataflow block instance.
    /// </summary>
    public required IDataflowBlock Block { get; init; }

    /// <summary>
    /// Gets the input type of the block.
    /// </summary>
    public required Type InputType { get; init; }

    /// <summary>
    /// Gets the output type of the block. Null for ActionBlock (terminal blocks).
    /// </summary>
    public required Type? OutputType { get; init; }

    /// <summary>
    /// Gets the zero-based index of the block in the pipeline.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Gets whether this block is a terminal block (no output).
    /// </summary>
    public bool IsTerminal => OutputType is null;

    /// <summary>
    /// Gets a delegate that links this block to a target block.
    /// Captured at compile-time to avoid reflection for AOT compatibility.
    /// Null for terminal blocks (ActionBlock).
    /// </summary>
    public Action<IDataflowBlock, DataflowLinkOptions>? LinkToNext { get; init; }
}
