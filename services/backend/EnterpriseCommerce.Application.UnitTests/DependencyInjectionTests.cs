using EnterpriseCommerce.Application.Common.CQRS;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseCommerce.Application.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_ShouldRegisterMediatR()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Required by MediatR 12+

        // Act
        services.AddApplication();
        var provider = services.BuildServiceProvider();

        // Assert
        var mediator = provider.GetService<IMediator>();
        Assert.NotNull(mediator);
    }
}
