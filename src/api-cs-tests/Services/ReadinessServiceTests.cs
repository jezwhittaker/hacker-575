using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OctoSupply.Api.Data;
using OctoSupply.Api.Services;
using Xunit;

namespace OctoSupply.Api.Tests.Services;

public sealed class ReadinessServiceTests
{
    [Fact]
    public async Task Check_WithoutSchema_ReportsNotReady()
    {
        await WithDatabaseAsync(async factory =>
        {
            var result = await new ReadinessService(factory, NullLogger<ReadinessService>.Instance).CheckAsync();

            Assert.False(result.Ready);
            Assert.Equal("unavailable", result.Database);
        });
    }

    [Fact]
    public async Task Check_WithProductsTable_ReportsReady()
    {
        await WithDatabaseAsync(async factory =>
        {
            await using (var connection = await factory.OpenConnectionAsync())
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE products (product_id INTEGER PRIMARY KEY)";
                await command.ExecuteNonQueryAsync();
            }

            var result = await new ReadinessService(factory, NullLogger<ReadinessService>.Instance).CheckAsync();

            Assert.True(result.Ready);
            Assert.Null(result.Error);
        });
    }

    private static async Task WithDatabaseAsync(Func<SqliteConnectionFactory, Task> test)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"octosupply-ready-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("DB_FILE", dbPath);
        try
        {
            await test(new SqliteConnectionFactory(new NullLogger<SqliteConnectionFactory>()));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DB_FILE", null);
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }
}
