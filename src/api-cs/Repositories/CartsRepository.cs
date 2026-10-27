using Microsoft.Data.Sqlite;
using OctoSupply.Api.Data;
using OctoSupply.Api.Models;
using OctoSupply.Api.Utils;

namespace OctoSupply.Api.Repositories;

public sealed class CartsRepository(SqliteConnectionFactory connectionFactory)
{
    public const int MaxItemQuantity = 99;

    private readonly SqliteConnectionFactory _connectionFactory = connectionFactory;

    public async Task<Cart> CreateAsync()
    {
        const string sql = """
            INSERT INTO carts DEFAULT VALUES;
            SELECT last_insert_rowid();
            """;

        await using var connection = await _connectionFactory.OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return await FindByIdAsync(id) ?? throw new DatabaseException("Failed to retrieve created Cart");
    }

    public async Task<Cart?> FindByIdAsync(int id)
    {
        const string cartSql = "SELECT COUNT(*) FROM carts WHERE cart_id = $id;";
        const string itemsSql = """
            SELECT ci.cart_item_id, ci.product_id, p.name, p.img_name, p.price, p.discount, ci.quantity
            FROM cart_items ci
            JOIN products p ON p.product_id = ci.product_id
            WHERE ci.cart_id = $id
            ORDER BY ci.cart_item_id;
            """;

        await using var connection = await _connectionFactory.OpenConnectionAsync();
        await using (var cartCmd = connection.CreateCommand())
        {
            cartCmd.CommandText = cartSql;
            cartCmd.Parameters.AddWithValue("$id", id);
            if (Convert.ToInt32(await cartCmd.ExecuteScalarAsync()) == 0)
            {
                return null;
            }
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = itemsSql;
        cmd.Parameters.AddWithValue("$id", id);
        await using var reader = await cmd.ExecuteReaderAsync();
        var items = new List<CartItem>();
        while (await reader.ReadAsync())
        {
            items.Add(MapItem(reader));
        }

        var total = Math.Round(items.Sum(item => item.LineTotal), 2, MidpointRounding.AwayFromZero);
        return new Cart(id, items, items.Sum(item => item.Quantity), total);
    }

    public async Task<Cart> AddItemAsync(int cartId, AddCartItemRequest request)
    {
        const string sql = """
            INSERT INTO cart_items (cart_id, product_id, quantity)
            VALUES ($cartId, $productId, $quantity)
            ON CONFLICT (cart_id, product_id)
            DO UPDATE SET quantity = MIN(quantity + excluded.quantity, $maxQuantity);
            """;

        await EnsureCartAndProductExistAsync(cartId, request.ProductId);
        await using var connection = await _connectionFactory.OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$cartId", cartId);
        cmd.Parameters.AddWithValue("$productId", request.ProductId);
        cmd.Parameters.AddWithValue("$quantity", request.Quantity);
        cmd.Parameters.AddWithValue("$maxQuantity", MaxItemQuantity);
        await cmd.ExecuteNonQueryAsync();

        return await FindByIdAsync(cartId) ?? throw new NotFoundException("Cart", cartId);
    }

    public async Task<Cart> SetItemQuantityAsync(int cartId, int productId, int quantity)
    {
        const string sql = "UPDATE cart_items SET quantity = $quantity WHERE cart_id = $cartId AND product_id = $productId;";

        await using var connection = await _connectionFactory.OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$cartId", cartId);
        cmd.Parameters.AddWithValue("$productId", productId);
        cmd.Parameters.AddWithValue("$quantity", quantity);
        if (await cmd.ExecuteNonQueryAsync() == 0)
        {
            await ThrowCartOrItemNotFoundAsync(connection, cartId, productId);
        }

        return await FindByIdAsync(cartId) ?? throw new NotFoundException("Cart", cartId);
    }

    public async Task<Cart> RemoveItemAsync(int cartId, int productId)
    {
        const string sql = "DELETE FROM cart_items WHERE cart_id = $cartId AND product_id = $productId;";

        await using var connection = await _connectionFactory.OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$cartId", cartId);
        cmd.Parameters.AddWithValue("$productId", productId);
        if (await cmd.ExecuteNonQueryAsync() == 0)
        {
            await ThrowCartOrItemNotFoundAsync(connection, cartId, productId);
        }

        return await FindByIdAsync(cartId) ?? throw new NotFoundException("Cart", cartId);
    }

    private async Task EnsureCartAndProductExistAsync(int cartId, int productId)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync();
        if (!await ExistsAsync(connection, "carts", "cart_id", cartId))
        {
            throw new NotFoundException("Cart", cartId);
        }
        if (!await ExistsAsync(connection, "products", "product_id", productId))
        {
            throw new NotFoundException("Product", productId);
        }
    }

    private static async Task ThrowCartOrItemNotFoundAsync(SqliteConnection connection, int cartId, int productId)
    {
        if (!await ExistsAsync(connection, "carts", "cart_id", cartId))
        {
            throw new NotFoundException("Cart", cartId);
        }
        throw new NotFoundException("CartItem for product", productId);
    }

    // Table and column names are compile-time constants from callers, never user input.
    private static async Task<bool> ExistsAsync(SqliteConnection connection, string table, string column, int id)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {column} = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    private static CartItem MapItem(SqliteDataReader reader)
    {
        var unitPrice = reader.GetDouble(reader.GetOrdinal("price"));
        var discount = reader.IsDBNull(reader.GetOrdinal("discount")) ? 0.0 : reader.GetDouble(reader.GetOrdinal("discount"));
        var quantity = reader.GetInt32(reader.GetOrdinal("quantity"));
        var lineTotal = Math.Round(unitPrice * (1 - discount) * quantity, 2, MidpointRounding.AwayFromZero);

        return new CartItem(
            CartItemId: reader.GetInt32(reader.GetOrdinal("cart_item_id")),
            ProductId: reader.GetInt32(reader.GetOrdinal("product_id")),
            Name: reader.GetString(reader.GetOrdinal("name")),
            ImgName: reader["img_name"] as string,
            UnitPrice: unitPrice,
            Discount: discount,
            Quantity: quantity,
            LineTotal: lineTotal);
    }
}
