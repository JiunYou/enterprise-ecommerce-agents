using EnterpriseCommerce.Domain.Marketing;
using FluentAssertions;
using System;
using Xunit;

namespace EnterpriseCommerce.Domain.UnitTests.Marketing;

public class WishlistItemTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldSucceedAndPreserveProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var addedAt = new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

        // Act
        var result = WishlistItem.Create(id, customerId, productId, addedAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(id);
        result.Value.CustomerId.Should().Be(customerId);
        result.Value.ProductId.Should().Be(productId);
        result.Value.AddedAt.Should().Be(addedAt);
    }

    [Fact]
    public void Create_WithEmptyId_ShouldFailWithInvalidIdError()
    {
        // Arrange
        var id = Guid.Empty;
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var addedAt = DateTimeOffset.UtcNow;

        // Act
        var result = WishlistItem.Create(id, customerId, productId, addedAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WishlistErrors.InvalidId.Code);
    }

    [Fact]
    public void Create_WithEmptyCustomerId_ShouldFailWithInvalidCustomerIdError()
    {
        // Arrange
        var id = Guid.NewGuid();
        var customerId = Guid.Empty;
        var productId = Guid.NewGuid();
        var addedAt = DateTimeOffset.UtcNow;

        // Act
        var result = WishlistItem.Create(id, customerId, productId, addedAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WishlistErrors.InvalidCustomerId.Code);
    }

    [Fact]
    public void Create_WithEmptyProductId_ShouldFailWithInvalidProductIdError()
    {
        // Arrange
        var id = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.Empty;
        var addedAt = DateTimeOffset.UtcNow;

        // Act
        var result = WishlistItem.Create(id, customerId, productId, addedAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WishlistErrors.InvalidProductId.Code);
    }
}
