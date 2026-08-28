using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.UnitTests;

public class EntityTests
{
    private class TestEntity : Entity<Guid>
    {
        public TestEntity(Guid id) : base(id)
        {
        }
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenIdsAreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        // Act
        var areEqual = entity1.Equals(entity2);

        // Assert
        Assert.True(areEqual);
        Assert.True(entity1 == entity2);
        Assert.False(entity1 != entity2);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenIdsAreDifferent()
    {
        // Arrange
        var entity1 = new TestEntity(Guid.NewGuid());
        var entity2 = new TestEntity(Guid.NewGuid());

        // Act
        var areEqual = entity1.Equals(entity2);

        // Assert
        Assert.False(areEqual);
        Assert.False(entity1 == entity2);
        Assert.True(entity1 != entity2);
    }

    [Fact]
    public void GetHashCode_ShouldReturnSameHashCode_WhenIdsAreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        // Act
        var hash1 = entity1.GetHashCode();
        var hash2 = entity2.GetHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }
}
