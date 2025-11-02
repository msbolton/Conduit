# Conduit.Saga

Saga orchestration for distributed transactions and long-running business processes in the Conduit framework. Inspired by NServiceBus Saga pattern.

## Features

- **Long-Running Workflows** - Coordinate multi-step processes across multiple services
- **Distributed Transactions** - Manage consistency in distributed systems without 2PC
- **Automatic State Management** - Saga state persisted and restored automatically
- **Message Correlation** - Automatic correlation of related messages to saga instances
- **Compensation Logic** - Built-in support for compensating actions on failure
- **Timeout Support** - Schedule timeout messages for saga coordination
- **Reflection-Based Routing** - Automatic message handler discovery

## What is a Saga?

A **Saga** is a pattern for managing long-running business transactions that span multiple services. Instead of using distributed transactions (2PC), sagas use a sequence of local transactions with compensating actions to maintain consistency.

### Saga vs Traditional Transactions

**Traditional Transaction (2PC):**
```
BEGIN TRANSACTION
  Update Order Status
  Reserve Inventory
  Charge Payment
  Send Confirmation
COMMIT or ROLLBACK
```

**Saga Pattern:**
```
1. Create Order → If fails, nothing to compensate
2. Reserve Inventory → If fails, cancel order
3. Charge Payment → If fails, release inventory, cancel order
4. Send Confirmation → If fails, refund payment, release inventory, cancel order
```

### When to Use Sagas

✅ **Use sagas when:**
- Process spans multiple services/boundaries
- Long-running workflows (minutes, hours, days)
- Need compensation logic for failures
- Cannot use distributed transactions

❌ **Don't use sagas for:**
- Simple request/response operations
- Single-service transactions
- Real-time, sub-second operations
- Workflows that can use local transactions

## Installation

```bash
dotnet add package Conduit.Saga
```

## Quick Start

### 1. Define Saga Data

```csharp
public class OrderSagaData : SagaData
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public bool InventoryReserved { get; set; }
    public bool PaymentCharged { get; set; }
}
```

### 2. Create a Saga

```csharp
[SagaStartedBy(typeof(CreateOrderCommand))]
[SagaHandles(typeof(InventoryReservedEvent), typeof(PaymentChargedEvent),
              typeof(InventoryReservationFailedEvent), typeof(PaymentFailedEvent))]
public class OrderSaga : Saga
{
    public new OrderSagaData Entity
    {
        get => (OrderSagaData)base.Entity;
        set => base.Entity = value;
    }

    protected override void ConfigureHowToFindSaga(IConfigureHowToFindSagaWithMessage mapper)
    {
        mapper.CorrelateByCorrelationId<CreateOrderCommand>(cmd => cmd.OrderId);
        mapper.CorrelateByCorrelationId<InventoryReservedEvent>(evt => evt.OrderId);
        mapper.CorrelateByCorrelationId<PaymentChargedEvent>(evt => evt.OrderId);
    }

    // Saga starts here
    public async Task HandleAsync(
        CreateOrderCommand message,
        ISagaMessageHandlerContext context,
        CancellationToken cancellationToken)
    {
        Entity.OrderId = message.OrderId;
        Entity.CustomerId = message.CustomerId;
        Entity.TotalAmount = message.TotalAmount;
        Entity.State = "RESERVING_INVENTORY";

        // Send command to reserve inventory
        await context.SendAsync(new ReserveInventoryCommand
        {
            OrderId = message.OrderId,
            Items = message.Items
        }, cancellationToken);
    }

    // Handle successful inventory reservation
    public async Task HandleAsync(
        InventoryReservedEvent message,
        ISagaMessageHandlerContext context,
        CancellationToken cancellationToken)
    {
        Entity.InventoryReserved = true;
        Entity.State = "CHARGING_PAYMENT";

        // Charge payment
        await context.SendAsync(new ChargePaymentCommand
        {
            OrderId = Entity.OrderId,
            CustomerId = Entity.CustomerId,
            Amount = Entity.TotalAmount
        }, cancellationToken);
    }

    // Handle successful payment
    public async Task HandleAsync(
        PaymentChargedEvent message,
        ISagaMessageHandlerContext context,
        CancellationToken cancellationToken)
    {
        Entity.PaymentCharged = true;
        Entity.State = "COMPLETED";

        // Send confirmation
        await context.PublishAsync(new OrderCompletedEvent
        {
            OrderId = Entity.OrderId,
            CustomerId = Entity.CustomerId
        }, cancellationToken);

        MarkAsComplete();
    }

    // Handle inventory reservation failure
    public async Task HandleAsync(
        InventoryReservationFailedEvent message,
        ISagaMessageHandlerContext context,
        CancellationToken cancellationToken)
    {
        Entity.State = "FAILED";

        // Publish failure event
        await context.PublishAsync(new OrderFailedEvent
        {
            OrderId = Entity.OrderId,
            Reason = "Inventory not available"
        }, cancellationToken);

        MarkAsComplete();
    }

    // Handle payment failure
    public async Task HandleAsync(
        PaymentFailedEvent message,
        ISagaMessageHandlerContext context,
        CancellationToken cancellationToken)
    {
        Entity.State = "COMPENSATING";

        // Compensate: Release reserved inventory
        if (Entity.InventoryReserved)
        {
            await context.SendAsync(new ReleaseInventoryCommand
            {
                OrderId = Entity.OrderId
            }, cancellationToken);
        }

        // Publish failure event
        await context.PublishAsync(new OrderFailedEvent
        {
            OrderId = Entity.OrderId,
            Reason = "Payment failed"
        }, cancellationToken);

        MarkAsComplete();
    }
}
```

