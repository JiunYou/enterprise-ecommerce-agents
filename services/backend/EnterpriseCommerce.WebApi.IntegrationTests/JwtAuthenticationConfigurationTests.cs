using EnterpriseCommerce.WebApi.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EnterpriseCommerce.WebApi.IntegrationTests;

public class JwtAuthenticationConfigurationTests
{
    [Fact]
    public void AddJwtAuthentication_WithValidConfiguration_ConfiguresJwtBearerOptionsCorrectly()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = "https://auth.example.com",
            ["Authentication:Audience"] = "test-api",
            ["Authentication:IdentityResolverClientId"] = "test-resolver-client"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddJwtAuthentication(configuration);
        var serviceProvider = services.BuildServiceProvider();
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        var jwtOptions = optionsMonitor.Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        jwtOptions.Authority.Should().Be("https://auth.example.com");
        jwtOptions.Audience.Should().Be("test-api");
        jwtOptions.RequireHttpsMetadata.Should().BeTrue();
        jwtOptions.TokenValidationParameters.Should().NotBeNull();
        jwtOptions.TokenValidationParameters.ValidateIssuer.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateAudience.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateLifetime.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateIssuerSigningKey.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddJwtAuthentication_WithMissingOrBlankAuthority_ThrowsInvalidOperationException(string? authority)
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = authority,
            ["Authentication:Audience"] = "test-api",
            ["Authentication:IdentityResolverClientId"] = "test-resolver-client"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        // Act
        var act = () => services.AddJwtAuthentication(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Authentication:Authority*required*");
    }

    [Theory]
    [InlineData("not-a-valid-uri")]
    [InlineData("http://insecure.example.com")]
    [InlineData("/relative/path")]
    [InlineData("ftp://files.example.com")]
    public void AddJwtAuthentication_WithInvalidOrNonHttpsAuthority_ThrowsInvalidOperationException(string authority)
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = authority,
            ["Authentication:Audience"] = "test-api",
            ["Authentication:IdentityResolverClientId"] = "test-resolver-client"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        // Act
        var act = () => services.AddJwtAuthentication(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Authentication:Authority*valid absolute HTTPS URI*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddJwtAuthentication_WithMissingOrBlankAudience_ThrowsInvalidOperationException(string? audience)
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = "https://auth.example.com",
            ["Authentication:Audience"] = audience,
            ["Authentication:IdentityResolverClientId"] = "test-resolver-client"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        // Act
        var act = () => services.AddJwtAuthentication(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Authentication:Audience*required*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddJwtAuthentication_WithMissingOrBlankIdentityResolverClientId_ThrowsInvalidOperationException(string? clientId)
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = "https://auth.example.com",
            ["Authentication:Audience"] = "test-api",
            ["Authentication:IdentityResolverClientId"] = clientId
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        // Act
        var act = () => services.AddJwtAuthentication(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Authentication:IdentityResolverClientId*required*");
    }
}
