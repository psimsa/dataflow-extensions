# DataflowPipelineBuilder

[![CI](https://github.com/psimsa/dataflow-extensions/actions/workflows/ci.yml/badge.svg)](https://github.com/psimsa/dataflow-extensions/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-blue)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A fluent builder pattern library for creating `System.Threading.Tasks.Dataflow` pipelines with type-safe chaining, automatic block linking, and built-in completion propagation.

## Features

- **Fluent API** - Chain dataflow blocks naturally with IntelliSense-friendly syntax
- **Type Safety** - Full compile-time type checking between pipeline stages
- **Auto-linking** - Blocks are automatically linked with completion propagation
- **Channel Integration** - Use System.Threading.Channels as input source or output sink
- **IAsyncEnumerable Support** - Consume pipeline output as async streams
- **IObservable Support** - Integrate with Reactive Extensions
- **Named Blocks** - Optional naming for debugging and diagnostics
- **Cancellation Support** - Built-in cancellation token propagation
- **AOT Compatible** - No reflection - fully compatible with Native AOT compilation

## Installation

```bash
# Main library
dotnet add package Tpl.Dataflow.Builder

# Abstractions only (for consumers/interfaces)
dotnet add package Tpl.Dataflow.Builder.Abstractions
```

## Quick Start

```csharp
using Tpl.Dataflow.Builder;

// Create a simple transform pipeline
await using var pipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<int>()
    .AddTransformBlock<string>(x => $"Number: {x}")
    .Build();

// Post data
for (int i = 1; i <= 5; i++)
    pipeline.Post(i);

// Signal completion
pipeline.Complete();

// Consume results as IAsyncEnumerable
await foreach (var result in pipeline.ToAsyncEnumerable())
{
    Console.WriteLine(result);
}
```

## Pipeline Examples

### Async Transform with Parallelism

```csharp
await using var pipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<string>()
    .AddTransformBlock<string>(
        async url => 
        {
            await Task.Delay(100);
            return $"Processed: {url}";
        },
        options: new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 4 })
    .Build();
```

### Batch Processing

```csharp
await using var pipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<int>()
    .AddBatchBlock(batchSize: 3)
    .AddTransformBlock<string>(batch => 
        $"Batch: [{string.Join(", ", batch)}]")
    .Build();
```

### Terminal Pipeline (ActionBlock)

```csharp
// AddActionBlock returns DataflowPipelineBuilder<TInput>
// which only has Build() - returns IDataflowPipeline<TInput> (base interface)
await using var pipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<int>()
    .AddTransformBlock<string>(x => $"Item #{x}")
    .AddActionBlock(item => Console.WriteLine($"Processing: {item}"))
    .Build();

await pipeline.Completion;
```

### TransformMany (1:N Expansion)

```csharp
await using var pipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<string>()
    .AddTransformManyBlock<char>(s => s.ToCharArray())
    .Build();
```

### Named Blocks for Debugging

```csharp
await using var pipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<int>("InputBuffer")
    .AddTransformBlock<int>(x => x * 2, "Doubler")
    .AddTransformBlock<string>(x => x.ToString(), "Stringifier")
    .Build();
```

### Channel Integration

#### Channel as Input Source

```csharp
var inputChannel = Channel.CreateUnbounded<string>();

await using var pipeline = new DataflowPipelineBuilder()
    .FromChannelSource(inputChannel)
    .AddTransformBlock(int.Parse)
    .AddActionBlock(Console.WriteLine)
    .Build();

// Write to channel from producer
await inputChannel.Writer.WriteAsync("42");
inputChannel.Writer.Complete();

await pipeline.Completion;
```

#### Channel as Output Sink

```csharp
var pipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<int>()
    .AddTransformBlock(x => x * 2)
    .BuildAsChannel();  // Returns IDataflowChannelPipeline

pipeline.Post(21);
pipeline.Complete();

// Read from output channel
await foreach (var item in pipeline.Output.ReadAllAsync())
{
    Console.WriteLine(item); // 42
}
```

#### Full Channel-to-Channel Pipeline

```csharp
var inputChannel = Channel.CreateUnbounded<int>();

var pipeline = new DataflowPipelineBuilder()
    .FromChannelSource(inputChannel)
    .AddTransformBlock(x => x * 2)
    .AddTransformBlock(x => $"Result: {x}")
    .BuildAsChannel(new BoundedChannelOptions(100)); // Bounded output

// Producer
await inputChannel.Writer.WriteAsync(21);
inputChannel.Writer.Complete();

// Consumer
await foreach (var result in pipeline.Output.ReadAllAsync())
{
    Console.WriteLine(result); // "Result: 42"
}
```

## Supported Blocks

| Block Type | Method | Description |
|------------|--------|-------------|
| BufferBlock | `AddBufferBlock<T>()` | Stores messages for later consumption |
| TransformBlock | `AddTransformBlock<TOut>(Func)` | Transforms each input to one output |
| TransformManyBlock | `AddTransformManyBlock<TOut>(Func)` | Transforms each input to multiple outputs |
| BatchBlock | `AddBatchBlock(batchSize)` | Groups inputs into arrays |
| ActionBlock | `AddActionBlock(Action)` | Terminal block that consumes inputs |
| Custom | `AddCustomBlock(IPropagatorBlock)` | Add any custom propagator block |
| Channel Source | `FromChannelSource(Channel/ChannelReader)` | Start pipeline from a channel |

## Build Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Build()` | `IDataflowPipeline<TIn, TOut>` | Standard pipeline with output |
| `Build()` (after ActionBlock) | `IDataflowPipeline<TIn>` | Terminal pipeline (no output) |
| `BuildAsChannel()` | `IDataflowChannelPipeline<TIn, TOut>` | Pipeline with Channel output |

## Output Consumption Methods

```csharp
// IAsyncEnumerable (preferred)
await foreach (var item in pipeline.ToAsyncEnumerable())
    Console.WriteLine(item);

// IObservable (Rx integration)
var subscription = pipeline.AsObservable()
    .Subscribe(item => Console.WriteLine(item));

// Single receive
var item = await pipeline.ReceiveAsync();

// Try receive (non-blocking)
if (pipeline.TryReceive(out var item))
    Console.WriteLine(item);
```

## Default Options

- **Link Options**: `PropagateCompletion = true` (automatic completion propagation)
- **Cancellation**: Default token can be set in builder constructor
- **Block Names**: Auto-generated as `{BlockType}_{Index}` if not specified

## Target Frameworks

- .NET 8.0
- .NET 10.0

## License

MIT License - see [LICENSE](LICENSE) for details.
