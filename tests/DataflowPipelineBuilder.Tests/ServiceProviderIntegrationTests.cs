using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.DependencyInjection;
using Tpl.Dataflow.Builder.Abstractions;

namespace Tpl.Dataflow.Builder.Tests;

public class ServiceProviderIntegrationTests
{
    [Fact]
    public async Task AddCustomBlock_WithServiceProvider_ResolvesFromDI()
    {
        var sp = new ServiceCollection().AddSingleton<TestDoublerBlock>().BuildServiceProvider();

        var results = new List<int>();
        var pipeline = new DataflowPipelineBuilder(serviceProvider: sp)
            .AddBufferBlock<int>()
            .AddCustomBlock<TestDoublerBlock, int>()
            .AddActionBlock(x => results.Add(x))
            .Build();

        pipeline.Post(5);
        pipeline.Post(10);
        pipeline.Complete();

        await pipeline.Completion;

        Assert.Equal([10, 20], results);
    }

    [Fact]
    public async Task AddCustomBlock_WithServiceProvider_ResolvesWithDependencies()
    {
        var logger = new TestLogger();
        var sp = new ServiceCollection()
            .AddSingleton(logger)
            .AddSingleton<TestLoggingBlock>()
            .BuildServiceProvider();

        var results = new List<string>();
        var pipeline = new DataflowPipelineBuilder(serviceProvider: sp)
            .AddBufferBlock<string>()
            .AddCustomBlock<TestLoggingBlock, string>()
            .AddActionBlock(x => results.Add(x))
            .Build();

        pipeline.Post("hello");
        pipeline.Complete();

        await pipeline.Completion;

        Assert.Equal(["HELLO"], results);
        Assert.Single(logger.Messages);
        Assert.Contains("Transforming: hello", logger.Messages[0]);
    }

    [Fact]
    public void AddCustomBlock_WithoutServiceProvider_ThrowsInvalidOperationException()
    {
        var builder = new DataflowPipelineBuilder().AddBufferBlock<int>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddCustomBlock<TestDoublerBlock, int>()
        );

