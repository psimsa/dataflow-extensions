# DataflowPipelineBuilder

[![CI](https://github.com/psimsa/dataflow-extensions/actions/workflows/ci.yml/badge.svg)](https://github.com/psimsa/dataflow-extensions/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-blue)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A fluent builder pattern library for creating `System.Threading.Tasks.Dataflow` pipelines with type-safe chaining, automatic block linking, and built-in completion propagation.

## Features

- **Fluent API** - Chain dataflow blocks naturally with IntelliSense-friendly syntax
- **Type Safety** - Full compile-time type checking between pipeline stages
- **Auto-linking** - Blocks are automatically linked with completion propagation
- **Custom Block Base Classes** - `PropagatorBlock<T,T}`, `PropagatorManyBlock<TIn, TOut>` and `AsyncPropagatorBlock<T,T>`, `AsyncPropagatorManyBlock<TIn, TOut>` for easy custom blocks
- **Dependency Injection** - Optional `IServiceProvider` integration for DI-based block resolution
- **Keyed Services** - Support for .NET 8+ keyed service resolution
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

### Custom Propagator Blocks

Create reusable custom blocks by inheriting from `PropagatorBlock<TIn, TOut>` (sync) or `AsyncPropagatorBlock<TIn, TOut>` (async):

#### Synchronous Custom Block

```csharp
using Tpl.Dataflow.Builder.Abstractions;

public class MultiplierBlock : PropagatorBlock<int, int>
{
    private readonly int _factor;
    
    public MultiplierBlock(int factor) => _factor = factor;
    
    protected override int Transform(int input) => input * _factor;
}

// Usage
var pipeline = new DataflowPipelineBuilder()
    .AddBufferBlock<int>()
    .AddCustomBlock(new MultiplierBlock(10))
    .AddActionBlock(Console.WriteLine)
    .Build();
```

#### Asynchronous Custom Block

```csharp
using Tpl.Dataflow.Builder.Abstractions;

public class HttpFetchBlock : AsyncPropagatorBlock<string, string>
{
    private readonly HttpClient _httpClient;
    
    public HttpFetchBlock(HttpClient httpClient) => _httpClient = httpClient;
    
    protected override async Task<string> TransformAsync(string url)
    {
        return await _httpClient.GetStringAsync(url);
    }
}

// Usage with options
public class ThrottledProcessor : AsyncPropagatorBlock<int, int>
{
    public ThrottledProcessor() 
        : base(new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 4 })
    { }
    
    protected override async Task<int> TransformAsync(int input)
    {
        await Task.Delay(100);
        return input * 2;
    }
}

// Synchronous 1:N custom block
public class SplitterBlock : PropagatorManyBlock<string, char>
{
    protected override IEnumerable<char> Transform(string input) => input.ToCharArray();
}

// Asynchronous 1:N custom block
public class AsyncSplitterBlock : AsyncPropagatorManyBlock<string, char>
{
    protected override async Task<IEnumerable<char>> TransformAsync(string input)
    {
        await Task.Delay(10);
        return input.ToCharArray();
    }
}
```

### Dependency Injection Integration

#### Basic DI Resolution

```csharp
var services = new ServiceCollection()
    .AddSingleton<MyCustomBlock>()
    .AddTransient<AnotherBlock>()
    .BuildServiceProvider();

var pipeline = new DataflowPipelineBuilder(serviceProvider: services)
    .AddBufferBlock<int>()
    .AddCustomBlock<MyCustomBlock, int>()        // Resolved from DI
    .AddCustomBlock<AnotherBlock, string>()      // Resolved from DI
    .AddActionBlock(Console.WriteLine)
    .Build();
```

#### Keyed Services (.NET 8+)

Use keyed services when you have multiple implementations of the same interface:

```csharp
// Register keyed services
var services = new ServiceCollection()
    .AddKeyedSingleton<IMultiplierBlock, DoublerBlock>("double")
    .AddKeyedSingleton<IMultiplierBlock, TriplerBlock>("triple")
    .BuildServiceProvider();

// Use specific implementation by key
var doublePipeline = new DataflowPipelineBuilder(serviceProvider: services)
    .AddBufferBlock<int>()
    .AddKeyedCustomBlock<IMultiplierBlock, int>("double")
    .AddActionBlock(x => Console.WriteLine($"Doubled: {x}"))
    .Build();

var triplePipeline = new DataflowPipelineBuilder(serviceProvider: services)
    .AddBufferBlock<int>()
    .AddKeyedCustomBlock<IMultiplierBlock, int>("triple")
    .AddActionBlock(x => Console.WriteLine($"Tripled: {x}"))
    .Build();

doublePipeline.Post(5);  // Output: "Doubled: 10"
triplePipeline.Post(5);  // Output: "Tripled: 15"
```

## Supported Blocks

| Block Type | Method | Description |
|------------|--------|-------------|
| BufferBlock | `AddBufferBlock<T>()` | Stores messages for later consumption |
| TransformBlock | `AddTransformBlock<TOut>(Func)` | Transforms each input to one output |
| TransformManyBlock | `AddTransformManyBlock<TOut>(Func)` | Transforms each input to multiple outputs |
| BatchBlock | `AddBatchBlock(batchSize)` | Groups inputs into arrays |
| ActionBlock | `AddActionBlock(Action)` | Terminal block that consumes inputs |
| Custom (instance) | `AddCustomBlock(IPropagatorBlock)` | Add a custom propagator block instance |
| Custom (factory) | `AddCustomBlock(Func<IPropagatorBlock>)` | Add a custom block via factory |
| Custom (DI) | `AddCustomBlock<TBlock, TOut>()` | Resolve block from IServiceProvider |
| Custom (Keyed DI) | `AddKeyedCustomBlock<TBlock, TOut>(key)` | Resolve keyed service from IServiceProvider |
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
- **EnsureOrdered**: `false` by default on execution blocks for better parallel performance

## Abstract Base Classes

The `Tpl.Dataflow.Builder.Abstractions` package provides base classes for creating custom blocks:

| Class | Description |
|-------|-------------|
| `PropagatorBlock<TIn, TOut>` | Base for synchronous transforms - override `Transform(TIn)` |
| `PropagatorManyBlock<TIn, TOut>` | Base for synchronous one-to-many transforms - override `Transform(TIn)` |
| `AsyncPropagatorBlock<TIn, TOut>` | Base for async transforms - override `TransformAsync(TIn)` |
| `AsyncPropagatorManyBlock<TIn, TOut>` | Base for async one-to-many transforms - override `TransformAsync(TIn)` |

Both classes:
- Handle all `IPropagatorBlock<TIn, TOut>` interface plumbing
- Accept optional `ExecutionDataflowBlockOptions` in constructor
- Expose `Completion`, `InputCount`, `OutputCount` properties
- Support `Complete()` and `Fault()` methods

## Target Frameworks

- .NET 8.0
- .NET 10.0

## License

MIT License - see [LICENSE](LICENSE) for details.
