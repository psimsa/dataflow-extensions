using Microsoft.Extensions.DependencyInjection;
using Tpl.Dataflow.Builder;

Console.WriteLine("=== DataflowPipelineBuilder Sample ===\n");

// Example 1: Simple Transform Pipeline
Console.WriteLine("Example 1: Simple Transform Pipeline");
Console.WriteLine("-------------------------------------");

var r = new Random();

await using var transformPipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<int>(options: new System.Threading.Tasks.Dataflow.DataflowBlockOptions
    {
        BoundedCapacity = 100
    })
    .AddTransformBlock<string>(async(x) => {
        await Task.Delay(r.Next(50, 2000));
        return $"Number: {x}";
    }, options: new System.Threading.Tasks.Dataflow.ExecutionDataflowBlockOptions
    {
        MaxDegreeOfParallelism = 50
    })
    .Build();

for (int i = 1; i <= 5; i++)
{
    transformPipeline.Post(i);
}

transformPipeline.Complete();

await foreach (var result in transformPipeline.ToAsyncEnumerable())
{
    Console.WriteLine($"  Received: {result}");
}

Console.WriteLine();

// Example 2: Async Transform with Parallelism
Console.WriteLine("Example 2: Async Transform with Parallelism");
Console.WriteLine("--------------------------------------------");

await using var asyncPipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<string>()
    .AddTransformBlock<string>(
        async url =>
        {
            await Task.Delay(100);
            return $"Processed: {url}";
        },
        options: new System.Threading.Tasks.Dataflow.ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = 4
        })
    .Build();

var urls = new[] { "url1", "url2", "url3", "url4", "url5" };
foreach (var url in urls)
{
    asyncPipeline.Post(url);
}

asyncPipeline.Complete();

await foreach (var result in asyncPipeline.ToAsyncEnumerable())
{
    Console.WriteLine($"  {result}");
}

Console.WriteLine();

// Example 3: Batch Processing
Console.WriteLine("Example 3: Batch Processing");
Console.WriteLine("----------------------------");

await using var batchPipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<int>()
    .AddBatchBlock(batchSize: 3)
    .AddTransformBlock<string>(
        batch => $"Batch of {batch.Length} items: [{string.Join(", ", batch)}]")
    .Build();

for (int i = 1; i <= 10; i++)
{
    batchPipeline.Post(i);
}

batchPipeline.Complete();

await foreach (var result in batchPipeline.ToAsyncEnumerable())
{
    Console.WriteLine($"  {result}");
}

Console.WriteLine();

// Example 4: Terminal Pipeline with ActionBlock
Console.WriteLine("Example 4: Terminal Pipeline with ActionBlock");
Console.WriteLine("----------------------------------------------");

var processedItems = new List<string>();

await using var terminalPipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<int>()
    .AddTransformBlock<string>(x => $"Item #{x}")
    .AddActionBlock(item =>
    {
        processedItems.Add(item);
        Console.WriteLine($"  Processing: {item}");
    })
    .Build();

for (int i = 1; i <= 5; i++)
{
    await terminalPipeline.SendAsync(i);
}

terminalPipeline.Complete();
await terminalPipeline.Completion;

Console.WriteLine($"  Total processed: {processedItems.Count}");
Console.WriteLine();

// Example 5: TransformMany - Expanding Data
Console.WriteLine("Example 5: TransformMany - Expanding Data");
Console.WriteLine("------------------------------------------");

await using var expandPipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<string>()
    .AddTransformManyBlock<char>(s => s.ToCharArray())
    .Build();

expandPipeline.Post("Hello");
expandPipeline.Complete();

await foreach (var c in expandPipeline.ToAsyncEnumerable())
{
    Console.Write($"'{c}' ");
}

Console.WriteLine("\n");

// Example 6: Named Blocks for Debugging
Console.WriteLine("Example 6: Named Blocks (for debugging)");
Console.WriteLine("----------------------------------------");

await using var namedPipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<int>("InputBuffer")
    .AddTransformBlock<int>(x => x * 2, "Doubler")
    .AddTransformBlock<string>(x => x.ToString(), "Stringifier")
    .Build();

namedPipeline.Post(21);
namedPipeline.Complete();

await foreach (var result in namedPipeline.ToAsyncEnumerable())
{
    Console.WriteLine($"  Result: {result}");
}

Console.WriteLine();

