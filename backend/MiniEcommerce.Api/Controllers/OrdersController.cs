using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Exceptions;
using MiniEcommerce.Api.Interfaces;
using MiniEcommerce.Api.Models;
using MiniEcommerce.Api.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace MiniEcommerce.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPaymentService _paymentService;
    private readonly IAddressBookService _addressBook;
    private readonly IVariantStockWriter _variantStock;
    private readonly ShippingOptions _shippingOptions;

    public OrdersController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPaymentService paymentService,
        IAddressBookService addressBook,
        IVariantStockWriter variantStock,
        IOptions<ShippingOptions> shippingOptions)
    {
        _context = context;
        _userManager = userManager;
        _paymentService = paymentService;
        _addressBook = addressBook;
        _variantStock = variantStock;
        _shippingOptions = shippingOptions.Value;
    }

    /// <summary>
    /// Create an order from the current user's cart.
    /// Validates stock atomically, processes payment, and clears the cart.
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "Checkout", Description = "Creates an order from the current user's cart. Atomically deducts stock (ADR 0002), charges via IPaymentService, and clears the cart. On payment failure stock is restored and 400 PAYMENT_FAILED is returned.")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = _userManager.GetUserId(User)!;
        var shippingFee = _shippingOptions.Fee;

        // Load the cart with items and variants
        var cart = await _context.Carts
            .Include(c => c.Items)
            .ThenInclude(ci => ci.ProductVariant)
            .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

        if (cart is null || cart.Items.Count == 0)
        {
            return BadRequest(ApiResponse.Fail(new ApiError
            {
                Code = "EMPTY_CART",
                Message = "Your cart is empty. Add items before checking out."
            }));
        }

        // ── Address snapshot (ADR 0004, Task 26e) ──────────────────────────
        // If an addressId is provided, load the saved address and snapshot it.
        // Otherwise, use the flat fields from the request body.
        string shippingFullName, shippingStreet, shippingCity, shippingPostalCode, shippingCountry, shippingPhone;

        if (request.AddressId.HasValue)
        {
            var address = await _context.Addresses
                .FirstOrDefaultAsync(a => a.Id == request.AddressId.Value && a.CustomerId == customerId, cancellationToken);

            if (address is null)
            {
                return NotFound(ApiResponse.Fail(new ApiError
                {
                    Code = "ADDRESS_NOT_FOUND",
                    Message = $"Address with ID {request.AddressId} was not found."
                }));
            }

            shippingFullName = address.FullName;
            shippingStreet = address.Street;
            shippingCity = address.City;
            shippingPostalCode = address.PostalCode;
            shippingCountry = address.Country;
            shippingPhone = address.Phone;
        }
        else
        {
            shippingFullName = request.FullName;
            shippingStreet = request.Street;
            shippingCity = request.City;
            shippingPostalCode = request.PostalCode;
            shippingCountry = request.Country;
            shippingPhone = request.Phone;
        }

        // Calculate totals (before stock deduction so Amount is known)
        var subtotal = cart.Items.Sum(i => i.UnitPrice * i.Quantity);
        var total = subtotal + shippingFee;

        // ── Atomic stock deduction (ADR 0002, per-variant ADR 0003) ────────
        // Each cart item: TryDeductAsync — atomic UPDATE on Postgres,
        // tracked-entity decrement on the InMemory provider used in tests.
        // On failure, restock everything already deducted so a partial
        // checkout never leaves phantom deductions behind.
        var deducted = new List<CartItem>();

        foreach (var item in cart.Items)
        {
            var ok = await _variantStock.TryDeductAsync(item.ProductVariantId, item.Quantity, cancellationToken);
            if (!ok)
            {
                foreach (var prev in deducted)
                {
                    await _variantStock.RestockAsync(prev.ProductVariantId, prev.Quantity, cancellationToken);
                }

                // Refresh tracked stock so subsequent error messages are accurate
                var name = item.ProductVariant.Product.Name;
                var available = item.ProductVariant.Stock;
                var msg = available > 0
                    ? $"Only {available} units of \"{name}\" are available, but you have {item.Quantity} in your cart."
                    : $"Insufficient stock for \"{name}\" (requested {item.Quantity}).";

                return BadRequest(ApiResponse.Fail(new ApiError
                {
                    Code = "INSUFFICIENT_STOCK",
                    Message = msg
                }));
            }

            deducted.Add(item);
        }

        // Process payment (external IO — not inside a DB transaction on purpose)
        var paymentResult = await _paymentService.ChargeAsync(new PaymentRequest
        {
            OrderId = Guid.NewGuid(),
            Amount = total,
            Currency = "USD"
        }, cancellationToken);

        if (!paymentResult.Success)
        {
            // Explicit restock loop (ADR 0002 consequence): atomic UPDATE back
            foreach (var item in deducted)
            {
                await _variantStock.RestockAsync(item.ProductVariantId, item.Quantity, cancellationToken);
            }

            // InMemory provider: tracked mutations were already saved by
            // TryDeductAsync; nothing more to do. Discard the pending order
            // by not calling SaveChanges further. (For Postgres, the above
            // RestockAsync issued an UPDATE per item.)
            return BadRequest(ApiResponse.Fail(new ApiError
            {
                Code = "PAYMENT_FAILED",
                Message = paymentResult.Message ?? "Payment processing failed."
            }));
        }

        // Payment succeeded — persist order + clear cart. For relational
        // provider the stock updates above are already committed via
        // ExecuteSql; for InMemory the tracked Stock mutations are saved here.
        var order = new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.Paid,
            ShippingFullName = shippingFullName,
            ShippingStreet = shippingStreet,
            ShippingCity = shippingCity,
            ShippingPostalCode = shippingPostalCode,
            ShippingCountry = shippingCountry,
            ShippingPhone = shippingPhone,
            Subtotal = subtotal,
            ShippingFee = shippingFee,
            Total = total,
            CreatedAt = DateTime.UtcNow,
            Items = cart.Items.Select(ci => new OrderItem
            {
                ProductVariantId = ci.ProductVariantId,
                ProductName = OrderItemNameFormatter.Format(ci.ProductVariant.Product, ci.ProductVariant),
                // ADR 0008: snapshot variant attributes onto the OrderItem row
                // so OrderHistory/OrderConfirmation can render them without
                // parsing the ProductName string.
                Size = ci.ProductVariant.Size,
                Color = ci.ProductVariant.Color,
                UnitPrice = ci.UnitPrice,
                Quantity = ci.Quantity
            }).ToList()
        };

        _context.Orders.Add(order);
        _context.CartItems.RemoveRange(cart.Items);
        cart.Items.Clear();

        await _context.SaveChangesAsync(cancellationToken);

        // ADR 0004 'Save this address' checkbox — delegated to the
        // single source of truth for the single-default invariant.
        if (request.SaveAddress && !request.AddressId.HasValue)
        {
            await _addressBook.CreateForCustomerAsync(
                customerId,
                new AddressData(
                    FullName: shippingFullName,
                    Street: shippingStreet,
                    City: shippingCity,
                    PostalCode: shippingPostalCode,
                    Country: shippingCountry,
                    Phone: shippingPhone),
                cancellationToken);
        }

        var dto = MapOrderToDto(order);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<OrderDto>.Ok(dto));
    }

    /// <summary>
    /// Get a paginated list of orders for the current user, newest first.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List my orders", Description = "Returns the current user's orders, newest first, paginated. 401 if not authenticated.")]
    [ProducesResponseType(typeof(ApiResponse<List<OrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        // Clamp to sane bounds to prevent unbounded queries.
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var customerId = _userManager.GetUserId(User)!;

        var query = _context.Orders
            .Where(o => o.CustomerId == customerId);

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = orders.Select(MapOrderToDto).ToList();

        var meta = new Meta
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return Ok(ApiResponse<List<OrderDto>>.Ok(dtos, meta));
    }

    /// <summary>
    /// Get a specific order by ID. Only returns orders belonging to the current user.
    /// </summary>
    [HttpGet("{id:int}")]
    [SwaggerOperation(Summary = "Get order by id", Description = "Returns the order with items if it belongs to the current user. 404 otherwise.")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderById(
        int id,
        CancellationToken cancellationToken = default)
    {
        var customerId = _userManager.GetUserId(User)!;

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == customerId, cancellationToken);

        if (order is null)
        {
            return NotFound(ApiResponse.Fail(new ApiError
            {
                Code = "ORDER_NOT_FOUND",
                Message = $"Order with ID {id} was not found."
            }));
        }

        var dto = MapOrderToDto(order);
        return Ok(ApiResponse<OrderDto>.Ok(dto));
    }

    // ─────────────── Private helpers ───────────────

    private static OrderDto MapOrderToDto(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            Status = order.Status.ToString(),
            Subtotal = order.Subtotal,
            ShippingFee = order.ShippingFee,
            Total = order.Total,
            ShippingFullName = order.ShippingFullName,
            ShippingStreet = order.ShippingStreet,
            ShippingCity = order.ShippingCity,
            ShippingPostalCode = order.ShippingPostalCode,
            ShippingCountry = order.ShippingCountry,
            ShippingPhone = order.ShippingPhone,
            CreatedAt = order.CreatedAt,
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductVariantId = i.ProductVariantId,
                ProductName = i.ProductName,
                // ADR 0008: split fields for programmatic variant display.
                // Snapshotted on the OrderItem row at checkout; null when the
                // variant had no Size/Color attribute.
                Size = i.Size,
                Color = i.Color,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity
            }).ToList()
        };
    }
}
