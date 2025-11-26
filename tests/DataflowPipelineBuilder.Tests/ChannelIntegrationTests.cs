using System.Threading.Channels;
using Tpl.Dataflow.Builder.Abstractions;

namespace Tpl.Dataflow.Builder.Tests;

public class ChannelIntegrationTests
{
    [Fact]
    public async Task FromChannelSource_WithUnboundedChannel_ProcessesItems()
    {
        var channel = Channel.CreateUnbounded<int>();
        var results = new List<int>();

        var pipeline = new DataflowPipelineBuilder()
            .FromChannelSource(channel)
            .AddTransformBlock(x => x * 2)
            .AddActionBlock(x => results.Add(x))
            .Build();

        await channel.Writer.WriteAsync(1);
        await channel.Writer.WriteAsync(2);
        await channel.Writer.WriteAsync(3);
        channel.Writer.Complete();

        await pipeline.Completion;

        Assert.Equal([2, 4, 6], results);
    }

    [Fact]
    public async Task FromChannelSource_WithChannelReader_ProcessesItems()
    {
        var channel = Channel.CreateUnbounded<string>();
        var results = new List<int>();

        var pipeline = new DataflowPipelineBuilder()
            .FromChannelSource(channel.Reader)
            .AddTransformBlock(int.Parse)
            .AddActionBlock(x => results.Add(x))
            .Build();

        await channel.Writer.WriteAsync("10");
        await channel.Writer.WriteAsync("20");
        channel.Writer.Complete();

        await pipeline.Completion;

        Assert.Equal([10, 20], results);
    }

    [Fact]
    public async Task FromChannelSource_WithBoundedChannel_ProcessesItems()
    {
        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(5));
        var results = new List<int>();

        var pipeline = new DataflowPipelineBuilder()
            .FromChannelSource(channel)
            .AddActionBlock(x => results.Add(x))
            .Build();

        for (var i = 0; i < 5; i++)
        {
            await channel.Writer.WriteAsync(i);
        }

        channel.Writer.Complete();
        await pipeline.Completion;

