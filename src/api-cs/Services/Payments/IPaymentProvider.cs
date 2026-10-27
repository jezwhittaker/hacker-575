namespace OctoSupply.Api.Services.Payments;

public sealed record PaymentCharge(
    long AmountMinorUnits,
    string Currency,
    string CardNumber,
    string Expiry,
    string Cvc,
    string Description);

public sealed record PaymentOutcome(bool Approved, string Reference, string? FailureReason);

public interface IPaymentProvider
{
    string Name { get; }

    Task<PaymentOutcome> ChargeAsync(PaymentCharge charge);

    // Reverses an approved charge when the order could not be recorded.
    Task VoidAsync(string reference);
}
