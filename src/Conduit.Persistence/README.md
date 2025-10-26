# Conduit.Persistence

A comprehensive persistence layer for the Conduit framework with support for multiple database technologies, repository pattern, Unit of Work, and caching.

## Features

- **Repository Pattern**: Generic repository with CRUD operations
- **Unit of Work**: Transaction management and coordinated saves
- **Multiple Databases**: Entity Framework Core (PostgreSQL, SQL Server, etc.), MongoDB
- **Caching**: Redis-based caching with decorator pattern
- **Soft Delete**: Automatic soft delete support
- **Auditing**: Automatic creation and modification tracking
- **Paging**: Built-in pagination support
- **LINQ Support**: Queryable repositories for complex queries

## Quick Start

### Entity Framework Core with PostgreSQL

```csharp
using Conduit.Persistence;
using Conduit.Persistence.EntityFramework;
using Conduit.Persistence.PostgreSQL;
using Microsoft.EntityFrameworkCore;

// Define your entity
public class Product : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

// Create your DbContext
public class AppDbContext : ConduitDbContext
{
    public DbSet<Product> Products { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
}

// Configure and use
var config = new PostgreSqlConfiguration
{
    Host = "localhost",
    Database = "myapp",
    Username = "postgres",
    Password = "password"
};

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(config.BuildConnectionString())
    .Options;

using var context = new AppDbContext(options);
using var unitOfWork = new EfCoreUnitOfWork(context);

// Get repository
var productRepo = unitOfWork.Repository<Product, Guid>();

// Add product
var product = new Product
{
    Name = "Widget",
    Price = 19.99m,
    Stock = 100
};

await productRepo.AddAsync(product);
await unitOfWork.SaveChangesAsync();

// Query products
var allProducts = await productRepo.GetAllAsync();
var expensiveProducts = await productRepo.FindAsync(p => p.Price > 50);
```

### MongoDB

```csharp
using Conduit.Persistence.MongoDB;

// Define your entity
public class Order : Entity<string>
{
    public string CustomerId { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal Total { get; set; }
    public DateTime OrderDate { get; set; }
}

// Configure MongoDB
var config = new MongoDbConfiguration
{
    ConnectionString = "mongodb://localhost:27017",
    DatabaseName = "orders_db"
};

var database = config.GetDatabase();
var orderRepo = new MongoRepository<Order, string>(database, "orders");

// Add order
var order = new Order
{
    Id = Guid.NewGuid().ToString(),
    CustomerId = "CUST123",
    OrderDate = DateTime.UtcNow,
    Total = 99.99m
};

await orderRepo.AddAsync(order);

// Query orders
var recentOrders = await orderRepo.FindAsync(o => o.OrderDate > DateTime.UtcNow.AddDays(-7));
```

### Redis Caching

```csharp
using Conduit.Persistence.Caching;
using StackExchange.Redis;

// Configure Redis
var redisConfig = new RedisConfiguration
{
    Host = "localhost",
    Port = 6379,
    Database = 0
};

var redis = redisConfig.CreateConnection();
var cacheProvider = new RedisCacheProvider(redis);

// Wrap repository with caching
var cachedRepo = new CachedRepository<Product, Guid>(
    productRepo,
    cacheProvider,
    new CacheOptions
    {
        Expiration = TimeSpan.FromMinutes(10),
        CacheListQueries = true
    });

// Cached operations
var product = await cachedRepo.GetByIdAsync(productId); // First call hits DB
var sameProd = await cachedRepo.GetByIdAsync(productId); // Second call from cache
```

## Core Concepts

### Entities

All entities must implement `IEntity<TId>`. The framework provides base classes:

```csharp
// Simple entity with Guid ID
public class MyEntity : Entity
{
    public string Name { get; set; } = string.Empty;
}

// Entity with custom ID type
public class Customer : Entity<int>
{
    public string Email { get; set; } = string.Empty;
}

// Entity with auditing
public class Product : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    // Automatically tracks CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
}

// Entity with soft delete
public class Document : AuditableEntity, ISoftDeletable
{
    public string Title { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
```

### Repository Pattern

The repository provides a consistent API for data access:

```csharp
// Query operations
var entity = await repo.GetByIdAsync(id);
var all = await repo.GetAllAsync();
var filtered = await repo.FindAsync(e => e.Status == "Active");
var first = await repo.FirstOrDefaultAsync(e => e.Code == "ABC");
var exists = await repo.AnyAsync(e => e.Email == email);
var count = await repo.CountAsync(e => e.IsActive);

// Write operations
await repo.AddAsync(entity);
await repo.AddRangeAsync(entities);
await repo.UpdateAsync(entity);
await repo.UpdateRangeAsync(entities);
await repo.DeleteAsync(id);
await repo.DeleteAsync(entity);
await repo.DeleteRangeAsync(entities);
var deleted = await repo.DeleteWhereAsync(e => e.Expired);
```

