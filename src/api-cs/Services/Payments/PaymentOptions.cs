namespace OctoSupply.Api.Services.Payments;

// Payment settings come from the environment so no keys or provider details live in source.
public sealed record PaymentOptions(string Provider, string? ApiKey, string Currency)
{
    public const string SimulatedProvider = "simulated";

    public static PaymentOptions FromEnvironment()
    {
        var provider = Environment.GetEnvironmentVariable("PAYMENT_PROVIDER");
        var apiKey = Environment.GetEnvironmentVariable("PAYMENT_API_KEY");
        var currency = Environment.GetEnvironmentVariable("PAYMENT_CURRENCY");

        return new PaymentOptions(
            string.IsNullOrWhiteSpace(provider) ? SimulatedProvider : provider.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
            string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant());
    }
}
