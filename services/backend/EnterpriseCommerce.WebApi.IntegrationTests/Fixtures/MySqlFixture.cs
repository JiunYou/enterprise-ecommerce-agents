using Testcontainers.MySql;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;

public class MySqlFixture : IAsyncLifetime
{
    private readonly MySqlContainer _mySqlContainer;

    public string ConnectionString => _mySqlContainer.GetConnectionString() + ";Max Pool Size=500;";

    public MySqlFixture()
    {
        _mySqlContainer = new MySqlBuilder("mysql:8.0")
            .WithDatabase("enterprise_commerce")
            .WithUsername("test")
            .WithPassword("test")
            .WithCommand("--max_connections=1000", "--max_connect_errors=10000")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _mySqlContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _mySqlContainer.DisposeAsync();
    }
}
