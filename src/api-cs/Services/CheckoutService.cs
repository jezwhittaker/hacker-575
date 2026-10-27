using OctoSupply.Api.Models;
using OctoSupply.Api.Repositories;
using OctoSupply.Api.Services.Payments;
using OctoSupply.Api.Utils;

namespace OctoSupply.Api.Services;

public sealed class CheckoutService(
    CartsRepository carts,
    CheckoutRepository checkout,
    IPaymentProvider paymentProvider,
    PaymentOptions options,
    ILogger<CheckoutService> logger)
{
    public async Task<CheckoutResult> CheckoutAsync(int cartId, CheckoutRequest request)
    {
        var cart = await carts.FindByIdAsync(cartId) ?? throw new NotFoundException("Cart", cartId);
        if (cart.Items.Count == 0)
        {
            throw new ValidationException("cart is empty");
        }

        // The charge happens before anything is written, so a failure leaves the cart untouched.
        var outcome = await paymentProvider.ChargeAsync(new PaymentCharge(
            (long)Math.Round(cart.Total * 100, MidpointRounding.AwayFromZero),
            options.Currency,
            request.CardNumber,
            request.Expiry,
            request.Cvc,
            $"OctoCAT Supply cart {cartId}"));

        if (!outcome.Approved)
        {
            logger.LogInformation("Payment declined for cart {CartId}: {Reason}", cartId, outcome.FailureReason);
            throw new PaymentDeclinedException(outcome.FailureReason ?? "the payment was not approved");
        }

        try
        {
            return await checkout.PlaceOrderAsync(
                cartId, request, cart.Total, options.Currency, paymentProvider.Name, outcome.Reference);
        }
        catch
        {
            // The card was charged but no order exists, so reverse the charge before surfacing the failure.
            try
            {
                await paymentProvider.VoidAsync(outcome.Reference);
            }
            catch (Exception voidError)
            {
                logger.LogError(voidError, "Failed to void payment {Reference} for cart {CartId}", outcome.Reference, cartId);
            }
            throw;
        }
    }
}
