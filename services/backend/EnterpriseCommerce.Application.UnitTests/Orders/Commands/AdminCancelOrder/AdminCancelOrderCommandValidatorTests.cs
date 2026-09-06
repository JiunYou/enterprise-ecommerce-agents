using EnterpriseCommerce.Application.Orders.Commands.AdminCancelOrder;
using FluentAssertions;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Commands.AdminCancelOrder;

public class AdminCancelOrderCommandValidatorTests
{
    private readonly AdminCancelOrderCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldBeValid()
    {
        var command = new AdminCancelOrderCommand(
            Guid.NewGuid(),
            "https://auth.example.com/",
            "auth0|admin-1",
            "Valid cancellation reason.");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyOrNullReason_ShouldBeInvalid(string? reason)
    {
        var command = new AdminCancelOrderCommand(
            Guid.NewGuid(),
            "https://auth.example.com/",
            "auth0|admin-1",
            reason!);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Reason));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t\t")]
    [InlineData("\n")]
    public void Validate_WhitespaceReason_ShouldBeInvalid(string whitespaceReason)
    {
        var command = new AdminCancelOrderCommand(
            Guid.NewGuid(),
            "https://auth.example.com/",
            "auth0|admin-1",
            whitespaceReason);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Reason));
    }

    [Fact]
    public void Validate_Reason500Chars_ShouldBeValid()
    {
        var command = new AdminCancelOrderCommand(
            Guid.NewGuid(),
            "https://auth.example.com/",
            "auth0|admin-1",
            new string('A', 500));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Reason501Chars_ShouldBeInvalid()
    {
        var command = new AdminCancelOrderCommand(
            Guid.NewGuid(),
            "https://auth.example.com/",
            "auth0|admin-1",
            new string('A', 501));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Reason));
    }

    [Fact]
    public void Validate_MissingOrderId_ShouldBeInvalid()
    {
        var command = new AdminCancelOrderCommand(
            Guid.Empty,
            "https://auth.example.com/",
            "auth0|admin-1",
            "Valid reason");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.OrderId));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_MissingActorIssuer_ShouldBeInvalid(string issuer)
    {
        var command = new AdminCancelOrderCommand(
            Guid.NewGuid(),
            issuer,
            "auth0|admin-1",
            "Valid reason");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.ActorIssuer));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_MissingActorSubject_ShouldBeInvalid(string subject)
    {
        var command = new AdminCancelOrderCommand(
            Guid.NewGuid(),
            "https://auth.example.com/",
            subject,
            "Valid reason");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.ActorSubject));
    }
}
