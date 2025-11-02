using FluentAssertions;
using Moq;
using Conduit.Persistence;
using Conduit.Persistence.Caching;
using System.Linq.Expressions;

namespace Conduit.Persistence.Tests;

public class CachedRepositoryTests
{
    private readonly Mock<IRepository<TestEntity, Guid>> _mockRepository;
    private readonly Mock<ICacheProvider> _mockCacheProvider;
    private readonly CachedRepository<TestEntity, Guid> _cachedRepository;
    private readonly CacheOptions _cacheOptions;

    public CachedRepositoryTests()
    {
        _mockRepository = new Mock<IRepository<TestEntity, Guid>>();
        _mockCacheProvider = new Mock<ICacheProvider>();
        _cacheOptions = new CacheOptions
        {
            EnableCaching = true,
            KeyPrefix = "test",
            Expiration = TimeSpan.FromMinutes(5),
            CacheListQueries = true,
            CacheOnWrite = true
        };
        _cachedRepository = new CachedRepository<TestEntity, Guid>(
            _mockRepository.Object,
            _mockCacheProvider.Object,
            _cacheOptions);
    }

    [Fact]
    public void CachedRepository_Constructor_WithValidParameters_ShouldSucceed()
    {
        // Act
        var repository = new CachedRepository<TestEntity, Guid>(
            _mockRepository.Object,
            _mockCacheProvider.Object);

        // Assert
        repository.Should().NotBeNull();
    }

    [Fact]
    public void CachedRepository_Constructor_WithNullRepository_ShouldThrow()
    {
        // Act & Assert
        var act = () => new CachedRepository<TestEntity, Guid>(
            null!,
            _mockCacheProvider.Object);
        act.Should().Throw<ArgumentNullException>().WithMessage("*innerRepository*");
    }

    [Fact]
    public void CachedRepository_Constructor_WithNullCacheProvider_ShouldThrow()
    {
        // Act & Assert
        var act = () => new CachedRepository<TestEntity, Guid>(
            _mockRepository.Object,
            null!);
        act.Should().Throw<ArgumentNullException>().WithMessage("*cacheProvider*");
    }

    [Fact]
    public async Task GetByIdAsync_WithCachingEnabled_ShouldCheckCacheFirst()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var cachedEntity = new TestEntity { Id = entityId, Name = "Cached" };

        _mockCacheProvider
            .Setup(x => x.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<TestEntity?>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedEntity);

        // Act
        var result = await _cachedRepository.GetByIdAsync(entityId);

