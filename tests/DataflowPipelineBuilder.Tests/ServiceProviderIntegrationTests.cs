using System.Threading.Tasks.Dataflow;
using Tpl.Dataflow.Builder.Abstractions;

namespace Tpl.Dataflow.Builder.Tests;

public class ServiceProviderIntegrationTests
{
    [Fact]
    public async Task AddCustomBlock_WithServiceProvider_ResolvesFromDI()
    {
        var serviceProvider = new TestServiceProvider();
        serviceProvider.Register<TestDoublerBlock>(() => new TestDoublerBlock());

        var results = new List<int>();
        var pipeline = new DataflowPipelineBuilder(serviceProvider: serviceProvider)
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
        var serviceProvider = new TestServiceProvider();
        serviceProvider.Register(() => new TestLoggingBlock(logger));

        var results = new List<string>();
        var pipeline = new DataflowPipelineBuilder(serviceProvider: serviceProvider)
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
        var builder = new DataflowPipelineBuilder()
            .AddBufferBlock<int>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddCustomBlock<TestDoublerBlock, int>());

        Assert.Contains("no IServiceProvider was configured", ex.Message);
        Assert.Contains(nameof(TestDoublerBlock), ex.Message);
    }

    [Fact]
    public void AddCustomBlock_WithUnregisteredType_ThrowsInvalidOperationException()
    {
        var serviceProvider = new TestServiceProvider();

        var builder = new DataflowPipelineBuilder(serviceProvider: serviceProvider)
            .AddBufferBlock<int>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddCustomBlock<TestDoublerBlock, int>());

        Assert.Contains("Unable to resolve service", ex.Message);
        Assert.Contains(nameof(TestDoublerBlock), ex.Message);
    }

    [Fact]
    public async Task ServiceProvider_PropagatesThroughChainedBuilders()
    {
        var serviceProvider = new TestServiceProvider();
        serviceProvider.Register<TestDoublerBlock>(() => new TestDoublerBlock());
        serviceProvider.Register<TestStringifierBlock>(() => new TestStringifierBlock());

        var results = new List<string>();
        var pipeline = new DataflowPipelineBuilder(serviceProvider: serviceProvider)
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
        var serviceProvider = new TestServiceProvider();
        serviceProvider.Register<TestDoublerBlock>(() => new TestDoublerBlock());

        var results = new List<int>();
        var pipeline = new DataflowPipelineBuilder(serviceProvider: serviceProvider)
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
        var serviceProvider = new TestServiceProvider();
        serviceProvider.Register<TestDoublerBlock>(() => new TestDoublerBlock());

        var pipeline = new DataflowPipelineBuilder(serviceProvider: serviceProvider)
            .AddBufferBlock<int>()
            .AddCustomBlock<TestDoublerBlock, int>(name: "MyDoubler")
            .AddActionBlock(_ => { })
            .Build();

        Assert.True(pipeline.Blocks.ContainsKey("MyDoubler"));

        pipeline.Complete();
        await pipeline.Completion;
    }

    public sealed class TestDoublerBlock : PropagatorBlock<int, int>
    {
        protected override int Transform(int input) => input * 2;
    }

    public sealed class TestStringifierBlock : PropagatorBlock<int, string>
    {
        protected override string Transform(int input) => input.ToString();
    }

    public sealed class TestLoggingBlock : PropagatorBlock<string, string>
    {
        private readonly TestLogger _logger;

        public TestLoggingBlock(TestLogger logger) => _logger = logger;

        protected override string Transform(string input)
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

    private sealed class TestServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, Func<object>> _factories = new();

        public void Register<T>(Func<T> factory) where T : class
        {
            _factories[typeof(T)] = factory;
        }

        public object? GetService(Type serviceType)
        {
            return _factories.TryGetValue(serviceType, out var factory) ? factory() : null;
        }
    }
}
