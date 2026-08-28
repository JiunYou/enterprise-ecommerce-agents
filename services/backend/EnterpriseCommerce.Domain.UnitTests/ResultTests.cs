using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.UnitTests;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = Result.Success();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_ShouldCreateFailedResult()
    {
        // Arrange
        var error = new Error("Test.Error", "A test error occurred.");

        // Act
        var result = Result.Failure(error);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void SuccessWithValue_ShouldCreateSuccessfulResultWithValue()
    {
        // Arrange
        var value = "Success Value";

        // Act
        var result = Result.Success(value);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value);
    }

    [Fact]
    public void Value_AccessingValueOnFailure_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var result = Result.Failure<string>(new Error("Code", "Message"));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
