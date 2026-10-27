using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OctoSupply.Api.Data;
using OctoSupply.Api.Models;
using OctoSupply.Api.Repositories;
using OctoSupply.Api.Services;
using OctoSupply.Api.Services.Payments;
using OctoSupply.Api.Utils;
using Xunit;

namespace OctoSupply.Api.Tests.Services;

public sealed class CheckoutServiceTests
{
    private const string ApprovedCard = "4242424242424242";

    [Fact]
    public async Task Checkout_WithApprovedCard_CreatesOrderAndClearsCart()
    {
        await WithServiceAsync(async (service, carts, factory) =>
        {
            var cart = await FillCartAsync(carts);

            var result = await service.CheckoutAsync(cart.CartId, Request(ApprovedCard));

            Assert.Equal("confirmed", result.Status);
            Assert.Equal(cart.Total, result.Total);
            Assert.Equal("4242", result.CardLast4);
            Assert.Equal(2, result.Items.Count);
            Assert.Equal(1, await CountAsync(factory, "orders"));
            Assert.Equal(2, await CountAsync(factory, "order_details"));
            Assert.Equal(1, await CountAsync(factory, "payments"));
            Assert.Empty((await carts.FindByIdAsync(cart.CartId))!.Items);
        });
    }

    [Theory]
    [InlineData("4000000000000002", "declined")]
    [InlineData("4000000000009995", "insufficient funds")]
    [InlineData("4242424242424241", "invalid")]
    public async Task Checkout_WhenPaymentFails_ThrowsAndLeavesCartIntact(string card, string expectedReason)
    {
        await WithServiceAsync(async (service, carts, factory) =>
        {
            var cart = await FillCartAsync(carts);

            var ex = await Assert.ThrowsAsync<PaymentDeclinedException>(() =>
                service.CheckoutAsync(cart.CartId, Request(card)));

            Assert.Equal(402, ex.StatusCode);
            Assert.Contains(expectedReason, ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, await CountAsync(factory, "orders"));
            Assert.Equal(0, await CountAsync(factory, "payments"));
            var unchanged = await carts.FindByIdAsync(cart.CartId);
            Assert.Equal(cart.Total, unchanged!.Total);
            Assert.Equal(2, unchanged.Items.Count);
        });
    }

    [Fact]
    public async Task Checkout_WithEmptyOrUnknownCart_Fails()
    {
        await WithServiceAsync(async (service, carts, _) =>
        {
            var empty = await carts.CreateAsync();

            await Assert.ThrowsAsync<ValidationException>(() => service.CheckoutAsync(empty.CartId, Request(ApprovedCard)));
            await Assert.ThrowsAsync<NotFoundException>(() => service.CheckoutAsync(9999, Request(ApprovedCard)));
        });
    }