### Unit of Work

Coordinates multiple repository operations in a single transaction:

```csharp
using var unitOfWork = new EfCoreUnitOfWork(dbContext);

var productRepo = unitOfWork.Repository<Product, Guid>();
var orderRepo = unitOfWork.Repository<Order, Guid>();

// Begin transaction
using var transaction = await unitOfWork.BeginTransactionAsync();

try
{
    // Multiple operations
    await productRepo.UpdateAsync(product);
    await orderRepo.AddAsync(order);

    // Save all changes
    await unitOfWork.SaveChangesAsync();

    // Commit transaction
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### Transaction Scope

Simplified transaction management with automatic rollback:

```csharp
using var scope = new TransactionScope(unitOfWork, transaction);

// Perform operations
await productRepo.UpdateAsync(product);
await orderRepo.AddAsync(order);
await unitOfWork.SaveChangesAsync();

// Mark as complete (will commit on dispose)
scope.Complete();

// Automatic rollback if Complete() not called
```

## Advanced Features

### Pagination

```csharp
// Basic paging
var page = await pagedRepo.GetPageAsync(
    pageNumber: 1,
    pageSize: 20
);

Console.WriteLine($"Total: {page.TotalCount}");
Console.WriteLine($"Pages: {page.TotalPages}");
Console.WriteLine($"Has Next: {page.HasNextPage}");

foreach (var item in page.Items)
{
    Console.WriteLine(item.Name);
}

// Paging with filtering and ordering
var filteredPage = await pagedRepo.GetPageAsync(
    pageNumber: 1,
    pageSize: 10,
    orderBy: p => p.CreatedAt,
    ascending: false,
    predicate: p => p.Price > 100
);
```

### Queryable Repository (EF Core)

```csharp
var queryableRepo = (IQueryableRepository<Product, Guid>)productRepo;

// Complex LINQ queries
var query = queryableRepo.Query()
    .Where(p => p.Stock > 0)
    .OrderBy(p => p.Price)
    .Include(p => p.Category)
    .ThenInclude(c => c.Department);

var products = await query.ToListAsync();

// With predicate
var activeProducts = queryableRepo.Query(p => p.IsActive)
    .OrderByDescending(p => p.CreatedAt)
    .Take(10);
