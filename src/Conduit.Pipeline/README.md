# Conduit.Pipeline

The Pipeline module provides a comprehensive pipeline processing framework with support for behaviors, interceptors, and various enterprise integration patterns.

## Overview

This module implements:
- **Chain of Responsibility Pattern** via `IPipelineBehavior` and `BehaviorChain`
- **Interceptor Pattern** for cross-cutting concerns
- **Pipeline Stages** for sequential processing
- **Fluent API** for pipeline composition
- **Enterprise Integration Patterns** including:
  - Message Translator (`Map`)
  - Pipes and Filters (`Then`)
  - Content-Based Router (`Branch`)
  - Dead Letter Channel (`HandleError`)
  - Message Filter (`Filter`)
  - Splitter (`Parallel`)

## Core Components

### 1. IPipeline<TInput, TOutput>
Main pipeline interface with fluent API methods for composition:
- `ExecuteAsync()` - Execute the pipeline
- `Map()` / `MapAsync()` - Transform output
- `Then()` / `ThenAsync()` - Chain processing
- `Filter()` / `FilterAsync()` - Filter results
- `Branch()` - Conditional routing
- `HandleError()` - Error handling
- `WithRetry()` - Add retry logic
- `WithTimeout()` - Add timeouts
- `WithCache()` - Add caching
- `Parallel()` - Parallel execution

### 2. PipelineContext
Carries state and metadata through pipeline execution:
- Unique context ID
- Properties dictionary for custom data
- Cancellation support
- Timing and performance tracking
- Error information
- Parent-child context relationships

### 3. IPipelineBehavior & BehaviorChain
Implements Chain of Responsibility for cross-cutting concerns:
- Pre/post processing logic
- Conditional execution
- Error handling
- Timing and metrics

### 4. IPipelineStage<TInput, TOutput>
Individual processing stages that can be composed:
- Transform data between types
- Async processing support
- Stage composition via `AndThen()`

### 5. IPipelineInterceptor
Lifecycle hooks for monitoring and modification:
- `BeforeExecutionAsync()` / `AfterExecutionAsync()`
- `BeforeStageAsync()` / `AfterStageAsync()`
- `OnErrorAsync()` - Error interception
- Priority-based ordering

### 6. BehaviorContribution
Rich metadata and placement rules for behaviors:
- Unique ID and naming
- Placement strategies (First, Last, Before, After, Replace)
- Tags for categorization
- Constraints for conditional execution
- Execution phases (PreProcessing, Processing, PostProcessing)

### 7. PipelineConfiguration
Configuration options for pipeline execution:
- Timeout settings
- Retry policies
- Error handling strategies
- Concurrency limits
- Caching configuration
- Metrics and tracing toggles

### 8. PipelineFactory
Factory for creating pre-configured pipelines:
- Sequential pipelines
- Parallel pipelines
- Event-driven pipelines
- Batch processing pipelines
- Stream processing pipelines
- Validation pipelines
- Saga/workflow pipelines

## Usage Examples

### Simple Pipeline
```csharp
var factory = new PipelineFactory();
var pipeline = factory.CreatePipeline<string, int>("ParseNumber")
    .Map(str => int.Parse(str))
    .WithRetry(3, TimeSpan.FromSeconds(1))
    .WithTimeout(TimeSpan.FromSeconds(10));

var result = await pipeline.ExecuteAsync("42");
```

### Pipeline with Behaviors
```csharp
var behavior = BehaviorContribution.Builder()
    .WithId("logging")
    .WithName("Logging Behavior")
    .WithBehavior(async (context, next) =>
    {
        Console.WriteLine($"Processing: {context.Input}");
        var result = await next.ProceedAsync(context);
        Console.WriteLine($"Result: {result}");
        return result;
    })
    .PlaceFirst()
    .Build();

var pipeline = factory.CreatePipelineWithBehaviors<string, string>(
    "MyPipeline", behavior);
```

### Parallel Execution
```csharp
var pipeline = factory.CreateParallelPipeline<int, string>("ParallelProcess");
var items = Enumerable.Range(1, 10);
var results = await pipeline.Parallel(items, i => i).ExecuteAsync(0);
```

### Error Handling
```csharp
var pipeline = factory.CreatePipeline<string, int>("SafeParser")
    .Map(str => int.Parse(str))
    .HandleError(ex => -1); // Return -1 on error

var result = await pipeline.ExecuteAsync("not-a-number"); // Returns -1
```

## Key Features

1. **Composable**: Pipelines can be composed using fluent API
2. **Extensible**: Add custom behaviors, stages, and interceptors
3. **Configurable**: Rich configuration options for different scenarios
4. **Observable**: Full lifecycle visibility through interceptors
5. **Resilient**: Built-in retry, timeout, and error handling
6. **Performant**: Caching, concurrency control, and async support
7. **Testable**: Clear separation of concerns and mockable interfaces

## Thread Safety

All pipeline components are designed to be thread-safe:
- `PipelineContext` uses `ConcurrentDictionary` for properties
- Pipeline instances can be reused across multiple executions
- Cache management is thread-safe
- Concurrency is controlled via semaphores

## Integration

The Pipeline module integrates seamlessly with other Conduit modules:
- Uses `Conduit.Api` interfaces for component contracts
- Can process `IMessage`, `ICommand`, `IEvent`, and `IQuery` types
- Works with `Conduit.Core` for component lifecycle management
- Ready for `Conduit.Messaging` integration for message routing