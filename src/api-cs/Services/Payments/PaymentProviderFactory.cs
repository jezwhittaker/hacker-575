namespace OctoSupply.Api.Services.Payments;

public static class PaymentProviderFactory
{
    public static IPaymentProvider Create(PaymentOptions options) =>
        options.Provider switch
        {
            PaymentOptions.SimulatedProvider => new SimulatedPaymentProvider(),
            _ => throw new InvalidOperationException(
                $"Unsupported PAYMENT_PROVIDER '{options.Provider}'. Supported providers: {PaymentOptions.SimulatedProvider}.")
        };
}