        // Assert
        result.Should().Be(cachedEntity);
        _mockCacheProvider.Verify(x => x.GetOrSetAsync(
            $"test:TestEntity:{entityId}",
            It.IsAny<Func<CancellationToken, Task<TestEntity?>>>(),
            _cacheOptions.Expiration,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WithCachingDisabled_ShouldCallRepositoryDirectly()
    {
        // Arrange
        _cacheOptions.EnableCaching = false;
        var entityId = Guid.NewGuid();
        var entity = new TestEntity { Id = entityId, Name = "Direct" };

        _mockRepository
            .Setup(x => x.GetByIdAsync(entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        var result = await _cachedRepository.GetByIdAsync(entityId);

        // Assert
        result.Should().Be(entity);
        _mockRepository.Verify(x => x.GetByIdAsync(entityId, It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheProvider.Verify(x => x.GetOrSetAsync(
            It.IsAny<string>(),
            It.IsAny<Func<CancellationToken, Task<TestEntity?>>>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_WithCachingEnabled_ShouldUseListCache()
    {
        // Arrange
        var entities = new[] { new TestEntity { Id = Guid.NewGuid(), Name = "Test" } };

        _mockCacheProvider
            .Setup(x => x.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<IEnumerable<TestEntity>>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities);

        // Act
        var result = await _cachedRepository.GetAllAsync();

        // Assert
        result.Should().BeEquivalentTo(entities);
        _mockCacheProvider.Verify(x => x.GetOrSetAsync(
            "test:TestEntity:all",
            It.IsAny<Func<CancellationToken, Task<IEnumerable<TestEntity>>>>(),
            _cacheOptions.ListExpiration,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_WithListCachingDisabled_ShouldCallRepositoryDirectly()
    {
        // Arrange
        _cacheOptions.CacheListQueries = false;
        var entities = new[] { new TestEntity { Id = Guid.NewGuid(), Name = "Test" } };

        _mockRepository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities);

        // Act
        var result = await _cachedRepository.GetAllAsync();

        // Assert
        result.Should().BeEquivalentTo(entities);
        _mockRepository.Verify(x => x.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheProvider.Verify(x => x.GetOrSetAsync(
            It.IsAny<string>(),
            It.IsAny<Func<CancellationToken, Task<IEnumerable<TestEntity>>>>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FindAsync_ShouldAlwaysCallRepositoryDirectly()
    {
        // Arrange
        var entities = new[] { new TestEntity { Id = Guid.NewGuid(), Name = "Test" } };
        Expression<Func<TestEntity, bool>> predicate = x => x.Name == "Test";

        _mockRepository
            .Setup(x => x.FindAsync(predicate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities);

        // Act
        var result = await _cachedRepository.FindAsync(predicate);

        // Assert
        result.Should().BeEquivalentTo(entities);
        _mockRepository.Verify(x => x.FindAsync(predicate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAsync_WithCachingEnabled_ShouldInvalidateListCacheAndOptionallyCache()
    {
        // Arrange
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "New" };

        _mockRepository
            .Setup(x => x.AddAsync(entity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        var result = await _cachedRepository.AddAsync(entity);

        // Assert
        result.Should().Be(entity);
        _mockRepository.Verify(x => x.AddAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheProvider.Verify(x => x.RemoveAsync("test:TestEntity:all", It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheProvider.Verify(x => x.SetAsync(
            $"test:TestEntity:{entity.Id}",
            entity,
            _cacheOptions.Expiration,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithCachingEnabled_ShouldInvalidateCaches()
    {
        // Arrange
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Updated" };

        // Act
        await _cachedRepository.UpdateAsync(entity);

        // Assert
        _mockRepository.Verify(x => x.UpdateAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheProvider.Verify(x => x.RemoveAsync($"test:TestEntity:{entity.Id}", It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheProvider.Verify(x => x.RemoveAsync("test:TestEntity:all", It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheProvider.Verify(x => x.SetAsync(
            $"test:TestEntity:{entity.Id}",
            entity,
            _cacheOptions.Expiration,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WithCachingEnabled_ShouldInvalidateCaches()
    {
        // Arrange
        var entityId = Guid.NewGuid();

        _mockRepository
            .Setup(x => x.DeleteAsync(entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _cachedRepository.DeleteAsync(entityId);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(x => x.DeleteAsync(entityId, It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheProvider.Verify(x => x.RemoveAsync($"test:TestEntity:{entityId}", It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheProvider.Verify(x => x.RemoveAsync("test:TestEntity:all", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenDeleteFails_ShouldNotInvalidateCache()
    {
        // Arrange
        var entityId = Guid.NewGuid();

        _mockRepository
            .Setup(x => x.DeleteAsync(entityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _cachedRepository.DeleteAsync(entityId);

        // Assert
        result.Should().BeFalse();
        _mockRepository.Verify(x => x.DeleteAsync(entityId, It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheProvider.Verify(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteWhereAsync_WithCachingEnabled_ShouldClearAllCaches()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> predicate = x => x.Name.StartsWith("Test");

        _mockRepository
            .Setup(x => x.DeleteWhereAsync(predicate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var result = await _cachedRepository.DeleteWhereAsync(predicate);

        // Assert
        result.Should().Be(5);
        _mockRepository.Verify(x => x.DeleteWhereAsync(predicate, It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheProvider.Verify(x => x.RemoveByPatternAsync(
            "test:TestEntity:*",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteWhereAsync_WhenNoEntitiesDeleted_ShouldNotClearCache()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> predicate = x => x.Name.StartsWith("Test");

        _mockRepository
            .Setup(x => x.DeleteWhereAsync(predicate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _cachedRepository.DeleteWhereAsync(predicate);

        // Assert
        result.Should().Be(0);
        _mockRepository.Verify(x => x.DeleteWhereAsync(predicate, It.IsAny<CancellationToken>()), Times.Once);
        _mockCacheProvider.Verify(x => x.RemoveByPatternAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // Test entity for testing
    public class TestEntity : Entity
    {
        public string Name { get; set; } = string.Empty;
    }
}