var sp = new ServiceCollection()
    .AddSingleton<CustomBlock>()
    .AddSingleton<AsyncCustomBlock>()
    .AddSingleton<SplitterBlock>()
    .AddSingleton<AsyncSplitterBlock>()
    .AddKeyedSingleton<IMultiplierBlock, DoublerBlock>("double")
    .AddKeyedSingleton<IMultiplierBlock, TriplerBlock>("triple")
    .BuildServiceProvider();

// Example 7: Custom Blocks from Service Provider
Console.WriteLine("Example 7: Custom Blocks from Service Provider");
Console.WriteLine("-----------------------------------------------");

await using var customBlockPipeline = new DataflowPipelineBuilder(serviceProvider: sp)
    .AddBufferBlock<int>()
    .AddCustomBlock<CustomBlock, int>()
    .AddCustomBlock<AsyncCustomBlock, string>()
    .AddActionBlock(x => Console.WriteLine($"  Final Output: {x}"))
    .Build();

for (int i = 1; i <= 3; i++)
{
    customBlockPipeline.Post(i);
}
customBlockPipeline.Complete();
await customBlockPipeline.Completion;

Console.WriteLine();

// Example 8: Keyed Services - Different implementations of same interface
Console.WriteLine("Example 8: Keyed Services");
Console.WriteLine("--------------------------");

Console.WriteLine("  Using 'double' key (multiplies by 2):");
await using var doublePipeline = new DataflowPipelineBuilder(serviceProvider: sp)
    .AddBufferBlock<int>()
    .AddKeyedCustomBlock<IMultiplierBlock, int>("double")
    .AddActionBlock(x => Console.WriteLine($"    Input * 2 = {x}"))
    .Build();

doublePipeline.Post(5);
doublePipeline.Post(10);
doublePipeline.Complete();
await doublePipeline.Completion;

Console.WriteLine("  Using 'triple' key (multiplies by 3):");
await using var triplePipeline = new DataflowPipelineBuilder(serviceProvider: sp)
    .AddBufferBlock<int>()
    .AddKeyedCustomBlock<IMultiplierBlock, int>("triple")
    .AddActionBlock(x => Console.WriteLine($"    Input * 3 = {x}"))
    .Build();

triplePipeline.Post(5);
triplePipeline.Post(10);
triplePipeline.Complete();
await triplePipeline.Completion;

// Example 9: Custom Many Blocks (synchronous)
Console.WriteLine("Example 9: Custom Many Blocks (synchronous)");
Console.WriteLine("---------------------------------------------");

var syncSplitter = new SplitterBlock();
var syncResults = new List<char>();

await using var syncManyPipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<string>()
    .AddCustomBlock(syncSplitter)
    .AddActionBlock(x =>
    {
        syncResults.Add(x);
        Console.WriteLine($"  Received: {x}");
    })
    .Build();

syncManyPipeline.Post("ab");
syncManyPipeline.Post("cd");
syncManyPipeline.Complete();

await syncManyPipeline.Completion;

Console.WriteLine();

// Example 10: Custom Many Blocks (async)
Console.WriteLine("Example 10: Custom Many Blocks (async)");
Console.WriteLine("-----------------------------------------");

var asyncSplitter = new AsyncSplitterBlock();
var asyncResults = new List<char>();

await using var asyncManyPipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<string>()
    .AddCustomBlock(asyncSplitter)
    .AddActionBlock(x =>
    {
        asyncResults.Add(x);
        Console.WriteLine($"  Received (async): {x}");
    })
    .Build();

asyncManyPipeline.Post("hi");
asyncManyPipeline.Post("yo");
asyncManyPipeline.Complete();

await asyncManyPipeline.Completion;

Console.WriteLine();

// Example 11: DI-resolved Many Blocks
Console.WriteLine("Example 11: DI-resolved Many Blocks");
Console.WriteLine("------------------------------------");

await using var diSyncMany = new DataflowPipelineBuilder(serviceProvider: sp)
    .AddBufferBlock<string>()
    .AddCustomBlock<SplitterBlock, char>()
    .AddActionBlock(x => Console.WriteLine($"  DI Received: {x}"))
    .Build();

await diSyncMany.SendAsync("ab");
diSyncMany.Complete();
await diSyncMany.Completion;

await using var diAsyncMany = new DataflowPipelineBuilder(serviceProvider: sp)
    .AddBufferBlock<string>()
    .AddCustomBlock<AsyncSplitterBlock, char>()
    .AddActionBlock(x => Console.WriteLine($"  DI Received (async): {x}"))
    .Build();

await diAsyncMany.SendAsync("xy");
diAsyncMany.Complete();
await diAsyncMany.Completion;

Console.WriteLine("\n=== Sample Complete ===");
