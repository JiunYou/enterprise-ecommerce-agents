using EnterpriseCommerce.Application.Identity.Commands.ResolveCustomerIdentity;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace EnterpriseCommerce.Application.UnitTests.Identity.Commands.ResolveCustomerIdentity;

public class ResolveCustomerIdentityCommandValidatorTests
{
    private readonly ResolveCustomerIdentityCommandValidator _validator;

    public ResolveCustomerIdentityCommandValidatorTests()
    {
        _validator = new ResolveCustomerIdentityCommandValidator();
    }

    [Fact]
    public void Should_Have_NoError_When_Command_Is_Valid()
    {
        var command = new ResolveCustomerIdentityCommand("https://auth.example.com/", "auth0|valid-sub");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Accept_Subject_With_Length_255()
    {
        var subject255 = new string('a', 255);
        var command = new ResolveCustomerIdentityCommand("https://auth.example.com/", subject255);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Subject);
    }

    [Fact]
    public void Should_Reject_Subject_With_Length_256()
    {
        var subject256 = new string('a', 256);
        var command = new ResolveCustomerIdentityCommand("https://auth.example.com/", subject256);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Subject)
            .WithErrorMessage("Subject must not exceed 255 characters.");
    }

    [Theory]
    [InlineData("auth0|使用者")]
    [InlineData("auth0|user@🎉")]
    [InlineData("auth0|user\u00A9")]
    [InlineData("auth0|user\u2603")]
    public void Should_Reject_Non_Ascii_Subject(string nonAsciiSubject)
    {
        var command = new ResolveCustomerIdentityCommand("https://auth.example.com/", nonAsciiSubject);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Subject)
            .WithErrorMessage("Subject must contain only ASCII characters.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Have_Error_When_Issuer_Is_Null_Or_Whitespace(string? issuer)
    {
        var command = new ResolveCustomerIdentityCommand(issuer!, "auth0|valid-sub");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Issuer);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Have_Error_When_Subject_Is_Null_Or_Whitespace(string? subject)
    {
        var command = new ResolveCustomerIdentityCommand("https://auth.example.com/", subject!);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Subject);
    }

    [Fact]
    public void Should_Have_Error_When_Issuer_Exceeds_MaxLength()
    {
        var longIssuer = "https://auth.example.com/" + new string('a', 512);
        var command = new ResolveCustomerIdentityCommand(longIssuer, "auth0|valid-sub");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Issuer);
    }
}