    [Fact]
    public async Task Checkout_WhenCardIsExpired_IsDeclined()
    {
        await WithServiceAsync(async (service, carts, _) =>
        {
            var cart = await FillCartAsync(carts);
            var request = Request(ApprovedCard);
            request.Expiry = "01/20";

            var ex = await Assert.ThrowsAsync<PaymentDeclinedException>(() => service.CheckoutAsync(cart.CartId, request));
            Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Checkout_WhenOrderCannotBePlaced_VoidsTheCharge()
    {
        await WithServiceAsync(async (_, carts, factory) =>
        {
            var cart = await FillCartAsync(carts);
            var provider = new CartEmptyingProvider(carts, cart.CartId);
            var service = new CheckoutService(
                carts,
                new CheckoutRepository(factory),
                provider,
                new PaymentOptions("simulated", null, "USD"),
                NullLogger<CheckoutService>.Instance);

            await Assert.ThrowsAsync<ConflictException>(() => service.CheckoutAsync(cart.CartId, Request(ApprovedCard)));

            Assert.Single(provider.Voided);
            Assert.Equal(0, await CountAsync(factory, "orders"));
            Assert.Equal(0, await CountAsync(factory, "payments"));
        });
    }

    // Approves the charge, but the cart changes while the payment is in flight.
    private sealed class CartEmptyingProvider(CartsRepository carts, int cartId) : IPaymentProvider
    {
        public List<string> Voided { get; } = [];
        public string Name => "simulated";

        public async Task<PaymentOutcome> ChargeAsync(PaymentCharge charge)
        {
            await carts.RemoveItemAsync(cartId, 1);
            return new PaymentOutcome(true, "sim_test", null);
        }

        public Task VoidAsync(string reference)
        {
            Voided.Add(reference);
            return Task.CompletedTask;
        }
    }

    private static CheckoutRequest Request(string card) => new()
    {
        CustomerName = "Alice",
        Email = "alice@example.com",
        CardNumber = card,
        Expiry = $"12/{(DateTime.UtcNow.Year + 2) % 100:00}",
        Cvc = "123"
    };

    private static async Task<Cart> FillCartAsync(CartsRepository carts)
    {
        var cart = await carts.CreateAsync();
        await carts.AddItemAsync(cart.CartId, new AddCartItemRequest { ProductId = 1, Quantity = 2 });
        return await carts.AddItemAsync(cart.CartId, new AddCartItemRequest { ProductId = 2, Quantity = 1 });
    }

    private static async Task<long> CountAsync(SqliteConnectionFactory factory, string table)
    {
        await using var connection = await factory.OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private static async Task WithServiceAsync(Func<CheckoutService, CartsRepository, SqliteConnectionFactory, Task> test)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"octosupply-tests-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("DB_FILE", dbPath);

        try
        {
            var factory = new SqliteConnectionFactory(new NullLogger<SqliteConnectionFactory>());
            await EnsureSchemaAsync(factory);
            var carts = new CartsRepository(factory);
            var service = new CheckoutService(
                carts,
                new CheckoutRepository(factory),
                new SimulatedPaymentProvider(),
                new PaymentOptions("simulated", null, "USD"),
                NullLogger<CheckoutService>.Instance);
            await test(service, carts, factory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DB_FILE", null);
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    private static async Task EnsureSchemaAsync(SqliteConnectionFactory connectionFactory)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE products (
                product_id INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                price REAL NOT NULL,
                img_name TEXT,
                discount REAL DEFAULT 0.0
            );
            INSERT INTO products (product_id, name, price, discount) VALUES (1, 'Plain', 10.0, 0.0);
            INSERT INTO products (product_id, name, price, discount) VALUES (2, 'Discounted', 20.0, 0.25);
            CREATE TABLE branches (branch_id INTEGER PRIMARY KEY, name TEXT NOT NULL);
            INSERT INTO branches (branch_id, name) VALUES (1, 'Main');
            CREATE TABLE orders (
                order_id INTEGER PRIMARY KEY,
                branch_id INTEGER NOT NULL,
                order_date TEXT NOT NULL,
                name TEXT NOT NULL,
                description TEXT,
                status TEXT NOT NULL DEFAULT 'pending'
            );
            CREATE TABLE order_details (
                order_detail_id INTEGER PRIMARY KEY,
                order_id INTEGER NOT NULL,
                product_id INTEGER NOT NULL,
                quantity INTEGER NOT NULL,
                unit_price REAL NOT NULL,
                notes TEXT
            );
            CREATE TABLE payments (
                payment_id INTEGER PRIMARY KEY,
                order_id INTEGER NOT NULL,
                provider TEXT NOT NULL,
                provider_reference TEXT NOT NULL,
                amount REAL NOT NULL CHECK (amount > 0),
                currency TEXT NOT NULL,
                status TEXT NOT NULL,
                card_last4 TEXT NOT NULL,
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            CREATE TABLE carts (
                cart_id INTEGER PRIMARY KEY,
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            CREATE TABLE cart_items (
                cart_item_id INTEGER PRIMARY KEY,
                cart_id INTEGER NOT NULL,
                product_id INTEGER NOT NULL,
                quantity INTEGER NOT NULL CHECK (quantity > 0),
                UNIQUE (cart_id, product_id)
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
