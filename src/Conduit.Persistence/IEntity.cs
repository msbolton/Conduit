using System;

namespace Conduit.Persistence
{
    /// <summary>
    /// Base interface for all entities.
    /// </summary>
    public interface IEntity
    {
        /// <summary>
        /// Gets the entity identifier.
        /// </summary>
        object GetId();
    }

    /// <summary>
    /// Generic entity interface with typed identifier.
    /// </summary>
    /// <typeparam name="TId">The type of the identifier</typeparam>
    public interface IEntity<TId> : IEntity where TId : notnull
    {
        /// <summary>
        /// Gets or sets the entity identifier.
        /// </summary>
        TId Id { get; set; }
    }

    /// <summary>
    /// Base entity class with GUID identifier.
    /// </summary>
    public abstract class Entity : IEntity<Guid>
    {
        /// <summary>
        /// Gets or sets the entity identifier.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets the entity identifier as an object.
        /// </summary>
        public object GetId() => Id;
    }

    /// <summary>
    /// Base entity class with typed identifier.
    /// </summary>
    /// <typeparam name="TId">The type of the identifier</typeparam>
    public abstract class Entity<TId> : IEntity<TId> where TId : notnull
    {
        /// <summary>
        /// Gets or sets the entity identifier.
        /// </summary>
        public TId Id { get; set; } = default!;

        /// <summary>
        /// Gets the entity identifier as an object.
        /// </summary>
        public object GetId() => Id;
    }

    /// <summary>
    /// Interface for entities with auditing support.
    /// </summary>
    public interface IAuditableEntity
    {
        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the user who created the entity.
        /// </summary>
        string? CreatedBy { get; set; }

        /// <summary>
        /// Gets or sets the last modification timestamp.
        /// </summary>
        DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the user who last modified the entity.
        /// </summary>
        string? UpdatedBy { get; set; }
    }

    /// <summary>
    /// Base auditable entity class.
    /// </summary>
    public abstract class AuditableEntity : Entity, IAuditableEntity
    {
        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the user who created the entity.
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Gets or sets the last modification timestamp.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the user who last modified the entity.
        /// </summary>
        public string? UpdatedBy { get; set; }
    }

    /// <summary>
    /// Interface for soft-deletable entities.
    /// </summary>
    public interface ISoftDeletable
    {
        /// <summary>
        /// Gets or sets a value indicating whether the entity is deleted.
        /// </summary>
        bool IsDeleted { get; set; }

        /// <summary>
        /// Gets or sets the deletion timestamp.
        /// </summary>
        DateTime? DeletedAt { get; set; }

        /// <summary>
        /// Gets or sets the user who deleted the entity.
        /// </summary>
        string? DeletedBy { get; set; }
    }
}
