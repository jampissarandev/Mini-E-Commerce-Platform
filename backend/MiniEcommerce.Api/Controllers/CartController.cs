using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Models;

namespace MiniEcommerce.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public CartController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>
    /// Get the current user's cart with all items.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCart(CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(User)!;
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);

        var dto = await MapCartToDtoAsync(cart, cancellationToken);
        return Ok(ApiResponse<CartDto>.Ok(dto));
    }

    /// <summary>
    /// Add an item to the cart. If the product is already in the cart, the quantity is increased.
    /// </summary>
    [HttpPost("items")]
    [ProducesResponseType(typeof(ApiResponse<CartItemDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CartItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddItem(
        [FromBody] AddCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(User)!;
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);

        // Validate product exists
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
        {
            return NotFound(ApiResponse.Fail(new ApiError
            {
                Code = "PRODUCT_NOT_FOUND",
                Message = $"Product with ID {request.ProductId} was not found."
            }));
        }

        if (!product.IsActive)
        {
            return NotFound(ApiResponse.Fail(new ApiError
            {
                Code = "PRODUCT_NOT_FOUND",
                Message = $"Product with ID {request.ProductId} is no longer available."
            }));
        }

        // Check if item already exists in cart
        var existingItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.ProductId == request.ProductId, cancellationToken);

        if (existingItem is not null)
        {
            var newQuantity = existingItem.Quantity + request.Quantity;

            if (newQuantity > product.Stock)
            {
                return BadRequest(ApiResponse.Fail(new ApiError
                {
                    Code = "INSUFFICIENT_STOCK",
                    Message = $"Only {product.Stock} units of \"{product.Name}\" are available. You already have {existingItem.Quantity} in your cart."
                }));
            }

            existingItem.Quantity = newQuantity;
            existingItem.UnitPrice = product.Price;
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            var updatedDto = MapCartItemToDto(existingItem, product);
            return Ok(ApiResponse<CartItemDto>.Ok(updatedDto));
        }

        if (request.Quantity > product.Stock)
        {
            return BadRequest(ApiResponse.Fail(new ApiError
            {
                Code = "INSUFFICIENT_STOCK",
                Message = $"Only {product.Stock} units of \"{product.Name}\" are available."
            }));
        }

        var cartItem = new CartItem
        {
            CartId = cart.Id,
            ProductId = product.Id,
            Quantity = request.Quantity,
            UnitPrice = product.Price
        };

        _context.CartItems.Add(cartItem);
        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var dto = MapCartItemToDto(cartItem, product);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CartItemDto>.Ok(dto));
    }

    /// <summary>
    /// Update the quantity of a cart item.
    /// </summary>
    [HttpPut("items/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CartItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateItem(
        int id,
        [FromBody] UpdateCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(User)!;
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);

        var cartItem = await _context.CartItems
            .Include(ci => ci.Product)
            .FirstOrDefaultAsync(ci => ci.Id == id && ci.CartId == cart.Id, cancellationToken);

        if (cartItem is null)
        {
            return NotFound(ApiResponse.Fail(new ApiError
            {
                Code = "CART_ITEM_NOT_FOUND",
                Message = $"Cart item with ID {id} was not found."
            }));
        }

        if (request.Quantity > cartItem.Product.Stock)
        {
            return BadRequest(ApiResponse.Fail(new ApiError
            {
                Code = "INSUFFICIENT_STOCK",
                Message = $"Only {cartItem.Product.Stock} units of \"{cartItem.Product.Name}\" are available."
            }));
        }

        cartItem.Quantity = request.Quantity;
        cartItem.UnitPrice = cartItem.Product.Price;
        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var dto = MapCartItemToDto(cartItem, cartItem.Product);
        return Ok(ApiResponse<CartItemDto>.Ok(dto));
    }

    /// <summary>
    /// Remove an item from the cart.
    /// </summary>
    [HttpDelete("items/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItem(
        int id,
        CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(User)!;
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);

        var cartItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.Id == id && ci.CartId == cart.Id, cancellationToken);

        if (cartItem is null)
        {
            return NotFound(ApiResponse.Fail(new ApiError
            {
                Code = "CART_ITEM_NOT_FOUND",
                Message = $"Cart item with ID {id} was not found."
            }));
        }

        _context.CartItems.Remove(cartItem);
        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse.Ok());
    }

    /// <summary>
    /// Clear all items from the cart.
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ClearCart(CancellationToken cancellationToken = default)
    {
        var userId = _userManager.GetUserId(User)!;
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);

        var items = await _context.CartItems
            .Where(ci => ci.CartId == cart.Id)
            .ToListAsync(cancellationToken);

        if (items.Count > 0)
        {
            _context.CartItems.RemoveRange(items);
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Ok(ApiResponse.Ok());
    }

    // ─────────────── Private helpers ───────────────

    private async Task<Cart> GetOrCreateCartAsync(string userId, CancellationToken cancellationToken)
    {
        var cart = await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart is null)
        {
            cart = new Cart
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Carts.Add(cart);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return cart;
    }

    private async Task<CartDto> MapCartToDtoAsync(Cart cart, CancellationToken cancellationToken)
    {
        var items = await _context.CartItems
            .Where(ci => ci.CartId == cart.Id)
            .Include(ci => ci.Product)
            .ThenInclude(p => p.Images.OrderBy(i => i.SortOrder).Take(1))
            .ToListAsync(cancellationToken);

        return new CartDto
        {
            Id = cart.Id,
            CreatedAt = cart.CreatedAt,
            UpdatedAt = cart.UpdatedAt,
            Items = items.Select(ci => new CartItemDto
            {
                Id = ci.Id,
                ProductId = ci.ProductId,
                ProductName = ci.Product.Name,
                ProductSlug = ci.Product.Slug,
                ImageUrl = ci.Product.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault() ?? string.Empty,
                UnitPrice = ci.UnitPrice,
                Quantity = ci.Quantity
            }).ToList()
        };
    }

    private static CartItemDto MapCartItemToDto(CartItem cartItem, Product product)
    {
        return new CartItemDto
        {
            Id = cartItem.Id,
            ProductId = cartItem.ProductId,
            ProductName = product.Name,
            ProductSlug = product.Slug,
            ImageUrl = product.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault() ?? string.Empty,
            UnitPrice = cartItem.UnitPrice,
            Quantity = cartItem.Quantity
        };
    }
}
