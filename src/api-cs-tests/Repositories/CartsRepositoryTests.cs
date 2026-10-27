using Microsoft.Extensions.Logging.Abstractions;
using OctoSupply.Api.Data;
using OctoSupply.Api.Models;
using OctoSupply.Api.Repositories;
using OctoSupply.Api.Utils;
using Xunit;

namespace OctoSupply.Api.Tests.Repositories;

public sealed class CartsRepositoryTests
{
    [Fact]
    public async Task AddUpdateRemoveItems_KeepsTotalAccurate()
    {
        await WithRepositoryAsync(async repository =>
        {
            var cart = await repository.CreateAsync();
            Assert.Empty(cart.Items);
            Assert.Equal(0, cart.Total);

            await repository.AddItemAsync(cart.CartId, new AddCartItemRequest { ProductId = 1, Quantity = 2 });
            cart = await repository.AddItemAsync(cart.CartId, new AddCartItemRequest { ProductId = 1, Quantity = 1 });
            Assert.Single(cart.Items);
            Assert.Equal(3, cart.Items[0].Quantity);
            Assert.Equal(30.0, cart.Total);

            cart = await repository.AddItemAsync(cart.CartId, new AddCartItemRequest { ProductId = 2, Quantity = 1 });
            Assert.Equal(2, cart.Items.Count);
            Assert.Equal(4, cart.ItemCount);
            Assert.Equal(45.0, cart.Total); // 3 x 10.00 + 1 x 20.00 with 25% discount

            cart = await repository.SetItemQuantityAsync(cart.CartId, 1, 5);
            Assert.Equal(65.0, cart.Total);

            cart = await repository.RemoveItemAsync(cart.CartId, 1);
            Assert.Single(cart.Items);
            Assert.Equal(15.0, cart.Total);

            var persisted = await repository.FindByIdAsync(cart.CartId);
            Assert.Equal(15.0, persisted!.Total);
        });
    }

    [Fact]
    public async Task AddItem_ClampsQuantityToMaximum()
    {
        await WithRepositoryAsync(async repository =>
        {
            var cart = await repository.CreateAsync();
            await repository.AddItemAsync(cart.CartId, new AddCartItemRequest { ProductId = 1, Quantity = 90 });
            cart = await repository.AddItemAsync(cart.CartId, new AddCartItemRequest { ProductId = 1, Quantity = 90 });

            Assert.Equal(CartsRepository.MaxItemQuantity, cart.Items[0].Quantity);
        });
    }

    [Fact]
    public async Task UnknownCartProductOrItem_ThrowsNotFound()
    {
        await WithRepositoryAsync(async repository =>
        {
            var cart = await repository.CreateAsync();

            await Assert.ThrowsAsync<NotFoundException>(() =>
                repository.AddItemAsync(9999, new AddCartItemRequest { ProductId = 1, Quantity = 1 }));
            await Assert.ThrowsAsync<NotFoundException>(() =>
                repository.AddItemAsync(cart.CartId, new AddCartItemRequest { ProductId = 9999, Quantity = 1 }));
            await Assert.ThrowsAsync<NotFoundException>(() =>
                repository.SetItemQuantityAsync(cart.CartId, 1, 2));
            await Assert.ThrowsAsync<NotFoundException>(() =>
                repository.RemoveItemAsync(cart.CartId, 1));
            Assert.Null(await repository.FindByIdAsync(9999));
        });
    }

    private static async Task WithRepositoryAsync(Func<CartsRepository, Task> test)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"octosupply-tests-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("DB_FILE", dbPath);

        try
        {
            var factory = new SqliteConnectionFactory(new NullLogger<SqliteConnectionFactory>());
            await EnsureSchemaAsync(factory);
            await test(new CartsRepository(factory));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DB_FILE", null);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
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
            CREATE TABLE carts (
                cart_id INTEGER PRIMARY KEY,
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            CREATE TABLE cart_items (
                cart_item_id INTEGER PRIMARY KEY,
                cart_id INTEGER NOT NULL,
                product_id INTEGER NOT NULL,
                quantity INTEGER NOT NULL CHECK (quantity > 0),
                UNIQUE (cart_id, product_id),
                FOREIGN KEY (cart_id) REFERENCES carts(cart_id) ON DELETE CASCADE,
                FOREIGN KEY (product_id) REFERENCES products(product_id) ON DELETE CASCADE
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
