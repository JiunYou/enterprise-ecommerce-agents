using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Commands.CreateOrder;
using EnterpriseCommerce.Domain.Orders;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Commands.CreateOrder;

public class CreateOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly CreateOrderCommandHandler _handler;

    public CreateOrderCommandHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new CreateOrderCommandHandler(_orderRepositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_Should_CreateOrder_And_SaveChanges()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var command = new CreateOrderCommand(customerId, "TWD");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);

        _orderRepositoryMock.Verify(x => x.Add(It.Is<Order>(o => o.CustomerId == customerId && o.Currency == "TWD")), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
