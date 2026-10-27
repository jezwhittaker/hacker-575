using OctoSupply.Api.Data;

namespace OctoSupply.Api.Services;

public sealed record ReadinessResult(bool Ready, string Database, string? Error);

public sealed class ReadinessService(SqliteConnectionFactory connectionFactory, ILogger<ReadinessService> logger)
{
    public async Task<ReadinessResult> CheckAsync()
    {
        try
        {
            await using var connection = await connectionFactory.OpenConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM products";
            await command.ExecuteScalarAsync();
            return new ReadinessResult(true, "ok", null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Readiness check failed");
            return new ReadinessResult(false, "unavailable", ex.GetType().Name);
        }
    }
}
