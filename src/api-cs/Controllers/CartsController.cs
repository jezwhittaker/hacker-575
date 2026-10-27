using Microsoft.AspNetCore.Mvc;
using OctoSupply.Api.Models;
using OctoSupply.Api.Repositories;
using OctoSupply.Api.Utils;

namespace OctoSupply.Api.Controllers;

[ApiController]
[Route("api/carts")]
public sealed class CartsController(CartsRepository repository) : ControllerBase
{
    private readonly CartsRepository _repository = repository;

    [HttpPost]
    public async Task<ActionResult<Cart>> Create()
    {
        var created = await _repository.CreateAsync();
        return CreatedAtAction(nameof(GetById), new { id = created.CartId }, created);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Cart>> GetById(int id)
    {
        var item = await _repository.FindByIdAsync(id);
        return item is null ? throw new NotFoundException("Cart", id) : Ok(item);
    }

    [HttpPost("{id:int}/items")]
    public async Task<ActionResult<Cart>> AddItem(int id, [FromBody] AddCartItemRequest request)
    {
        if (request.ProductId <= 0)
        {
            throw new ValidationException("productId is required");
        }

        ValidateQuantity(request.Quantity);
        return Ok(await _repository.AddItemAsync(id, request));
    }

    [HttpPut("{id:int}/items/{productId:int}")]
    public async Task<ActionResult<Cart>> UpdateItem(int id, int productId, [FromBody] UpdateCartItemRequest request)
    {
        ValidateQuantity(request.Quantity);
        return Ok(await _repository.SetItemQuantityAsync(id, productId, request.Quantity));
    }

    [HttpDelete("{id:int}/items/{productId:int}")]
    public async Task<ActionResult<Cart>> RemoveItem(int id, int productId) =>
        Ok(await _repository.RemoveItemAsync(id, productId));

    private static void ValidateQuantity(int quantity)
    {
        if (quantity < 1 || quantity > CartsRepository.MaxItemQuantity)
        {
            throw new ValidationException($"quantity must be between 1 and {CartsRepository.MaxItemQuantity}");
        }
    }
}
