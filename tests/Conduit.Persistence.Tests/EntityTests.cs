using FluentAssertions;
using Conduit.Persistence;

namespace Conduit.Persistence.Tests;

public class EntityTests
{
    [Fact]
    public void Entity_Constructor_ShouldSetGuidId()
    {
        // Act
        var entity = new TestEntity();

        // Assert
        entity.Id.Should().NotBeEmpty();
        entity.GetId().Should().Be(entity.Id);
    }

    [Fact]
    public void Entity_GetId_ShouldReturnIdAsObject()
    {
        // Arrange
        var entity = new TestEntity();
        var expectedId = entity.Id;

        // Act
        var result = entity.GetId();

        // Assert
        result.Should().BeOfType<Guid>();
        result.Should().Be(expectedId);
    }

    [Fact]
    public void Entity_SetId_ShouldUpdateId()
    {
        // Arrange
        var entity = new TestEntity();
        var newId = Guid.NewGuid();

        // Act
        entity.Id = newId;

        // Assert
        entity.Id.Should().Be(newId);
        entity.GetId().Should().Be(newId);
    }

    [Fact]
    public void GenericEntity_Constructor_ShouldSetDefaultId()
    {
        // Act
        var entity = new TestTypedEntity();

        // Assert
        entity.Id.Should().Be(0); // Default for int
        entity.GetId().Should().Be(0);
    }

    [Fact]
    public void GenericEntity_SetId_ShouldUpdateId()
    {
        // Arrange
        var entity = new TestTypedEntity();
        var newId = 42;

        // Act
        entity.Id = newId;

        // Assert
        entity.Id.Should().Be(newId);
        entity.GetId().Should().Be(newId);
    }

    [Fact]
    public void AuditableEntity_Constructor_ShouldSetCreatedAt()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var entity = new TestAuditableEntity();
        var afterCreation = DateTime.UtcNow.AddSeconds(1);

        // Assert
        entity.CreatedAt.Should().BeAfter(beforeCreation);
        entity.CreatedAt.Should().BeBefore(afterCreation);
        entity.CreatedBy.Should().BeNull();
        entity.UpdatedAt.Should().BeNull();
        entity.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public void AuditableEntity_SetAuditProperties_ShouldUpdateValues()
    {
        // Arrange
        var entity = new TestAuditableEntity();
        var createdBy = "test-user";
        var updatedAt = DateTime.UtcNow.AddHours(1);
        var updatedBy = "updated-user";

        // Act
        entity.CreatedBy = createdBy;
        entity.UpdatedAt = updatedAt;
        entity.UpdatedBy = updatedBy;

        // Assert
        entity.CreatedBy.Should().Be(createdBy);
        entity.UpdatedAt.Should().Be(updatedAt);
        entity.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void SoftDeletableEntity_SetDeletionProperties_ShouldUpdateValues()
    {
        // Arrange
        var entity = new TestSoftDeletableEntity();
        var deletedAt = DateTime.UtcNow;
        var deletedBy = "deleter-user";

        // Act
        entity.IsDeleted = true;
        entity.DeletedAt = deletedAt;
        entity.DeletedBy = deletedBy;

        // Assert
        entity.IsDeleted.Should().BeTrue();
        entity.DeletedAt.Should().Be(deletedAt);
        entity.DeletedBy.Should().Be(deletedBy);
    }

    [Fact]
    public void SoftDeletableEntity_DefaultValues_ShouldBeNotDeleted()
    {
        // Act
        var entity = new TestSoftDeletableEntity();

        // Assert
        entity.IsDeleted.Should().BeFalse();
        entity.DeletedAt.Should().BeNull();
        entity.DeletedBy.Should().BeNull();
    }

    // Test entity classes for testing
    private class TestEntity : Entity
    {
    }

    private class TestTypedEntity : Entity<int>
    {
    }

    private class TestAuditableEntity : AuditableEntity
    {
    }

    private class TestSoftDeletableEntity : Entity, ISoftDeletable
    {
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}