        Assert.Equal([0, 1, 2, 3, 4], results);
    }

    [Fact]
    public async Task FromChannelSource_WithCustomName_UsesProvidedName()
    {
        var channel = Channel.CreateUnbounded<int>();

        var pipeline = new DataflowPipelineBuilder()
            .FromChannelSource(channel, name: "ChannelInput")
            .AddActionBlock(_ => { })
            .Build();

        Assert.True(pipeline.Blocks.ContainsKey("ChannelInput"));

        channel.Writer.Complete();
        await pipeline.Completion;
    }

    [Fact]
    public async Task FromChannelSource_CompletesWhenChannelCompletes()
    {
        var channel = Channel.CreateUnbounded<int>();

        var pipeline = new DataflowPipelineBuilder()
            .FromChannelSource(channel)
            .AddActionBlock(_ => { })
            .Build();

        Assert.False(pipeline.Completion.IsCompleted);

        channel.Writer.Complete();
        await pipeline.Completion;

        Assert.True(pipeline.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task FromChannelSource_PropagatesChannelException()
    {
        var channel = Channel.CreateUnbounded<int>();
        var expectedException = new InvalidOperationException("Test exception");

        var pipeline = new DataflowPipelineBuilder()
            .FromChannelSource(channel)
            .AddActionBlock(_ => { })
            .Build();

        channel.Writer.Complete(expectedException);

        var completionException = await Assert.ThrowsAsync<AggregateException>(
            async () => await pipeline.Completion);

        Assert.Contains(completionException.Flatten().InnerExceptions,
            ex => ex.Message == expectedException.Message);
    }

    [Fact]
    public async Task BuildAsChannel_WithUnboundedChannel_ProducesOutput()
    {
        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .AddTransformBlock(x => x * 2)
            .BuildAsChannel();

        pipeline.Post(1);
        pipeline.Post(2);
        pipeline.Post(3);
        pipeline.Complete();

        var results = new List<int>();
        await foreach (var item in pipeline.Output.ReadAllAsync())
        {
            results.Add(item);
        }

        Assert.Equal([2, 4, 6], results);
    }

    [Fact]
    public async Task BuildAsChannel_WithBoundedChannel_ProducesOutput()
    {
        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .AddTransformBlock(x => x.ToString())
            .BuildAsChannel(new BoundedChannelOptions(10));

        pipeline.Post(42);
        pipeline.Complete();

        var result = await pipeline.Output.ReadAsync();
        Assert.Equal("42", result);
    }

    [Fact]
    public async Task BuildAsChannel_OutputIsIDataflowPipelineOfTInput()
    {
        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .BuildAsChannel();

        IDataflowPipeline<int> basePipeline = pipeline;

        Assert.True(basePipeline.Post(1));
        basePipeline.Complete();

        await basePipeline.Completion;
    }

    [Fact]
    public async Task BuildAsChannel_ExposesBlocks()
    {
        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>(name: "Input")
            .AddTransformBlock(x => x * 2, name: "Transform")
            .BuildAsChannel();

        Assert.True(pipeline.Blocks.ContainsKey("Input"));
        Assert.True(pipeline.Blocks.ContainsKey("Transform"));
        Assert.True(pipeline.Blocks.ContainsKey("ChannelOutput"));

        pipeline.Complete();
        await pipeline.Completion;
    }

    [Fact]
    public async Task BuildAsChannel_CompletesChannelOnPipelineCompletion()
    {
        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .BuildAsChannel();

        pipeline.Post(1);
        pipeline.Complete();

        var results = new List<int>();
        await foreach (var item in pipeline.Output.ReadAllAsync())
        {
            results.Add(item);
        }

        Assert.Single(results);
        Assert.Equal(1, results[0]);
        Assert.True(pipeline.Output.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task BuildAsChannel_SupportsAsyncEnumeration()
    {
        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<string>()
            .AddTransformBlock(s => s.ToUpperInvariant())
            .BuildAsChannel();

        pipeline.Post("hello");
        pipeline.Post("world");
        pipeline.Complete();

        var results = new List<string>();
        await foreach (var item in pipeline.Output.ReadAllAsync())
        {
            results.Add(item);
        }

        Assert.Equal(["HELLO", "WORLD"], results);
    }

    [Fact]
    public async Task BuildAsChannel_SupportsSendAsync()
    {
        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .BuildAsChannel();

        var accepted = await pipeline.SendAsync(42);
        Assert.True(accepted);

        pipeline.Complete();

        var result = await pipeline.Output.ReadAsync();
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task BuildAsChannel_CanBeDisposed()
    {
        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .BuildAsChannel();

        await pipeline.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => pipeline.Post(1));
    }

    [Fact]
    public async Task ChannelToChannel_FullIntegration()
    {
        var inputChannel = Channel.CreateUnbounded<int>();

        var pipeline = new DataflowPipelineBuilder()
            .FromChannelSource(inputChannel)
            .AddTransformBlock(x => x * 2)
            .AddTransformBlock(x => $"Result: {x}")
            .BuildAsChannel();

        await inputChannel.Writer.WriteAsync(21);
        await inputChannel.Writer.WriteAsync(10);
        inputChannel.Writer.Complete();

        var results = new List<string>();
        await foreach (var item in pipeline.Output.ReadAllAsync())
        {
            results.Add(item);
        }

        Assert.Equal(["Result: 42", "Result: 20"], results);
    }

    [Fact]
    public async Task ChannelToChannel_WithBatchBlock()
    {
        var inputChannel = Channel.CreateUnbounded<int>();

        var pipeline = new DataflowPipelineBuilder()
            .FromChannelSource(inputChannel)
            .AddBatchBlock(3)
            .AddTransformBlock(batch => batch.Sum())
            .BuildAsChannel();

        for (var i = 1; i <= 6; i++)
        {
            await inputChannel.Writer.WriteAsync(i);
        }

        inputChannel.Writer.Complete();

        var results = new List<int>();
        await foreach (var item in pipeline.Output.ReadAllAsync())
        {
            results.Add(item);
        }

        Assert.Equal([6, 15], results);
    }

    [Fact]
    public async Task FromChannelSource_WithCancellation_StopsPumping()
    {
        var channel = Channel.CreateUnbounded<int>();
        using var cts = new CancellationTokenSource();
        var results = new List<int>();

        var pipeline = new DataflowPipelineBuilder(defaultCancellationToken: cts.Token)
            .FromChannelSource(channel)
            .AddActionBlock(x => results.Add(x))
            .Build();

        await channel.Writer.WriteAsync(1);
        await channel.Writer.WriteAsync(2);

        await Task.Delay(100);

        cts.Cancel();

        await Task.Delay(100);

        await channel.Writer.WriteAsync(3);
        channel.Writer.Complete();

        try
        {
            await pipeline.Completion;
        }
        catch
        {
            // Expected - cancellation causes completion
        }

        Assert.Contains(1, results);
        Assert.Contains(2, results);
        Assert.DoesNotContain(3, results);
    }

    [Fact]
    public void FromChannelSource_WithNullChannel_ThrowsArgumentNullException()
    {
        var builder = new DataflowPipelineBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.FromChannelSource<int>((Channel<int>)null!));
    }

    [Fact]
    public void FromChannelSource_WithNullReader_ThrowsArgumentNullException()
    {
        var builder = new DataflowPipelineBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.FromChannelSource<int>((ChannelReader<int>)null!));
    }
}
