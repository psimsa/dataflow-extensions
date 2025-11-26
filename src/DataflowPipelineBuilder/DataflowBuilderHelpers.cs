using System.Threading.Channels;
using System.Threading.Tasks.Dataflow;
using Tpl.Dataflow.Builder.Blocks;

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
    /// Also applies the ensureOrdered setting.
    /// </summary>
    /// <param name="options">The options to modify (may be null).</param>
    /// <param name="defaultCancellationToken">The default cancellation token.</param>
    /// <param name="ensureOrdered">Whether to preserve input order in output. Default is false for better parallel performance.</param>
    /// <returns>The modified options, or a new instance if options was null.</returns>
    public static ExecutionDataflowBlockOptions ApplyExecutionOptions(
        ExecutionDataflowBlockOptions? options,
        CancellationToken defaultCancellationToken,
        bool ensureOrdered)
    {
        if (options is null)
        {
            options = new ExecutionDataflowBlockOptions
            {
                EnsureOrdered = ensureOrdered
            };
        }
        else if (options.EnsureOrdered != ensureOrdered)
        {
            // Copy all relevant properties to a new instance
            var newOptions = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
                BoundedCapacity = options.BoundedCapacity,
                CancellationToken = options.CancellationToken,
                MaxMessagesPerTask = options.MaxMessagesPerTask,
                NameFormat = options.NameFormat,
                TaskScheduler = options.TaskScheduler,
                EnsureOrdered = ensureOrdered
            };
            options = newOptions;
        }
        if (defaultCancellationToken != default && options.CancellationToken == default)
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
    [Obsolete("Use ApplyExecutionOptions instead to also set EnsureOrdered.")]
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

    /// <summary>
    /// Starts a background task that pumps items from a ChannelReader to a target block.
    /// </summary>
    /// <typeparam name="T">The type of items being pumped.</typeparam>
    /// <param name="reader">The channel reader to consume from.</param>
    /// <param name="target">The target block to post items to.</param>
    /// <param name="cancellationToken">Cancellation token to stop the pumping task.</param>
    /// <remarks>
    /// The task completes the target block when the channel reader completes.
    /// If an exception occurs, the target block is faulted.
    /// This fire-and-forget pattern is safe because exceptions are handled within PumpChannelToBlockAsync,
    /// which faults the target block if an error occurs. The target block's completion task provides
    /// observability of any errors through the pipeline's overall completion.
    /// </remarks>
    public static void StartChannelPumpingTask<T>(
        ChannelReader<T> reader,
        ITargetBlock<T> target,
        CancellationToken cancellationToken)
    {
        _ = PumpChannelToBlockAsync(reader, target, cancellationToken);
    }

    private static async Task PumpChannelToBlockAsync<T>(
        ChannelReader<T> reader,
        ITargetBlock<T> target,
        CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (reader.TryRead(out var item))
                {
                    await target.SendAsync(item, cancellationToken).ConfigureAwait(false);
                }
            }

            target.Complete();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            target.Complete();
        }
        catch (Exception ex)
        {
            ((IDataflowBlock)target).Fault(ex);
        }
    }

    /// <summary>
    /// Creates a channel and an ActionBlock that writes to it.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <param name="options">Bounded channel options, or null for unbounded.</param>
    /// <param name="cancellationToken">Cancellation token for the action block.</param>
    /// <returns>A tuple containing the ActionBlock and the ChannelReader.</returns>
    public static (ActionBlock<T> Block, ChannelReader<T> Reader) CreateChannelOutputBlock<T>(
        BoundedChannelOptions? options,
        CancellationToken cancellationToken)
    {
        var channel = options is null
            ? Channel.CreateUnbounded<T>()
            : Channel.CreateBounded<T>(options);

        var writer = channel.Writer;

        var actionBlock = new ActionBlock<T>(
            async item => await writer.WriteAsync(item).ConfigureAwait(false),
            new ExecutionDataflowBlockOptions
            {
                CancellationToken = cancellationToken
            });

        actionBlock.Completion.ContinueWith(
            task =>
            {
                if (task.IsFaulted)
                {
                    writer.Complete(task.Exception?.InnerException);
                }
                else
                {
                    writer.Complete();
                }
            },
            TaskScheduler.Default);

        return (actionBlock, channel.Reader);
    }
}
