using Tpl.Dataflow.Builder;

Console.WriteLine("=== DataflowPipelineBuilder Sample ===\n");

// Example 1: Simple Transform Pipeline
Console.WriteLine("Example 1: Simple Transform Pipeline");
Console.WriteLine("-------------------------------------");

await using var transformPipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<int>()
    .AddTransformBlock<string>(x => $"Number: {x}")
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

Console.WriteLine("\n=== Sample Complete ===");
