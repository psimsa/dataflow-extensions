using System.Threading.Tasks.Dataflow;
using Tpl.Dataflow.Builder;

namespace Tpl.Dataflow.Builder.Tests;

public class DataflowPipelineExecutionTests
{
    [Fact]
    public async Task Pipeline_ProcessesItems_WithTransformBlock()
    {
        await using var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .AddTransformBlock<string>(x => $"Number: {x}")
            .Build();

        pipeline.Post(42);
        pipeline.Complete();

        var result = await pipeline.ReceiveAsync();

        result.Should().Be("Number: 42");
    }

    [Fact]
    public async Task Pipeline_ProcessesItems_WithAsyncTransformBlock()
    {
        await using var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .AddTransformBlock<string>(async x =>
            {
                await Task.Delay(10);
                return $"Async: {x}";
            })
            .Build();

        pipeline.Post(42);
        pipeline.Complete();

        var result = await pipeline.ReceiveAsync();

        result.Should().Be("Async: 42");
    }

    [Fact]
    public async Task Pipeline_ProcessesItems_ToAsyncEnumerable()
    {
        await using var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .AddTransformBlock<int>(x => x * 2)
            .Build();

        pipeline.Post(1);
        pipeline.Post(2);
        pipeline.Post(3);
        pipeline.Complete();

        var results = new List<int>();
        await foreach (var item in pipeline.ToAsyncEnumerable())
        {
            results.Add(item);
        }

        results.Should().BeEquivalentTo([2, 4, 6]);
    }

    [Fact]
    public async Task Pipeline_ProcessesItems_WithTransformManyBlock()
    {
        await using var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<string>()
            .AddTransformManyBlock<char>(s => s.ToCharArray())
            .Build();

        pipeline.Post("ABC");
        pipeline.Complete();

        var results = new List<char>();
        await foreach (var item in pipeline.ToAsyncEnumerable())
        {
            results.Add(item);
        }

        results.Should().BeEquivalentTo(['A', 'B', 'C']);
    }

    [Fact]
    public async Task Pipeline_ProcessesItems_WithBatchBlock()
    {
        await using var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .AddBatchBlock(3)
            .Build();

        pipeline.Post(1);
        pipeline.Post(2);
        pipeline.Post(3);
        pipeline.Post(4);
        pipeline.Post(5);
        pipeline.Post(6);
        pipeline.Complete();

        var results = new List<int[]>();
        await foreach (var batch in pipeline.ToAsyncEnumerable())
        {
            results.Add(batch);
        }

        results.Should().HaveCount(2);
        results[0].Should().BeEquivalentTo([1, 2, 3]);
        results[1].Should().BeEquivalentTo([4, 5, 6]);
    }

    [Fact]
    public async Task TerminalPipeline_ProcessesItems_WithActionBlock()
    {
        var results = new List<int>();

        await using var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .AddActionBlock(x => results.Add(x * 10))
            .Build();

        pipeline.Post(1);
        pipeline.Post(2);
        pipeline.Post(3);
        pipeline.Complete();

        await pipeline.Completion;

        results.Should().BeEquivalentTo([10, 20, 30]);
    }

    [Fact]
    public async Task TerminalPipeline_ProcessesItems_WithAsyncActionBlock()
    {
        var results = new List<int>();
        var semaphore = new SemaphoreSlim(1);

        await using var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .AddActionBlock(
                async (int x) =>
                {
                    await Task.Delay(10);
                    await semaphore.WaitAsync();
                    try
                    {
                        results.Add(x * 10);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
            )
            .Build();

        pipeline.Post(1);
        pipeline.Post(2);
        pipeline.Post(3);
        pipeline.Complete();

        await pipeline.Completion;

        results.Should().BeEquivalentTo([10, 20, 30]);
    }

    [Fact]
    public async Task Pipeline_SendAsync_ReturnsTrue_WhenAccepted()
    {
        await using var pipeline = new DataflowPipelineBuilder().AddBufferBlock<int>().Build();

        var accepted = await pipeline.SendAsync(42);

        accepted.Should().BeTrue();
    }

    [Fact]
    public async Task Pipeline_TryReceive_ReturnsTrue_WhenItemAvailable()
    {
        await using var pipeline = new DataflowPipelineBuilder().AddBufferBlock<int>().Build();

        pipeline.Post(42);
        await Task.Delay(50);

        var received = pipeline.TryReceive(out var item);

        received.Should().BeTrue();
        item.Should().Be(42);
    }

    [Fact]
    public async Task Pipeline_Completion_CompletesWhenPipelineCompletes()
    {
        await using var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<int>()
            .AddTransformBlock<int>(x => x)
            .Build();

        var completionTask = pipeline.Completion;

        pipeline.Complete();
        await completionTask;

        completionTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Pipeline_AsObservable_EmitsItems()
    {
        await using var pipeline = new DataflowPipelineBuilder().AddBufferBlock<int>().Build();

        var results = new List<int>();
        var tcs = new TaskCompletionSource();

        var observable = pipeline.AsObservable();
        using var subscription = observable.Subscribe(
            new DelegateObserver<int>(
                onNext: x => results.Add(x),
                onCompleted: () => tcs.SetResult()
            )
        );

        pipeline.Post(1);
        pipeline.Post(2);
        pipeline.Post(3);
        pipeline.Complete();

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        results.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public async Task Pipeline_WithCancellation_RespectsCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        await using var pipeline = new DataflowPipelineBuilder(defaultCancellationToken: cts.Token)
            .AddBufferBlock<int>()
            .Build();

        cts.Cancel();

        var act = async () => await pipeline.Completion;

        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task Pipeline_ComplexChain_ProcessesCorrectly()
    {
        await using var pipeline = new DataflowPipelineBuilder()
            .AddBufferBlock<string>()
            .AddTransformBlock<int>(int.Parse)
            .AddTransformBlock<int>(x => x * 2)
            .AddBatchBlock(2)
            .AddTransformBlock<int>(batch => batch.Sum())
            .Build();

        pipeline.Post("10");
        pipeline.Post("20");
        pipeline.Post("30");
        pipeline.Post("40");
        pipeline.Complete();

        var results = new List<int>();
        await foreach (var item in pipeline.ToAsyncEnumerable())
        {
            results.Add(item);
        }

        results.Should().BeEquivalentTo([60, 140]);
    }
}

internal sealed class DelegateObserver<T>(
    Action<T> onNext,
    Action? onCompleted = null,
    Action<Exception>? onError = null
) : IObserver<T>
{
    public void OnCompleted() => onCompleted?.Invoke();

    public void OnError(Exception error) => onError?.Invoke(error);

    public void OnNext(T value) => onNext(value);
}
