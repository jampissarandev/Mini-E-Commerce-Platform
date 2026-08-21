using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Models;
using Swashbuckle.AspNetCore.Annotations;

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
    [SwaggerOperation(Summary = "Get cart", Description = "Returns the current user's cart with all items. Creates an empty cart on first call (idempotent).")]
    [ProducesResponseType(typeof(ApiResponse<CartDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCart(CancellationToken cancellationToken = default)
    {
        var customerId = _userManager.GetUserId(User)!;
        var cart = await GetOrCreateCartAsync(customerId, cancellationToken);

        var dto = await MapCartToDtoAsync(cart, cancellationToken);
        return Ok(ApiResponse<CartDto>.Ok(dto));
    }

    /// <summary>
    /// Add an item to the cart. If the variant is already in the cart, the quantity is increased.
    /// </summary>
    [HttpPost("items")]
    [SwaggerOperation(Summary = "Add item to cart", Description = "Adds a product variant to the cart or increases quantity if already present. 400 INSUFFICIENT_STOCK if quantity exceeds stock.")]
    [ProducesResponseType(typeof(ApiResponse<CartItemDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CartItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddItem(
        [FromBody] AddCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = _userManager.GetUserId(User)!;
        var cart = await GetOrCreateCartAsync(customerId, cancellationToken);

        // Validate variant exists
        var variant = await _context.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == request.ProductVariantId, cancellationToken);

        if (variant is null)
        {
            return NotFound(ApiResponse.Fail(new ApiError
            {
                Code = "VARIANT_NOT_FOUND",
                Message = $"Product variant with ID {request.ProductVariantId} was not found."
            }));
        }

        if (!variant.IsActive || !variant.Product.IsActive)
        {
            return NotFound(ApiResponse.Fail(new ApiError
            {
                Code = "VARIANT_NOT_FOUND",
                Message = $"Product variant with ID {request.ProductVariantId} is no longer available."
            }));
        }

        // Check if item already exists in cart
        var existingItem = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.ProductVariantId == request.ProductVariantId, cancellationToken);

        if (existingItem is not null)
        {
            var newQuantity = existingItem.Quantity + request.Quantity;

            if (newQuantity > variant.Stock)
            {
                return BadRequest(ApiResponse.Fail(new ApiError
                {
                    Code = "INSUFFICIENT_STOCK",
                    Message = $"Only {variant.Stock} units of \"{variant.Product.Name}\" are available. You already have {existingItem.Quantity} in your cart."
                }));
            }

            existingItem.Quantity = newQuantity;
            // UnitPrice is a snapshot at add-time (CONTEXT.md). The price
            // re-validation happens at checkout, not on re-add.
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            var updatedDto = MapCartItemToDto(existingItem, variant);
            return Ok(ApiResponse<CartItemDto>.Ok(updatedDto));
        }

        if (request.Quantity > variant.Stock)
        {
            return BadRequest(ApiResponse.Fail(new ApiError
            {
                Code = "INSUFFICIENT_STOCK",
                Message = $"Only {variant.Stock} units of \"{variant.Product.Name}\" are available."
            }));
        }

        var cartItem = new CartItem
        {
            CartId = cart.Id,
            ProductVariantId = variant.Id,
            Quantity = request.Quantity,
            UnitPrice = variant.Product.Price
        };

        _context.CartItems.Add(cartItem);
        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var dto = MapCartItemToDto(cartItem, variant);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CartItemDto>.Ok(dto));
    }

    /// <summary>
    /// Update the quantity of a cart item.
    /// </summary>
    [HttpPut("items/{id:int}")]
    [SwaggerOperation(Summary = "Update cart item quantity", Description = "Updates quantity; 400 INSUFFICIENT_STOCK if exceeds stock. 404 if item not in cart.")]
    [ProducesResponseType(typeof(ApiResponse<CartItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateItem(
        int id,
        [FromBody] UpdateCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = _userManager.GetUserId(User)!;
        var cart = await GetOrCreateCartAsync(customerId, cancellationToken);

        var cartItem = await _context.CartItems
            .Include(ci => ci.ProductVariant)
            .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(ci => ci.Id == id && ci.CartId == cart.Id, cancellationToken);

        if (cartItem is null)
        {
            return NotFound(ApiResponse.Fail(new ApiError
            {
                Code = "CART_ITEM_NOT_FOUND",
                Message = $"Cart item with ID {id} was not found."
            }));
        }

        if (request.Quantity > cartItem.ProductVariant.Stock)
        {
            return BadRequest(ApiResponse.Fail(new ApiError
            {
                Code = "INSUFFICIENT_STOCK",
                Message = $"Only {cartItem.ProductVariant.Stock} units of \"{cartItem.ProductVariant.Product.Name}\" are available."
            }));
        }

        cartItem.Quantity = request.Quantity;
        // UnitPrice is a snapshot at add-time (CONTEXT.md). Do not recompute
        // on PUT — checkout re-validates against the live price.
        cart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var dto = MapCartItemToDto(cartItem, cartItem.ProductVariant);
        return Ok(ApiResponse<CartItemDto>.Ok(dto));
    }

    /// <summary>
    /// Remove an item from the cart.
    /// </summary>
    [HttpDelete("items/{id:int}")]
    [SwaggerOperation(Summary = "Remove cart item", Description = "Removes a single line item from the cart. 404 if item not in cart.")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItem(
        int id,
        CancellationToken cancellationToken = default)
    {
        var customerId = _userManager.GetUserId(User)!;
        var cart = await GetOrCreateCartAsync(customerId, cancellationToken);

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
    [SwaggerOperation(Summary = "Clear cart", Description = "Removes all items from the current user's cart. Idempotent — clearing an empty cart returns 200.")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ClearCart(CancellationToken cancellationToken = default)
    {
        var customerId = _userManager.GetUserId(User)!;
        var cart = await GetOrCreateCartAsync(customerId, cancellationToken);

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

    private async Task<Cart> GetOrCreateCartAsync(string customerId, CancellationToken cancellationToken)
    {
        var cart = await _context.Carts
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

        if (cart is null)
        {
            cart = new Cart
            {
                CustomerId = customerId,
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
            .Include(ci => ci.ProductVariant)
            .ThenInclude(v => v.Product)
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
                ProductId = ci.ProductVariant.Product.Id,
                ProductVariantId = ci.ProductVariantId,
                ProductName = ci.ProductVariant.Product.Name,
                ProductSlug = ci.ProductVariant.Product.Slug,
                Size = ci.ProductVariant.Size,
                Color = ci.ProductVariant.Color,
                ImageUrl = ci.ProductVariant.Product.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault() ?? string.Empty,
                UnitPrice = ci.UnitPrice,
                Quantity = ci.Quantity
            }).ToList()
        };
    }

    private static CartItemDto MapCartItemToDto(CartItem cartItem, ProductVariant variant)
    {
        return new CartItemDto
        {
            Id = cartItem.Id,
            ProductId = variant.Product.Id,
            ProductVariantId = cartItem.ProductVariantId,
            ProductName = variant.Product.Name,
            ProductSlug = variant.Product.Slug,
            Size = variant.Size,
            Color = variant.Color,
            ImageUrl = variant.Product.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault() ?? string.Empty,
            UnitPrice = cartItem.UnitPrice,
            Quantity = cartItem.Quantity
        };
    }
}
