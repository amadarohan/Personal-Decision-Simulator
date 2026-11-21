namespace DecisionSimulator.Domain.Common;

/// <summary>
/// Base type for all persisted entities. Identity is a client-generated
/// <see cref="Guid"/> so that aggregates can be composed before persistence.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public override bool Equals(object? obj)
        => obj is Entity other && other.GetType() == GetType() && other.Id == Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

/// <summary>
/// Entity that records creation and last-modification timestamps in UTC.
/// </summary>
public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAtUtc { get; protected set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; protected set; } = DateTime.UtcNow;

    public void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}

/// <summary>
/// Marks an entity that participates in optimistic concurrency control through
/// a SQL Server <c>rowversion</c> column.
/// </summary>
public interface IConcurrencyGuarded
{
    byte[] RowVersion { get; }
}

/// <summary>
/// Marker for aggregate roots. Repositories are only defined for these.
/// </summary>
public interface IAggregateRoot;
