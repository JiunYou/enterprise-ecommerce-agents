using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.UnitTests;

public class ValueObjectTests
{
    private class TestValueObject : ValueObject
    {
        public string Property1 { get; }
        public int Property2 { get; }

        public TestValueObject(string property1, int property2)
        {
            Property1 = property1;
            Property2 = property2;
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Property1;
            yield return Property2;
        }
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenPropertiesAreEqual()
    {
        // Arrange
        var vo1 = new TestValueObject("test", 1);
        var vo2 = new TestValueObject("test", 1);

        // Act
        var areEqual = vo1.Equals(vo2);

        // Assert
        Assert.True(areEqual);
        Assert.True(vo1 == vo2);
        Assert.False(vo1 != vo2);
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenPropertiesAreDifferent()
    {
        // Arrange
        var vo1 = new TestValueObject("test", 1);
        var vo2 = new TestValueObject("test", 2);

        // Act
        var areEqual = vo1.Equals(vo2);

        // Assert
        Assert.False(areEqual);
        Assert.False(vo1 == vo2);
        Assert.True(vo1 != vo2);
    }

    [Fact]
    public void GetHashCode_ShouldReturnSameHashCode_WhenPropertiesAreEqual()
    {
        // Arrange
        var vo1 = new TestValueObject("test", 1);
        var vo2 = new TestValueObject("test", 1);

        // Act
        var hash1 = vo1.GetHashCode();
        var hash2 = vo2.GetHashCode();

        // Assert
        Assert.Equal(hash1, hash2);
    }
}