```

### Soft Delete

Entities implementing `ISoftDeletable` are automatically soft-deleted:

```csharp
public class Document : Entity, ISoftDeletable
{
    public string Title { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

// Soft delete (sets IsDeleted = true)
await repo.DeleteAsync(document);

// Soft-deleted entities are automatically filtered from queries
var active = await repo.GetAllAsync(); // Only non-deleted documents
```

### Auditing

Entities implementing `IAuditableEntity` have automatic timestamp tracking:

```csharp
// On creation
entity.CreatedAt = DateTime.UtcNow;
entity.CreatedBy = currentUserId;

// On update
entity.UpdatedAt = DateTime.UtcNow;
entity.UpdatedBy = currentUserId;

// Set current user
if (dbContext is ConduitDbContext conduitContext)
{
    conduitContext.SetCurrentUser(userId);
}
```

### Caching Strategies

```csharp
var cacheOptions = new CacheOptions
{
    EnableCaching = true,
    KeyPrefix = "myapp",
    Expiration = TimeSpan.FromMinutes(10),
    CacheListQueries = true, // Cache GetAll()
    ListExpiration = TimeSpan.FromMinutes(1),
    CacheOnWrite = true // Cache after Add/Update
};

var cachedRepo = new CachedRepository<Product, Guid>(
    innerRepository,
    cacheProvider,
    cacheOptions
);

// Cached operations
var product = await cachedRepo.GetByIdAsync(id); // DB + Cache
var all = await cachedRepo.GetAllAsync(); // DB + Cache (if enabled)
await cachedRepo.UpdateAsync(product); // Invalidates cache
await cachedRepo.DeleteAsync(id); // Invalidates cache
```

## Database Configuration

### PostgreSQL

```csharp
var config = new PostgreSqlConfiguration
{
    Host = "localhost",
    Port = 5432,
    Database = "myapp",
    Username = "postgres",
    Password = "password",
    UseSsl = true,
    MinPoolSize = 5,
    MaxPoolSize = 100,
    ConnectionTimeout = 30,
    EnableDetailedErrors = true
};

config.Validate();

var connectionString = config.BuildConnectionString();

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connectionString)
    .EnableSensitiveDataLogging(config.EnableSensitiveDataLogging)
    .EnableDetailedErrors(config.EnableDetailedErrors)
    .Options;
```

### MongoDB

```csharp
var config = new MongoDbConfiguration
{
    ConnectionString = "mongodb://localhost:27017",
    DatabaseName = "myapp",
    // Or configure separately:
    Server = "localhost",
    Port = 27017,
    Username = "admin",
    Password = "password",
    AuthenticationDatabase = "admin",
    UseSsl = false,
    ReplicaSetName = "rs0",
    MaxConnectionPoolSize = 100,
    MinConnectionPoolSize = 10,
    ConnectionTimeout = 30000,
    SocketTimeout = 30000
};

config.Validate();

var database = config.GetDatabase();
var client = config.CreateClient();
```

### Redis

```csharp
var config = new RedisConfiguration
{
    Host = "localhost",
    Port = 6379,
    Password = "password",
    Database = 0,
    UseSsl = false,
    ConnectTimeout = 5000,
    SyncTimeout = 5000,
    AbortOnConnectFail = false,
    AllowAdmin = false,
    KeyPrefix = "myapp",
    DefaultExpiration = TimeSpan.FromHours(1)
};

config.Validate();

var redis = config.CreateConnection();
var cacheProvider = new RedisCacheProvider(redis, config.Database);

// Direct cache operations
await cacheProvider.SetAsync("key", value, TimeSpan.FromMinutes(10));
var cached = await cacheProvider.GetAsync<MyType>("key");
var exists = await cacheProvider.ExistsAsync("key");
await cacheProvider.RemoveAsync("key");
await cacheProvider.RemoveByPatternAsync("prefix:*");
await cacheProvider.ClearAsync();

// Get or set pattern
var data = await cacheProvider.GetOrSetAsync(
    "expensive_data",
    async ct => await FetchExpensiveDataAsync(ct),
    TimeSpan.FromMinutes(30)
);
```

## Examples

### Complete CRUD Application

```csharp
using Conduit.Persistence;
using Conduit.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore;

public class Product : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; } = true;

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

public class AppDbContext : ConduitDbContext
{
    public DbSet<Product> Products { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.HasIndex(e => e.Name);
        });
    }
}

// Application service
public class ProductService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Product, Guid> _productRepo;

    public ProductService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _productRepo = unitOfWork.Repository<Product, Guid>();
    }

    public async Task<Product> CreateProductAsync(
        string name,
        string description,
        decimal price,
        int stock)
    {
        var product = new Product
        {
            Name = name,
            Description = description,
            Price = price,
            Stock = stock
        };

        await _productRepo.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        return product;
    }

    public async Task<Product?> GetProductAsync(Guid id)
    {
        return await _productRepo.GetByIdAsync(id);
    }

    public async Task<PagedResult<Product>> SearchProductsAsync(
        string? searchTerm,
        decimal? minPrice,
        decimal? maxPrice,
        int pageNumber,
        int pageSize)
    {
        var pagedRepo = (IPagedRepository<Product, Guid>)_productRepo;

        Expression<Func<Product, bool>>? predicate = null;

        if (!string.IsNullOrEmpty(searchTerm))
        {
            predicate = p => p.Name.Contains(searchTerm) ||
                           p.Description.Contains(searchTerm);
        }

        if (minPrice.HasValue && predicate != null)
        {
            var min = minPrice.Value;
            predicate = predicate.And(p => p.Price >= min);
        }

        if (maxPrice.HasValue && predicate != null)
        {
            var max = maxPrice.Value;
            predicate = predicate.And(p => p.Price <= max);
        }

        return await pagedRepo.GetPageAsync(
            pageNumber,
            pageSize,
            orderBy: p => p.Name,
            ascending: true,
            predicate: predicate
        );
    }

    public async Task UpdateStockAsync(Guid productId, int quantity)
    {
        var product = await _productRepo.GetByIdAsync(productId);
        if (product == null)
            throw new KeyNotFoundException($"Product {productId} not found");

        product.Stock += quantity;
        await _productRepo.UpdateAsync(product);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteProductAsync(Guid id)
    {
        await _productRepo.DeleteAsync(id); // Soft delete
        await _unitOfWork.SaveChangesAsync();
    }
}
```

### Multi-Database Application

```csharp
// SQL for transactional data
public class Order : AuditableEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

