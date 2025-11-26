using System.Threading.Tasks.Dataflow;

namespace Tpl.Dataflow.Builder;

/// <summary>
/// Internal static helper methods shared across all builder classes.
/// Consolidates common functionality for AOT compatibility and maintainability.
/// </summary>
internal static class DataflowBuilderHelpers
{
    /// <summary>
    /// Links all blocks in the pipeline using their stored linking delegates.
    /// </summary>
    /// <param name="blocks">The list of block descriptors to link.</param>
    /// <param name="linkOptions">The link options to use for all connections.</param>
    public static void LinkBlocks(List<BlockDescriptor> blocks, DataflowLinkOptions linkOptions)
    {
        for (var i = 0; i < blocks.Count - 1; i++)
        {
            var source = blocks[i];
            var target = blocks[i + 1];

            source.LinkToNext!(target.Block, linkOptions);
        }
    }

    /// <summary>
    /// Generates a default name for a block based on its type and index.
    /// </summary>
    /// <param name="block">The dataflow block.</param>
    /// <param name="index">The zero-based index in the pipeline.</param>
    /// <returns>A generated name in the format "{BlockTypeName}_{Index}".</returns>
    public static string GenerateBlockName(IDataflowBlock block, int index)
    {
        var blockTypeName = block.GetType().Name;
        var genericArgsIndex = blockTypeName.IndexOf('`');
        if (genericArgsIndex > 0)
        {
            blockTypeName = blockTypeName[..genericArgsIndex];
        }

        return $"{blockTypeName}_{index}";
    }

    /// <summary>
    /// Applies the default cancellation token to block options if not already set.
    /// </summary>
    /// <param name="options">The options to modify (may be null).</param>
    /// <param name="defaultCancellationToken">The default cancellation token.</param>
    /// <returns>The modified options, or null if no changes were needed.</returns>
    public static DataflowBlockOptions? ApplyCancellationToken(
        DataflowBlockOptions? options,
        CancellationToken defaultCancellationToken)
    {
        if (defaultCancellationToken == default)
        {
            return options;
        }

        options ??= new DataflowBlockOptions();
        if (options.CancellationToken == default)
        {
            options.CancellationToken = defaultCancellationToken;
        }

        return options;
    }

    /// <summary>
    /// Applies the default cancellation token to execution block options if not already set.
    /// </summary>
    /// <param name="options">The options to modify (may be null).</param>
    /// <param name="defaultCancellationToken">The default cancellation token.</param>
    /// <returns>The modified options, or null if no changes were needed.</returns>
    public static ExecutionDataflowBlockOptions? ApplyCancellationToken(
        ExecutionDataflowBlockOptions? options,
        CancellationToken defaultCancellationToken)
    {
        if (defaultCancellationToken == default)
        {
            return options;
        }

        options ??= new ExecutionDataflowBlockOptions();
        if (options.CancellationToken == default)
        {
            options.CancellationToken = defaultCancellationToken;
        }

        return options;
    }

    /// <summary>
    /// Applies the default cancellation token to grouping block options if not already set.
    /// </summary>
    /// <param name="options">The options to modify (may be null).</param>
    /// <param name="defaultCancellationToken">The default cancellation token.</param>
    /// <returns>The modified options, or null if no changes were needed.</returns>
    public static GroupingDataflowBlockOptions? ApplyCancellationToken(
        GroupingDataflowBlockOptions? options,
        CancellationToken defaultCancellationToken)
    {
        if (defaultCancellationToken == default)
        {
            return options;
        }

        options ??= new GroupingDataflowBlockOptions();
        if (options.CancellationToken == default)
        {
            options.CancellationToken = defaultCancellationToken;
        }

        return options;
    }

    /// <summary>
    /// Creates a block descriptor with the specified parameters.
    /// </summary>
    /// <param name="name">The name for the block (null for auto-generated).</param>
    /// <param name="block">The dataflow block instance.</param>
    /// <param name="inputType">The input type of the block.</param>
    /// <param name="outputType">The output type of the block (null for terminal blocks).</param>
    /// <param name="index">The zero-based index in the pipeline.</param>
    /// <param name="linkToNext">The delegate to link this block to the next (null for terminal blocks).</param>
    /// <returns>A configured BlockDescriptor.</returns>
    public static BlockDescriptor CreateDescriptor(
        string? name,
        IDataflowBlock block,
        Type inputType,
        Type? outputType,
        int index,
        Action<IDataflowBlock, DataflowLinkOptions>? linkToNext)
    {
        return new BlockDescriptor
        {
            Name = name ?? GenerateBlockName(block, index),
            Block = block,
            InputType = inputType,
            OutputType = outputType,
            Index = index,
            LinkToNext = linkToNext
        };
    }

    /// <summary>
    /// Adds a block descriptor to the blocks list after validating the name is unique.
    /// </summary>
    /// <param name="blocks">The list of existing blocks.</param>
    /// <param name="blockNames">The set of existing block names.</param>
    /// <param name="name">The name for the block (null for auto-generated).</param>
    /// <param name="block">The dataflow block instance.</param>
    /// <param name="inputType">The input type of the block.</param>
    /// <param name="outputType">The output type of the block (null for terminal blocks).</param>
    /// <param name="linkToNext">The delegate to link this block to the next (null for terminal blocks).</param>
    /// <exception cref="ArgumentException">Thrown when a block with the same name already exists.</exception>
    public static void AddBlock(
        List<BlockDescriptor> blocks,
        HashSet<string> blockNames,
        string? name,
        IDataflowBlock block,
        Type inputType,
        Type? outputType,
        Action<IDataflowBlock, DataflowLinkOptions>? linkToNext)
    {
        var index = blocks.Count;
        var actualName = name ?? GenerateBlockName(block, index);

        if (!blockNames.Add(actualName))
        {
            throw new ArgumentException($"A block with the name '{actualName}' already exists in the pipeline.", nameof(name));
        }

        blocks.Add(new BlockDescriptor
        {
            Name = actualName,
            Block = block,
            InputType = inputType,
            OutputType = outputType,
            Index = index,
            LinkToNext = linkToNext
        });
    }
}