        Assert.Contains("no IServiceProvider was configured", ex.Message);
        Assert.Contains(nameof(TestDoublerBlock), ex.Message);
    }

    [Fact]
    public void AddCustomBlock_WithUnregisteredType_ThrowsInvalidOperationException()
    {
        var sp = new ServiceCollection().BuildServiceProvider();

        var builder = new DataflowPipelineBuilder(serviceProvider: sp).AddBufferBlock<int>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddCustomBlock<TestDoublerBlock, int>()
        );

        Assert.Contains("Unable to resolve service", ex.Message);
        Assert.Contains(nameof(TestDoublerBlock), ex.Message);
    }

    [Fact]
    public async Task ServiceProvider_PropagatesThroughChainedBuilders()
    {
        var sp = new ServiceCollection()
            .AddSingleton<TestDoublerBlock>()
            .AddSingleton<TestStringifierBlock>()
            .BuildServiceProvider();

        var results = new List<string>();
        var pipeline = new DataflowPipelineBuilder(serviceProvider: sp)
            .AddBufferBlock<int>()
            .AddCustomBlock<TestDoublerBlock, int>()
            .AddCustomBlock<TestStringifierBlock, string>()
            .AddActionBlock(x => results.Add(x))
            .Build();

        pipeline.Post(21);
        pipeline.Complete();

        await pipeline.Completion;

        Assert.Equal(["42"], results);
    }

    [Fact]
    public async Task ServiceProvider_WorksWithOtherBlockTypes()
    {
        var sp = new ServiceCollection().AddSingleton<TestDoublerBlock>().BuildServiceProvider();

        var results = new List<int>();
        var pipeline = new DataflowPipelineBuilder(serviceProvider: sp)
            .AddBufferBlock<int>()
            .AddTransformBlock(x => x + 1)
            .AddCustomBlock<TestDoublerBlock, int>()
            .AddTransformBlock(x => x - 1)
            .AddActionBlock(x => results.Add(x))
            .Build();

        pipeline.Post(5);
        pipeline.Complete();

        await pipeline.Completion;

        Assert.Equal([11], results);
    }

    [Fact]
    public async Task AddCustomBlock_WithName_UsesProvidedName()
    {
        var sp = new ServiceCollection().AddSingleton<TestDoublerBlock>().BuildServiceProvider();

        var pipeline = new DataflowPipelineBuilder(serviceProvider: sp)
            .AddBufferBlock<int>()
            .AddCustomBlock<TestDoublerBlock, int>(name: "MyDoubler")
            .AddActionBlock(_ => { })
            .Build();

        Assert.True(pipeline.Blocks.ContainsKey("MyDoubler"));

        pipeline.Complete();
        await pipeline.Completion;
    }

    [Fact]
    public async Task AddKeyedCustomBlock_ResolvesKeyedService()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IMultiplierBlock, DoublerBlock>("double");
        services.AddKeyedSingleton<IMultiplierBlock, TriplerBlock>("triple");
        var sp = services.BuildServiceProvider();

        var results = new List<int>();
        var pipeline = new DataflowPipelineBuilder(serviceProvider: sp)
            .AddBufferBlock<int>()
            .AddKeyedCustomBlock<IMultiplierBlock, int>("double")
            .AddActionBlock(x => results.Add(x))
            .Build();

        pipeline.Post(5);
        pipeline.Complete();
        await pipeline.Completion;

        Assert.Equal([10], results);
    }

    [Fact]
    public async Task AddKeyedCustomBlock_DifferentKeys_ResolveDifferentImplementations()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IMultiplierBlock, DoublerBlock>("double");
        services.AddKeyedSingleton<IMultiplierBlock, TriplerBlock>("triple");
        var sp = services.BuildServiceProvider();

        var doubleResults = new List<int>();
        var doublePipeline = new DataflowPipelineBuilder(serviceProvider: sp)
            .AddBufferBlock<int>()
            .AddKeyedCustomBlock<IMultiplierBlock, int>("double")
            .AddActionBlock(x => doubleResults.Add(x))
            .Build();

        var tripleResults = new List<int>();
        var triplePipeline = new DataflowPipelineBuilder(serviceProvider: sp)
            .AddBufferBlock<int>()
            .AddKeyedCustomBlock<IMultiplierBlock, int>("triple")
            .AddActionBlock(x => tripleResults.Add(x))
            .Build();

        doublePipeline.Post(10);
        triplePipeline.Post(10);
        doublePipeline.Complete();
        triplePipeline.Complete();

        await Task.WhenAll(doublePipeline.Completion, triplePipeline.Completion);

        Assert.Equal([20], doubleResults);
        Assert.Equal([30], tripleResults);
    }

    [Fact]
    public void AddKeyedCustomBlock_WithoutServiceProvider_ThrowsInvalidOperationException()
    {
        var builder = new DataflowPipelineBuilder().AddBufferBlock<int>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddKeyedCustomBlock<IMultiplierBlock, int>("double")
        );

        Assert.Contains("no IServiceProvider was configured", ex.Message);
        Assert.Contains("IMultiplierBlock", ex.Message);
    }

    [Fact]
    public void AddKeyedCustomBlock_WithUnregisteredKey_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IMultiplierBlock, DoublerBlock>("double");
        var sp = services.BuildServiceProvider();

        var builder = new DataflowPipelineBuilder(serviceProvider: sp).AddBufferBlock<int>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddKeyedCustomBlock<IMultiplierBlock, int>("nonexistent")
        );

        Assert.Contains("Unable to resolve keyed service", ex.Message);
        Assert.Contains("IMultiplierBlock", ex.Message);
        Assert.Contains("nonexistent", ex.Message);
    }

    [Fact]
    public async Task AddKeyedCustomBlock_WithName_UsesProvidedName()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IMultiplierBlock, DoublerBlock>("double");
        var sp = services.BuildServiceProvider();

        var pipeline = new DataflowPipelineBuilder(serviceProvider: sp)
            .AddBufferBlock<int>()
            .AddKeyedCustomBlock<IMultiplierBlock, int>("double", name: "MyKeyedDoubler")
            .AddActionBlock(_ => { })
            .Build();

        Assert.True(pipeline.Blocks.ContainsKey("MyKeyedDoubler"));

        pipeline.Complete();
        await pipeline.Completion;
    }

    [Fact]
    public void AddKeyedCustomBlock_WithNullKey_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();

        var builder = new DataflowPipelineBuilder(serviceProvider: sp).AddBufferBlock<int>();

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddKeyedCustomBlock<IMultiplierBlock, int>(null!)
        );
    }

    #region Custom Block as First Block Tests

    [Fact]
    public async Task AddCustomBlock_AsFirstBlock_WithServiceProvider_ResolvesFromDI()
    {
        var sp = new ServiceCollection().AddSingleton<TestDoublerBlock>().BuildServiceProvider();

        var results = new List<int>();
        var pipeline = new DataflowPipelineBuilder(serviceProvider: sp)
            .AddCustomBlock<TestDoublerBlock, int, int>()
            .AddActionBlock(x => results.Add(x))
            .Build();

        pipeline.Post(5);
        pipeline.Post(10);
        pipeline.Complete();

        await pipeline.Completion;

        Assert.Equal([10, 20], results);
    }

    [Fact]
    public async Task AddCustomBlock_AsFirstBlock_WithDependencies_ResolvesCorrectly()
    {
        var logger = new TestLogger();
        var sp = new ServiceCollection()
            .AddSingleton(logger)
            .AddSingleton<TestLoggingBlock>()
            .BuildServiceProvider();

        var results = new List<string>();
        var pipeline = new DataflowPipelineBuilder(serviceProvider: sp)
            .AddCustomBlock<TestLoggingBlock, string, string>()
            .AddActionBlock(x => results.Add(x))
            .Build();

        pipeline.Post("hello");
        pipeline.Complete();

        await pipeline.Completion;

        Assert.Equal(["HELLO"], results);
        Assert.Single(logger.Messages);
    }

    [Fact]
    public void AddCustomBlock_AsFirstBlock_WithoutServiceProvider_ThrowsInvalidOperationException()
    {
        var builder = new DataflowPipelineBuilder();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddCustomBlock<TestDoublerBlock, int, int>()
        );

        Assert.Contains("no IServiceProvider was configured", ex.Message);
        Assert.Contains(nameof(TestDoublerBlock), ex.Message);
    }

    [Fact]
    public void AddCustomBlock_AsFirstBlock_WithUnregisteredType_ThrowsInvalidOperationException()
    {
        var sp = new ServiceCollection().BuildServiceProvider();

        var builder = new DataflowPipelineBuilder(serviceProvider: sp);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddCustomBlock<TestDoublerBlock, int, int>()
        );

        Assert.Contains("Unable to resolve service", ex.Message);
        Assert.Contains(nameof(TestDoublerBlock), ex.Message);
    }

    [Fact]
    public async Task AddKeyedCustomBlock_AsFirstBlock_ResolvesKeyedService()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IMultiplierBlock, DoublerBlock>("double");
        services.AddKeyedSingleton<IMultiplierBlock, TriplerBlock>("triple");
        var sp = services.BuildServiceProvider();

        var results = new List<int>();
        var pipeline = new DataflowPipelineBuilder(serviceProvider: sp)
            .AddKeyedCustomBlock<IMultiplierBlock, int, int>("triple")
            .AddActionBlock(x => results.Add(x))
            .Build();

        pipeline.Post(5);
        pipeline.Complete();
        await pipeline.Completion;

        Assert.Equal([15], results);
    }

    [Fact]
    public void AddKeyedCustomBlock_AsFirstBlock_WithoutServiceProvider_ThrowsInvalidOperationException()
    {
        var builder = new DataflowPipelineBuilder();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddKeyedCustomBlock<IMultiplierBlock, int, int>("double")
        );

        Assert.Contains("no IServiceProvider was configured", ex.Message);
        Assert.Contains("IMultiplierBlock", ex.Message);
    }

    [Fact]
    public void AddKeyedCustomBlock_AsFirstBlock_WithUnregisteredKey_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IMultiplierBlock, DoublerBlock>("double");
        var sp = services.BuildServiceProvider();

        var builder = new DataflowPipelineBuilder(serviceProvider: sp);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddKeyedCustomBlock<IMultiplierBlock, int, int>("nonexistent")
        );

        Assert.Contains("Unable to resolve keyed service", ex.Message);
        Assert.Contains("IMultiplierBlock", ex.Message);
        Assert.Contains("nonexistent", ex.Message);
    }

    [Fact]
    public void AddKeyedCustomBlock_AsFirstBlock_WithNullKey_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();

        var builder = new DataflowPipelineBuilder(serviceProvider: sp);

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddKeyedCustomBlock<IMultiplierBlock, int, int>(null!)
        );
    }

    [Fact]
    public async Task AddCustomBlock_AsFirstBlock_WithInstance_Works()
    {
        var block = new TestDoublerBlock();
        var results = new List<int>();

        var pipeline = new DataflowPipelineBuilder()
            .AddCustomBlock<int, int>(block)
            .AddActionBlock(x => results.Add(x))
            .Build();

        pipeline.Post(7);
        pipeline.Post(3);
        pipeline.Complete();

        await pipeline.Completion;

        Assert.Equal([14, 6], results);
    }

    [Fact]
    public void AddCustomBlock_AsFirstBlock_WithNullInstance_ThrowsArgumentNullException()
    {
        var builder = new DataflowPipelineBuilder();

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddCustomBlock<int, int>((IPropagatorBlock<int, int>)null!)
        );
    }

    [Fact]
    public async Task AddCustomBlock_AsFirstBlock_WithFactory_Works()
    {
        var results = new List<int>();

        var pipeline = new DataflowPipelineBuilder()
            .AddCustomBlock(() => new TestDoublerBlock())
            .AddActionBlock(x => results.Add(x))
            .Build();

        pipeline.Post(4);
        pipeline.Post(8);
        pipeline.Complete();

        await pipeline.Completion;

        Assert.Equal([8, 16], results);
    }

    [Fact]
    public void AddCustomBlock_AsFirstBlock_WithNullFactory_ThrowsArgumentNullException()
    {
        var builder = new DataflowPipelineBuilder();

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddCustomBlock<int, int>((Func<IPropagatorBlock<int, int>>)null!)
        );
    }

    [Fact]
    public async Task AddCustomBlock_AsFirstBlock_WithName_UsesProvidedName()
    {
        var block = new TestDoublerBlock();

        var pipeline = new DataflowPipelineBuilder()
            .AddCustomBlock<int, int>(block, name: "FirstBlockDoubler")
            .AddActionBlock(_ => { })
            .Build();

        Assert.True(pipeline.Blocks.ContainsKey("FirstBlockDoubler"));

        pipeline.Complete();
        await pipeline.Completion;
    }

    [Fact]
    public async Task AddCustomBlock_AsFirstBlock_CanChainWithOtherBlocks()
    {
        var sp = new ServiceCollection()
            .AddSingleton<TestStringifierBlock>()
            .BuildServiceProvider();

        var results = new List<string>();
        var pipeline = new DataflowPipelineBuilder(serviceProvider: sp)
            .AddCustomBlock<int, int>(new TestDoublerBlock())
            .AddTransformBlock(x => x + 1)
            .AddCustomBlock<TestStringifierBlock, string>()
            .AddActionBlock(x => results.Add(x))
            .Build();

        pipeline.Post(10);
        pipeline.Complete();

        await pipeline.Completion;

        Assert.Equal(["21"], results);
    }

    [Fact]
    public async Task AddCustomBlock_AsFirstBlock_ServiceProviderPropagates()
    {
        var sp = new ServiceCollection()
            .AddSingleton<TestDoublerBlock>()
            .AddSingleton<TestStringifierBlock>()
            .BuildServiceProvider();

        var results = new List<string>();
        var pipeline = new DataflowPipelineBuilder(serviceProvider: sp)
            .AddCustomBlock<TestDoublerBlock, int, int>()
            .AddCustomBlock<TestStringifierBlock, string>()
            .AddActionBlock(x => results.Add(x))
            .Build();

        pipeline.Post(21);
        pipeline.Complete();

        await pipeline.Completion;

        Assert.Equal(["42"], results);
    }

    #endregion

    public interface IMultiplierBlock : IPropagatorBlock<int, int>;

    public sealed class DoublerBlock : PropagatorBlock<int, int>, IMultiplierBlock
    {
        public override int Transform(int input) => input * 2;
    }

    public sealed class TriplerBlock : PropagatorBlock<int, int>, IMultiplierBlock
    {
        public override int Transform(int input) => input * 3;
    }

    public sealed class TestDoublerBlock : PropagatorBlock<int, int>
    {
        public override int Transform(int input) => input * 2;
    }

    public sealed class TestStringifierBlock : PropagatorBlock<int, string>
    {
        public override string Transform(int input) => input.ToString();
    }

    public sealed class TestLoggingBlock : PropagatorBlock<string, string>
    {
        private readonly TestLogger _logger;

        public TestLoggingBlock(TestLogger logger) => _logger = logger;

        public override string Transform(string input)
        {
            _logger.Log($"Transforming: {input}");
            return input.ToUpperInvariant();
        }
    }

    public sealed class TestLogger
    {
        public List<string> Messages { get; } = [];

        public void Log(string message) => Messages.Add(message);
    }
}
