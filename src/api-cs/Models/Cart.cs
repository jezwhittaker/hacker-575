namespace OctoSupply.Api.Models;

public sealed record CartItem(
    int CartItemId,
    int ProductId,
    string Name,
    string? ImgName,
    double UnitPrice,
    double Discount,
    int Quantity,
    double LineTotal);

public sealed record Cart(
    int CartId,
    IReadOnlyList<CartItem> Items,
    int ItemCount,
    double Total);

public sealed class AddCartItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public sealed class UpdateCartItemRequest
{
    public int Quantity { get; set; }
}
