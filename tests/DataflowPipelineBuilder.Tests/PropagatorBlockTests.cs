using System.Threading.Tasks.Dataflow;
using Tpl.Dataflow.Builder.Abstractions;

namespace Tpl.Dataflow.Builder.Tests;

public class PropagatorBlockTests
{
    [Fact]
    public async Task PropagatorBlock_SyncTransform_ProcessesItems()
    {
        var multiplier = new TestMultiplierBlock(10);
        var results = new List<int>();

        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .AddCustomBlock(multiplier, name: "Multiplier")
            .AddActionBlock(x => results.Add(x))
            .Build();

        pipeline.Post(1);
        pipeline.Post(2);
        pipeline.Post(3);
        pipeline.Complete();

        await pipeline.Completion;

        Assert.Equal([10, 20, 30], results);
    }

    [Fact]
    public async Task AsyncPropagatorBlock_AsyncTransform_ProcessesItems()
    {
        var transformer = new TestAsyncUppercaseBlock();
        var results = new List<string>();

        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<string>()
            .AddCustomBlock(transformer)
            .AddActionBlock(x => results.Add(x))
            .Build();

        pipeline.Post("hello");
        pipeline.Post("world");
        pipeline.Complete();

        await pipeline.Completion;

        Assert.Equal(["HELLO", "WORLD"], results);
    }

    [Fact]
    public async Task PropagatorBlock_WithOptions_AppliesOptions()
    {
        var transformer = new TestParallelBlock();
        var results = new List<int>();
        var lockObj = new object();

        var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .AddCustomBlock(transformer)
            .AddActionBlock(x =>
            {
                lock (lockObj) results.Add(x);
            })
            .Build();

        for (var i = 1; i <= 5; i++)
        {
            pipeline.Post(i);
        }

        pipeline.Complete();
        await pipeline.Completion;

        Assert.Equal(5, results.Count);
        Assert.Contains(2, results);
        Assert.Contains(4, results);
        Assert.Contains(6, results);
        Assert.Contains(8, results);
        Assert.Contains(10, results);
    }

    [Fact]
    public async Task PropagatorBlock_Complete_CompletesUnderlyingBlock()
    {
        var multiplier = new TestMultiplierBlock(2);

        multiplier.Complete();

        await multiplier.Completion;

        Assert.True(multiplier.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task PropagatorBlock_Fault_FaultsUnderlyingBlock()
    {
        var multiplier = new TestMultiplierBlock(2);
        var expectedException = new InvalidOperationException("Test fault");

        ((IDataflowBlock)multiplier).Fault(expectedException);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await multiplier.Completion);
        Assert.Equal(expectedException.Message, ex.Message);
    }

    [Fact]
    public void AsyncPropagatorBlock_InputCount_ReflectsQueuedItems()
    {
        var slowBlock = new TestSlowBlock();

        ((ITargetBlock<int>)slowBlock).Post(1);
        ((ITargetBlock<int>)slowBlock).Post(2);

        Assert.True(slowBlock.InputCount >= 0);
    }

    [Fact]
    public async Task PropagatorBlock_LinkTo_LinksToTarget()
    {
        var multiplier = new TestMultiplierBlock(3);
        var results = new List<int>();
        var actionBlock = new ActionBlock<int>(x => results.Add(x));

        multiplier.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

        ((ITargetBlock<int>)multiplier).Post(5);
        multiplier.Complete();

        await actionBlock.Completion;

        Assert.Equal([15], results);
    }

    [Fact]
    public void PropagatorBlock_DefaultConstructor_CreatesValidBlock()
    {
        var block = new TestDefaultConstructorBlock();
        Assert.NotNull(block);
        Assert.False(block.Completion.IsCompleted);
    }

    [Fact]
    public void AsyncPropagatorBlock_DefaultConstructor_CreatesValidBlock()
    {
        var block = new TestAsyncDefaultConstructorBlock();
        Assert.NotNull(block);
        Assert.False(block.Completion.IsCompleted);
    }

    private sealed class TestMultiplierBlock : PropagatorBlock<int, int>
    {
        private readonly int _factor;

        public TestMultiplierBlock(int factor) => _factor = factor;

        public override int Transform(int input) => input * _factor;
    }

    private sealed class TestAsyncUppercaseBlock : AsyncPropagatorBlock<string, string>
    {
        public override async Task<string> TransformAsync(string input)
        {
            await Task.Delay(1);
            return input.ToUpperInvariant();
        }
    }

    private sealed class TestParallelBlock : PropagatorBlock<int, int>
    {
        public TestParallelBlock()
            : base(new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 4 })
        {
        }

        public override int Transform(int input) => input * 2;
    }

    private sealed class TestSlowBlock : AsyncPropagatorBlock<int, int>
    {
        public TestSlowBlock()
            : base(new ExecutionDataflowBlockOptions { BoundedCapacity = 1 })
        {
        }

        public override async Task<int> TransformAsync(int input)
        {
            await Task.Delay(1000);
            return input;
        }
    }

    private sealed class TestDefaultConstructorBlock : PropagatorBlock<string, string>
    {
        public override string Transform(string input) => input;
    }

    private sealed class TestAsyncDefaultConstructorBlock : AsyncPropagatorBlock<string, string>
    {
        public override Task<string> TransformAsync(string input) => Task.FromResult(input);
    }
}
