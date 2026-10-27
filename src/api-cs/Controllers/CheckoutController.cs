using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using OctoSupply.Api.Models;
using OctoSupply.Api.Services;
using OctoSupply.Api.Utils;

namespace OctoSupply.Api.Controllers;

[ApiController]
[Route("api/carts")]
public sealed partial class CheckoutController(CheckoutService service) : ControllerBase
{
    private readonly CheckoutService _service = service;

    [HttpPost("{id:int}/checkout")]
    public async Task<ActionResult<CheckoutResult>> Checkout(int id, [FromBody] CheckoutRequest request)
    {
        Normalize(request);
        Validate(request);
        var result = await _service.CheckoutAsync(id, request);
        return CreatedAtAction(nameof(OrdersController.GetById), "Orders", new { id = result.OrderId }, result);
    }

    // Spaces and dashes are common in card entry; everything else is validated as-is.
    private static void Normalize(CheckoutRequest request)
    {
        request.CustomerName = request.CustomerName?.Trim() ?? string.Empty;
        request.Email = request.Email?.Trim() ?? string.Empty;
        request.CardNumber = (request.CardNumber ?? string.Empty).Replace(" ", "").Replace("-", "");
        request.Expiry = request.Expiry?.Trim() ?? string.Empty;
        request.Cvc = request.Cvc?.Trim() ?? string.Empty;
    }

    private static void Validate(CheckoutRequest request)
    {
        if (request.CustomerName.Length == 0)
        {
            throw new ValidationException("customerName is required");
        }
        if (!EmailPattern().IsMatch(request.Email))
        {
            throw new ValidationException("a valid email is required");
        }
        if (!CardNumberPattern().IsMatch(request.CardNumber))
        {
            throw new ValidationException("cardNumber must be 12 to 19 digits");
        }
        if (!ExpiryPattern().IsMatch(request.Expiry))
        {
            throw new ValidationException("expiry must be in MM/YY format");
        }
        if (!CvcPattern().IsMatch(request.Cvc))
        {
            throw new ValidationException("cvc must be 3 or 4 digits");
        }
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\d{12,19}$")]
    private static partial Regex CardNumberPattern();

    [GeneratedRegex(@"^(0[1-9]|1[0-2])/\d{2}$")]
    private static partial Regex ExpiryPattern();

    [GeneratedRegex(@"^\d{3,4}$")]
    private static partial Regex CvcPattern();
}
