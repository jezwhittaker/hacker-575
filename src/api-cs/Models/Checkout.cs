namespace OctoSupply.Api.Models;

public sealed class CheckoutRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;
    public string Cvc { get; set; } = string.Empty;
}

public sealed record CheckoutOrderItem(
    int ProductId,
    string Name,
    int Quantity,
    double UnitPrice,
    double LineTotal);

public sealed record CheckoutResult(
    int OrderId,
    string Status,
    string OrderDate,
    IReadOnlyList<CheckoutOrderItem> Items,
    double Total,
    string Currency,
    string PaymentReference,
    string CardLast4);
