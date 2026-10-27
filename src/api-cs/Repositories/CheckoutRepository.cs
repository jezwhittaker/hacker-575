using Microsoft.Data.Sqlite;
using OctoSupply.Api.Data;
using OctoSupply.Api.Models;
using OctoSupply.Api.Utils;

namespace OctoSupply.Api.Repositories;

public sealed class CheckoutRepository(SqliteConnectionFactory connectionFactory)
{
    private readonly SqliteConnectionFactory _connectionFactory = connectionFactory;

    // Order, order details, payment and cart clearing succeed or fail together.
    public async Task<CheckoutResult> PlaceOrderAsync(
        int cartId,
        CheckoutRequest request,
        double expectedTotal,
        string currency,
        string provider,
        string paymentReference)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

        var items = await ReadCartItemsAsync(connection, transaction, cartId);
        var total = Math.Round(items.Sum(item => item.LineTotal), 2, MidpointRounding.AwayFromZero);
        if (items.Count == 0 || total != expectedTotal)
        {
            throw new ConflictException("the cart changed during checkout; review it and try again");
        }

        var branchId = await ResolveBranchIdAsync(connection, transaction);
        var orderDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var orderId = await InsertOrderAsync(connection, transaction, branchId, orderDate, cartId, request);

        foreach (var item in items)
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO order_details (order_id, product_id, quantity, unit_price)
                VALUES ($orderId, $productId, $quantity, $unitPrice);
                """;
            cmd.Parameters.AddWithValue("$orderId", orderId);
            cmd.Parameters.AddWithValue("$productId", item.ProductId);
            cmd.Parameters.AddWithValue("$quantity", item.Quantity);
            cmd.Parameters.AddWithValue("$unitPrice", item.UnitPrice);
            await cmd.ExecuteNonQueryAsync();
        }

        var cardLast4 = request.CardNumber[^4..];
        await using (var payment = connection.CreateCommand())
        {
            payment.Transaction = transaction;
            payment.CommandText = """
                INSERT INTO payments (order_id, provider, provider_reference, amount, currency, status, card_last4)
                VALUES ($orderId, $provider, $reference, $amount, $currency, 'approved', $last4);
                """;
            payment.Parameters.AddWithValue("$orderId", orderId);
            payment.Parameters.AddWithValue("$provider", provider);
            payment.Parameters.AddWithValue("$reference", paymentReference);
            payment.Parameters.AddWithValue("$amount", total);
            payment.Parameters.AddWithValue("$currency", currency);
            payment.Parameters.AddWithValue("$last4", cardLast4);
            await payment.ExecuteNonQueryAsync();
        }

        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM cart_items WHERE cart_id = $cartId;";
            clear.Parameters.AddWithValue("$cartId", cartId);
            await clear.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return new CheckoutResult(orderId, "confirmed", orderDate, items, total, currency, paymentReference, cardLast4);
    }

    private static async Task<List<CheckoutOrderItem>> ReadCartItemsAsync(
        SqliteConnection connection, SqliteTransaction transaction, int cartId)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            SELECT ci.product_id, p.name, p.price, p.discount, ci.quantity
            FROM cart_items ci
            JOIN products p ON p.product_id = ci.product_id
            WHERE ci.cart_id = $cartId
            ORDER BY ci.cart_item_id;
            """;
        cmd.Parameters.AddWithValue("$cartId", cartId);

        await using var reader = await cmd.ExecuteReaderAsync();
        var items = new List<CheckoutOrderItem>();
        while (await reader.ReadAsync())
        {
            var discount = reader.IsDBNull(3) ? 0.0 : reader.GetDouble(3);
            var unitPrice = reader.GetDouble(2) * (1 - discount);
            var quantity = reader.GetInt32(4);
            items.Add(new CheckoutOrderItem(
                reader.GetInt32(0),
                reader.GetString(1),
                quantity,
                unitPrice,
                Math.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero)));
        }
        return items;
    }

    private static async Task<int> ResolveBranchIdAsync(SqliteConnection connection, SqliteTransaction transaction)
    {
        var configured = Environment.GetEnvironmentVariable("CHECKOUT_BRANCH_ID");
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;

        if (int.TryParse(configured, out var branchId))
        {
            cmd.CommandText = "SELECT branch_id FROM branches WHERE branch_id = $id;";
            cmd.Parameters.AddWithValue("$id", branchId);
        }
        else
        {
            cmd.CommandText = "SELECT branch_id FROM branches ORDER BY branch_id LIMIT 1;";
        }

        var result = await cmd.ExecuteScalarAsync();
        return result is null
            ? throw new ConflictException("no branch is available to fulfil the order")
            : Convert.ToInt32(result);
    }

    private static async Task<int> InsertOrderAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int branchId,
        string orderDate,
        int cartId,
        CheckoutRequest request)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO orders (branch_id, order_date, name, description, status)
            VALUES ($branchId, $orderDate, $name, $description, 'confirmed');
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$branchId", branchId);
        cmd.Parameters.AddWithValue("$orderDate", orderDate);
        cmd.Parameters.AddWithValue("$name", $"Web order from cart {cartId}");
        cmd.Parameters.AddWithValue("$description", $"Customer: {request.CustomerName} <{request.Email}>");
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
