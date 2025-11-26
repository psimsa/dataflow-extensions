# DataflowPipelineBuilder

[![CI](https://github.com/psimsa/dataflow-extensions/actions/workflows/ci.yml/badge.svg)](https://github.com/psimsa/dataflow-extensions/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-blue)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A fluent builder pattern library for creating `System.Threading.Tasks.Dataflow` pipelines with type-safe chaining, automatic block linking, and built-in completion propagation.

## Features

- **Fluent API** - Chain dataflow blocks naturally with IntelliSense-friendly syntax
- **Type Safety** - Full compile-time type checking between pipeline stages
- **Auto-linking** - Blocks are automatically linked with completion propagation
- **IAsyncEnumerable Support** - Consume pipeline output as async streams
- **IObservable Support** - Integrate with Reactive Extensions
- **Named Blocks** - Optional naming for debugging and diagnostics
- **Cancellation Support** - Built-in cancellation token propagation

## Installation

```bash
# Main library
dotnet add package DataflowPipelineBuilder

# Abstractions only (for consumers/interfaces)
dotnet add package DataflowPipelineBuilder.Abstractions
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

## Supported Blocks

| Block Type | Method | Description |
|------------|--------|-------------|
| BufferBlock | `AddBufferBlock<T>()` | Stores messages for later consumption |
| TransformBlock | `AddTransformBlock<TOut>(Func)` | Transforms each input to one output |
| TransformManyBlock | `AddTransformManyBlock<TOut>(Func)` | Transforms each input to multiple outputs |
| BatchBlock | `AddBatchBlock(batchSize)` | Groups inputs into arrays |
| ActionBlock | `AddActionBlock(Action)` | Terminal block that consumes inputs |
| Custom | `AddCustomBlock(IPropagatorBlock)` | Add any custom propagator block |

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