### 3. Register Saga

```csharp
services.AddConduitSaga();
services.AddSaga<OrderSaga>();
```

### 4. Handle Messages

```csharp
public class OrderService
{
    private readonly ISagaOrchestrator _orchestrator;

    public OrderService(ISagaOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task ProcessOrderAsync(CreateOrderCommand command)
    {
        var context = new SagaMessageHandlerContext(command.OrderId);

        await _orchestrator.HandleMessageAsync(
            typeof(OrderSaga),
            command,
            context);
    }
}
```

## Core Concepts

### Saga Lifecycle

```
┌─────────────┐
│   START     │ ← Message marked with [SagaStartedBy]
└──────┬──────┘
       │
       ▼
┌─────────────┐
│  RUNNING    │ ← Handle messages
│  (State     │ ← Send commands
│   persisted)│ ← Publish events
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ COMPLETED   │ ← MarkAsComplete() called
│ (Deleted)   │
└─────────────┘
```

### Message Correlation

Sagas use **correlation** to route messages to the correct saga instance:

```csharp
protected override void ConfigureHowToFindSaga(IConfigureHowToFindSagaWithMessage mapper)
{
    // Correlate by correlation ID
    mapper.CorrelateByCorrelationId<OrderCreated>(evt => evt.OrderId);

    // Correlate by message property to saga property
    mapper.CorrelateMessage<PaymentProcessed, OrderSagaData, string>(
        msg => msg.TransactionId,
        saga => saga.PaymentTransactionId);
}
```

### Saga State

Saga state is automatically persisted between messages:

```csharp
public class OrderSagaData : SagaData
{
    // Business data
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    // Tracking flags
    public bool InventoryReserved { get; set; }
    public bool PaymentCharged { get; set; }
    public bool ShipmentScheduled { get; set; }

    // Compensation data
    public string? ReservationId { get; set; }
    public string? PaymentTransactionId { get; set; }
}
```

## Advanced Features

### Timeouts

Request timeout messages to handle long-running operations:

```csharp
public async Task HandleAsync(
    OrderCreated message,
    ISagaMessageHandlerContext context,
    CancellationToken cancellationToken)
{
    Entity.OrderId = message.OrderId;

    // Request timeout in 5 minutes
    await RequestTimeoutAsync(
        context,
        TimeSpan.FromMinutes(5),
        new OrderTimeout { OrderId = message.OrderId },
        cancellationToken);
}

public async Task HandleAsync(
    OrderTimeout message,
    ISagaMessageHandlerContext context,
    CancellationToken cancellationToken)
{
    // Handle timeout - cancel order if not completed
    if (Entity.State != "COMPLETED")
    {
        await CompensateAsync(context, cancellationToken);
        MarkAsComplete();
    }
}
```

### Reply to Originator

Send a reply back to the message originator:

```csharp
public async Task HandleAsync(
    ProcessOrderCommand message,
    ISagaMessageHandlerContext context,
    CancellationToken cancellationToken)
{
    // Process order...

    // Reply to originator
    await ReplyToOriginatorAsync(
        context,
        new OrderProcessedReply
        {
            OrderId = Entity.OrderId,
            Success = true
        },
        cancellationToken);
}
```

### Compensation Pattern

Implement compensation logic for failed operations:

```csharp
private async Task CompensateAsync(
    ISagaMessageHandlerContext context,
    CancellationToken cancellationToken)
{
    // Undo operations in reverse order
    if (Entity.PaymentCharged)
    {
        await context.SendAsync(new RefundPaymentCommand
        {
            TransactionId = Entity.PaymentTransactionId
        }, cancellationToken);
    }

    if (Entity.InventoryReserved)
    {
        await context.SendAsync(new ReleaseInventoryCommand
        {
            ReservationId = Entity.ReservationId
        }, cancellationToken);
    }

    Entity.State = "COMPENSATED";
}
```

