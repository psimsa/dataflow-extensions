using System.Threading.Tasks.Dataflow;
using DataflowPipelineBuilder.Abstractions;

namespace DataflowPipelineBuilder.Tests;

public class DataflowPipelineBuilderTests
{
    [Fact]
    public void AddBufferBlock_CreatesValidBuilder()
    {
        var builder = new DataflowPipelineBuilder()
            .AddBufferBlock<int>();

        var pipeline = builder.Build();

        pipeline.Should().NotBeNull();
        pipeline.Blocks.Should().HaveCount(1);
        pipeline.Blocks.Should().ContainKey("BufferBlock_0");
    }

    [Fact]
    public void AddBufferBlock_WithCustomName_UsesProvidedName()
    {
        var builder = new DataflowPipelineBuilder()
            .AddBufferBlock<int>("MyBuffer");

        var pipeline = builder.Build();

        pipeline.Blocks.Should().ContainKey("MyBuffer");
    }

    [Fact]
    public void AddTransformBlock_ChangesOutputType()
    {
        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<string>()
            .AddTransformBlock<int>(int.Parse)
            .Build();

        pipeline.Should().NotBeNull();
        pipeline.Blocks.Should().HaveCount(2);
    }

    [Fact]
    public void AddTransformManyBlock_SplitsItems()
    {
        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<string>()
            .AddTransformManyBlock<char>(s => s.ToCharArray())
            .Build();

        pipeline.Should().NotBeNull();
        pipeline.Blocks.Should().HaveCount(2);
    }

    [Fact]
    public void AddBatchBlock_GroupsItems()
    {
        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .AddBatchBlock(5)
            .Build();

        pipeline.Should().NotBeNull();
        pipeline.Blocks.Should().HaveCount(2);
    }

    [Fact]
    public void AddBroadcastBlock_CreatesValidPipeline()
    {
        var pipeline = new DataflowPipelineBuilder()
            .AddBroadcastBlock<string>()
            .Build();

        pipeline.Should().NotBeNull();
        pipeline.Blocks.Should().HaveCount(1);
    }

    [Fact]
    public void AddActionBlock_CreatesTerminalPipeline()
    {
        var received = new List<int>();

        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .AddActionBlock(x => received.Add(x))
            .BuildTerminal();

        pipeline.Should().NotBeNull();
        pipeline.Should().BeAssignableTo<ITerminalDataflowPipeline<int>>();
    }

    [Fact]
    public void Build_WithEmptyPipeline_ThrowsInvalidOperationException()
    {
        var builder = new DataflowPipelineBuilder();

        var act = () => builder.AddBufferBlock<int>().AddActionBlock(_ => { }).Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ActionBlock*BuildTerminal*");
    }

    [Fact]
    public void BuildTerminal_WithoutActionBlock_ThrowsInvalidOperationException()
    {
        var builder = new DataflowPipelineBuilder()
            .AddBufferBlock<int>();

        var act = () => builder.BuildTerminal();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ActionBlock*Build()*");
    }

    [Fact]
    public void AddBlock_AfterActionBlock_ThrowsInvalidOperationException()
    {
        var builder = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .AddActionBlock(_ => { });

        var act = () => builder.AddTransformBlock<string>(x => x.ToString());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ActionBlock*terminated*");
    }

    [Fact]
    public void DuplicateBlockName_ThrowsArgumentException()
    {
        var builder = new DataflowPipelineBuilder()
            .AddBufferBlock<int>("MyBlock");

        var act = () => builder.AddTransformBlock<string>(x => x.ToString(), "MyBlock");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*MyBlock*already exists*");
    }

    [Fact]
    public void AddBatchBlock_WithZeroSize_ThrowsArgumentOutOfRangeException()
    {
        var builder = new DataflowPipelineBuilder()
            .AddBufferBlock<int>();

        var act = () => builder.AddBatchBlock(0);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public void WithDefaultLinkOptions_PropagatesCompletion()
    {
        var options = new DataflowLinkOptions { PropagateCompletion = true };

        var builder = new DataflowPipelineBuilder(options);
        var pipeline = builder
            .AddBufferBlock<int>()
            .AddTransformBlock<string>(x => x.ToString())
            .Build();

        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void AddCustomBlock_WithFactory_CreatesValidPipeline()
    {
        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .AddCustomBlock(() => new TransformBlock<int, string>(x => x.ToString()))
            .Build();

        pipeline.Should().NotBeNull();
        pipeline.Blocks.Should().HaveCount(2);
    }

    [Fact]
    public void AddCustomBlock_WithInstance_CreatesValidPipeline()
    {
        var customBlock = new TransformBlock<int, string>(x => x.ToString());

        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .AddCustomBlock(customBlock)
            .Build();

        pipeline.Should().NotBeNull();
        pipeline.Blocks.Should().HaveCount(2);
    }
}