// MongoDB for flexible documents
public class ProductCatalog : Entity<string>
{
    public string SKU { get; set; } = string.Empty;
    public Dictionary<string, object> Attributes { get; set; } = new();
}

// Redis for caching
var sqlRepo = unitOfWork.Repository<Order, Guid>();
var mongoRepo = new MongoRepository<ProductCatalog, string>(mongoDb, "catalog");
var cachedSqlRepo = new CachedRepository<Order, Guid>(sqlRepo, cacheProvider);

// Use the right database for the job
var order = await cachedSqlRepo.GetByIdAsync(orderId); // SQL + Cache
var catalog = await mongoRepo.FirstOrDefaultAsync(c => c.SKU == sku); // MongoDB
```

## Best Practices

### 1. Use Unit of Work for Transactions

```csharp
// Good: All-or-nothing operations
using var transaction = await unitOfWork.BeginTransactionAsync();
try
{
    await repo1.AddAsync(entity1);
    await repo2.UpdateAsync(entity2);
    await unitOfWork.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

// Bad: Partial failures possible
await repo1.AddAsync(entity1);
await unitOfWork.SaveChangesAsync();
await repo2.UpdateAsync(entity2); // If this fails, entity1 is already saved
await unitOfWork.SaveChangesAsync();
```

### 2. Cache Expensive Queries

```csharp
// Cache frequently accessed data
var cachedRepo = new CachedRepository<Product, Guid>(
    productRepo,
    cacheProvider,
    new CacheOptions { Expiration = TimeSpan.FromMinutes(10) }
);

// Don't cache frequently changing data
var orderRepo = unitOfWork.Repository<Order, Guid>(); // No cache
```

### 3. Use Pagination for Large Datasets

```csharp
// Good: Paginated results
var page = await pagedRepo.GetPageAsync(pageNumber, 50);

// Bad: Load everything
var all = await repo.GetAllAsync(); // May load millions of records
```

### 4. Leverage Soft Delete

```csharp
// Implement ISoftDeletable for recoverable data
public class Invoice : Entity, ISoftDeletable
{
    // ...soft delete properties
}

// Regular entities for truly deleted data
public class TempFile : Entity
{
    // Hard delete when removed
}
```

### 5. Set Current User for Auditing

```csharp
// In your middleware or service
if (dbContext is ConduitDbContext conduitContext)
{
    conduitContext.SetCurrentUser(currentUserId);
}

// CreatedBy and UpdatedBy will be automatically set
```

## Thread Safety

All repository implementations are thread-safe for concurrent read operations. Write operations should be coordinated at the application level using appropriate locking or serialization mechanisms.

## Performance Tuning

### EF Core

```csharp
// Use AsNoTracking for read-only queries
var queryableRepo = (IQueryableRepository<Product, Guid>)productRepo;
var products = await queryableRepo.Query()
    .AsNoTracking()
    .Where(p => p.IsActive)
    .ToListAsync();

// Batch operations
await repo.AddRangeAsync(products); // Better than multiple AddAsync calls
```

### MongoDB

```csharp
// Use projection to reduce data transfer
var names = await mongoDb.GetCollection<Product>("products")
    .Find(p => p.IsActive)
    .Project(p => p.Name)
    .ToListAsync();

// Create indexes
await mongoDb.GetCollection<Product>("products")
    .Indexes.CreateOneAsync(
        new CreateIndexModel<Product>(
            Builders<Product>.IndexKeys.Ascending(p => p.Name)
        )
    );
```

### Redis

```csharp
// Use appropriate expiration times
await cache.SetAsync("frequently_accessed", data, TimeSpan.FromMinutes(10));
await cache.SetAsync("rarely_accessed", data, TimeSpan.FromHours(24));

// Batch cache operations when possible
var tasks = ids.Select(id => cache.GetAsync<Product>($"product:{id}"));
var products = await Task.WhenAll(tasks);
```

## Version History

- **0.4.0** (Current)
  - Initial release
  - Repository pattern with CRUD operations
  - Entity Framework Core support
  - MongoDB support
  - Redis caching
  - Unit of Work pattern
  - Soft delete and auditing
  - Pagination support

## License

Part of the Conduit framework. See main repository for license information.

## Related Modules

- **Conduit.Api**: Core interfaces and message types
- **Conduit.Common**: Shared utilities and extensions
- **Conduit.Messaging**: Message bus for domain events
- **Conduit.Security**: Authentication and authorization

## Support

For issues, questions, or contributions, please refer to the main Conduit repository.
