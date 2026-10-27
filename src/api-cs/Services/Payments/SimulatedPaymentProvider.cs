namespace OctoSupply.Api.Services.Payments;

// Offline provider for development and tests, driven by well-known test card numbers.
public sealed class SimulatedPaymentProvider : IPaymentProvider
{
    public const string DeclinedCard = "4000000000000002";
    public const string InsufficientFundsCard = "4000000000009995";

    public string Name => PaymentOptions.SimulatedProvider;

    public Task<PaymentOutcome> ChargeAsync(PaymentCharge charge)
    {
        var reference = $"sim_{Guid.NewGuid():N}";
        var digits = charge.CardNumber;

        if (!PassesLuhn(digits))
        {
            return Task.FromResult(new PaymentOutcome(false, reference, "The card number is invalid."));
        }
        if (IsExpired(charge.Expiry))
        {
            return Task.FromResult(new PaymentOutcome(false, reference, "The card has expired."));
        }

        return Task.FromResult(digits switch
        {
            DeclinedCard => new PaymentOutcome(false, reference, "Your card was declined."),
            InsufficientFundsCard => new PaymentOutcome(false, reference, "Your card has insufficient funds."),
            _ => new PaymentOutcome(true, reference, null)
        });
    }

    public Task VoidAsync(string reference) => Task.CompletedTask;

    public static bool PassesLuhn(string digits)
    {
        if (digits.Length < 12 || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var value = digits[i] - '0';
            if (alternate)
            {
                value *= 2;
                if (value > 9)
                {
                    value -= 9;
                }
            }
            sum += value;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }

    // Expiry is MM/YY and valid through the end of the stated month.
    private static bool IsExpired(string expiry)
    {
        var parts = expiry.Split('/');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var month) || !int.TryParse(parts[1], out var year)
            || month is < 1 or > 12)
        {
            return true;
        }

        var endOfMonth = new DateTime(2000 + year, month, 1).AddMonths(1);
        return endOfMonth <= DateTime.UtcNow;
    }
}