## Saga Patterns

### Orchestration Pattern

Saga **controls** the workflow (what we've implemented):

```csharp
OrderSaga:
  1. Create Order
  2. Reserve Inventory → Success/Failure
  3. Charge Payment → Success/Failure
  4. Complete or Compensate
```

**Pros:** Centralized control, easier to understand
**Cons:** Saga becomes coupling point

### Choreography Pattern

Services **react** to events without central coordinator:

```
OrderService → OrderCreatedEvent
               ↓
InventoryService → InventoryReservedEvent
                   ↓
PaymentService → PaymentChargedEvent
                 ↓
ShippingService → ShipmentScheduledEvent
```

**Pros:** Loose coupling, services autonomous
**Cons:** Harder to understand flow, distributed logic

## Saga Persistence

### In-Memory (Development/Testing)

```csharp
services.AddConduitSaga(config =>
{
    config.UseInMemoryPersister = true;
});
```

### Custom Persister

```csharp
public class SqlSagaPersister : ISagaPersister
{
    private readonly IDbConnection _connection;

    public async Task SaveAsync(IContainSagaData sagaData, CancellationToken cancellationToken)
    {
        // Save to SQL database
        await _connection.ExecuteAsync(
            "INSERT INTO Sagas (Id, CorrelationId, State, Data) VALUES (@Id, @CorrelationId, @State, @Data)",
            sagaData);
    }

    public async Task<TSagaData?> FindAsync<TSagaData>(
        string sagaType,
        string correlationId,
        CancellationToken cancellationToken)
        where TSagaData : class, IContainSagaData
    {
        // Find in SQL database
        return await _connection.QueryFirstOrDefaultAsync<TSagaData>(
            "SELECT * FROM Sagas WHERE SagaType = @SagaType AND CorrelationId = @CorrelationId",
            new { SagaType = sagaType, CorrelationId = correlationId });
    }

    public async Task RemoveAsync(IContainSagaData sagaData, CancellationToken cancellationToken)
    {
        // Delete from SQL database
        await _connection.ExecuteAsync(
            "DELETE FROM Sagas WHERE Id = @Id",
            new { sagaData.Id });
    }
}

// Register
services.AddSagaPersister<SqlSagaPersister>();
```

## Real-World Examples

### E-Commerce Order Processing

```csharp
[SagaStartedBy(typeof(PlaceOrderCommand))]
public class ECommerceOrderSaga : Saga
{
    // Saga data
    public new ECommerceOrderData Entity
    {
        get => (ECommerceOrderData)base.Entity;
        set => base.Entity = value;
    }

    // Workflow:
    // 1. Validate order
    // 2. Reserve inventory
    // 3. Authorize payment
    // 4. Capture payment
    // 5. Schedule shipment
    // 6. Send confirmation

    public async Task HandleAsync(PlaceOrderCommand message, ISagaMessageHandlerContext context, CancellationToken cancellationToken)
    {
        Entity.State = "VALIDATING";
        await context.SendAsync(new ValidateOrderCommand { OrderId = message.OrderId }, cancellationToken);
    }

    // Handle each step with success/failure branches
    // Implement compensation for each step
}
```

### Travel Booking Saga

```csharp
[SagaStartedBy(typeof(BookTripCommand))]
public class TravelBookingSaga : Saga
{
    // Workflow:
    // 1. Reserve flight
    // 2. Reserve hotel
    // 3. Reserve car rental
    // 4. Charge payment
    // 5. Confirm all bookings or cancel

    // If any step fails, compensate by canceling previous reservations
}
```

### Account Transfer Saga

```csharp
[SagaStartedBy(typeof(TransferMoneyCommand))]
public class MoneyTransferSaga : Saga
{
    // Workflow:
    // 1. Debit source account
    // 2. Credit destination account
    // 3. Log transaction
    // 4. Send notifications

    // If credit fails, compensate by crediting source account
}
```

## Best Practices

### 1. **Idempotent Handlers**

Saga handlers should be idempotent (safe to execute multiple times):

```csharp
public async Task HandleAsync(PaymentChargedEvent message, ...)
{
    // Check if already processed
    if (Entity.PaymentCharged)
    {
        return; // Already processed, skip
    }

    Entity.PaymentCharged = true;
    // Continue processing...
}
```

### 2. **Explicit State Tracking**

Use explicit state fields to track saga progress:

```csharp
public class OrderSagaData : SagaData
{
    public OrderState CurrentState { get; set; }

    public bool InventoryReserved { get; set; }
    public bool PaymentProcessed { get; set; }
    public bool ShipmentScheduled { get; set; }
}

public enum OrderState
{
    Created,
    ReservingInventory,
    ChargingPayment,
    SchedulingShipment,
    Completed,
    Failed,
    Compensating
}
```

### 3. **Compensation Data**

Store data needed for compensation:

```csharp
public class OrderSagaData : SagaData
{
    // Business data
    public string OrderId { get; set; } = string.Empty;

    // Compensation data
    public string? InventoryReservationId { get; set; }
    public string? PaymentTransactionId { get; set; }
    public DateTime? ReservationTimestamp { get; set; }
}
```

### 4. **Timeout Handling**

Always handle timeouts for long-running operations:

```csharp
// Request timeout
await RequestTimeoutAsync(context, TimeSpan.FromHours(24),
    new OrderExpirationTimeout { OrderId = Entity.OrderId });

// Handle timeout
public async Task HandleAsync(OrderExpirationTimeout message, ...)
{
    if (Entity.State != "COMPLETED")
    {
        await CompensateAsync(context);
        MarkAsComplete();
    }
}
```

### 5. **Correlation IDs**

Use stable correlation IDs (business identifiers, not random GUIDs):

```csharp
// Good: Use business identifier
Entity.CorrelationId = message.OrderId;

// Bad: Use random GUID (can't correlate with business events)
Entity.CorrelationId = Guid.NewGuid().ToString();
```

## Troubleshooting

### Saga Not Starting

**Problem:** Message received but saga not created

**Solution:**
- Ensure message type is in `[SagaStartedBy]` attribute
- Check saga is registered: `services.AddSaga<MySaga>()`
- Verify correlation ID is set correctly

### Saga Instance Not Found

**Problem:** Message correlation fails, creates duplicate saga

**Solution:**
- Verify `ConfigureHowToFindSaga()` maps message correctly
- Ensure correlation ID is stable across messages
- Check saga persister is saving/loading correctly

### Saga State Not Persisted

**Problem:** Saga state lost between messages

**Solution:**
- Verify persister is registered correctly
- Check `SaveSagaAsync()` is called (should be automatic)
- Ensure saga data implements `IContainSagaData`

### Deadlocks or Timeouts

**Problem:** Saga processing hangs

**Solution:**
- Use asynchronous handlers (`async Task HandleAsync`)
- Avoid blocking calls (`Wait()`, `Result`)
- Set appropriate timeouts
- Check for circular message dependencies

## Performance Considerations

### Saga Granularity

**Too Fine-Grained:**
```csharp
// ❌ Each step is a separate saga
CreateOrderSaga → ReserveInventorySaga → ChargePaymentSaga
```

**Too Coarse-Grained:**
```csharp
// ❌ One saga for entire customer journey
CustomerJourneySaga: Signup → Browse → Order → Ship → Review
```

**Just Right:**
```csharp
// ✅ One saga per business transaction
OrderProcessingSaga: Create → Reserve → Pay → Ship → Complete
```

### Saga Cleanup

Always call `MarkAsComplete()` when saga is done:

```csharp
public async Task HandleAsync(OrderCompleted message, ...)
{
    // Final actions
    await context.PublishAsync(new OrderConfirmationEmail { ... });

    // Mark complete to delete saga data
    MarkAsComplete();
}
```

### Saga Queries

For querying saga state, use read models instead of querying saga data directly:

```csharp
// ❌ Don't query saga persister
var saga = await sagaPersister.FindAsync(...);
return saga.State;

// ✅ Use read model / projection
var orderStatus = await _readModel.GetOrderStatusAsync(orderId);
return orderStatus;
```

## Dependencies

- `Conduit.Api` - Core interfaces
- `Conduit.Common` - Shared utilities
- `Conduit.Core` - Framework core
- `Conduit.Messaging` - Message bus and CQRS
- `Conduit.Persistence` - Data persistence interfaces
- `Microsoft.Extensions.DependencyInjection` - DI support
- `Microsoft.Extensions.Logging` - Logging support

## Additional Resources

- [Saga Pattern (Microservices.io)](https://microservices.io/patterns/data/saga.html)
- [NServiceBus Sagas](https://docs.particular.net/nservicebus/sagas/)
- [Distributed Sagas: A Protocol for Coordinating Microservices](https://www.youtube.com/watch?v=xDuwrtwYHu8)

## License

Part of the Conduit Framework - see LICENSE file for details.
