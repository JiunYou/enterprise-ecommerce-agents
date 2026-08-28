namespace EnterpriseCommerce.Domain.Primitives;

public abstract class Entity<TId> : IEquatable<Entity<TId>>
{
    public TId Id { get; protected set; } = default!;

    protected Entity(TId id)
    {
        Id = id;
    }

    protected Entity()
    {
    }

    public static bool operator ==(Entity<TId>? first, Entity<TId>? second)
    {
        if (first is null && second is null)
            return true;
        
        if (first is null || second is null)
            return false;

        return first.Equals(second);
    }

    public static bool operator !=(Entity<TId>? first, Entity<TId>? second)
    {
        return !(first == second);
    }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other.GetType() != GetType())
        {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        if (obj is not Entity<TId> other)
        {
            return false;
        }

        return Equals(other);
    }

    public override int GetHashCode()
    {
        return EqualityComparer<TId>.Default.GetHashCode(Id!) * 41;
    }
}
