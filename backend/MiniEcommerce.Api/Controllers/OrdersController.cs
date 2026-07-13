using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniEcommerce.Api.Data;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Exceptions;
using MiniEcommerce.Api.Interfaces;
using MiniEcommerce.Api.Models;

namespace MiniEcommerce.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPaymentService _paymentService;

    private const decimal ShippingFee = 5.99m;

    public OrdersController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPaymentService paymentService)
    {
        _context = context;
        _userManager = userManager;
        _paymentService = paymentService;
    }

    /// <summary>
    /// Create an order from the current user's cart.
    /// Validates stock, processes payment, deducts stock, and clears the cart.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = _userManager.GetUserId(User)!;

        // Load the cart with items and products
        var cart = await _context.Carts
            .Include(c => c.Items)
            .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

        if (cart is null || cart.Items.Count == 0)
        {
            return BadRequest(ApiResponse.Fail(new ApiError
            {
                Code = "EMPTY_CART",
                Message = "Your cart is empty. Add items before checking out."
            }));
        }

        // Re-validate stock for each item
        var stockErrors = new List<string>();
        foreach (var item in cart.Items)
        {
            if (item.Quantity > item.Product.Stock)
            {
                stockErrors.Add(
                    $"Only {item.Product.Stock} units of \"{item.Product.Name}\" are available, but you have {item.Quantity} in your cart.");
            }
        }

        if (stockErrors.Count > 0)
        {
            return BadRequest(ApiResponse.Fail(new ApiError
            {
                Code = "INSUFFICIENT_STOCK",
                Message = string.Join(" ", stockErrors)
            }));
        }

        // Calculate totals
        var subtotal = cart.Items.Sum(i => i.UnitPrice * i.Quantity);
        var total = subtotal + ShippingFee;

        // Create the order
        var order = new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            ShippingFullName = request.FullName,
            ShippingStreet = request.Street,
            ShippingCity = request.City,
            ShippingPostalCode = request.PostalCode,
            ShippingCountry = request.Country,
            ShippingPhone = request.Phone,
            Subtotal = subtotal,
            ShippingFee = ShippingFee,
            Total = total,
            CreatedAt = DateTime.UtcNow,
            Items = cart.Items.Select(ci => new OrderItem
            {
                ProductId = ci.ProductId,
                ProductName = ci.Product.Name,
                UnitPrice = ci.UnitPrice,
                Quantity = ci.Quantity
            }).ToList()
        };

        _context.Orders.Add(order);

        // Deduct stock
        foreach (var item in cart.Items)
        {
            item.Product.Stock -= item.Quantity;
        }

        // Process payment
        var paymentResult = await _paymentService.ChargeAsync(new PaymentRequest
        {
            OrderId = Guid.NewGuid(),
            Amount = total,
            Currency = "USD"
        }, cancellationToken);

        if (!paymentResult.Success)
        {
            return BadRequest(ApiResponse.Fail(new ApiError
            {
                Code = "PAYMENT_FAILED",
                Message = paymentResult.Message ?? "Payment processing failed."
            }));
        }

        // Mark order as paid
        order.Status = OrderStatus.Paid;

        // Clear the cart
        _context.CartItems.RemoveRange(cart.Items);
        cart.Items.Clear();

        await _context.SaveChangesAsync(cancellationToken);

        var dto = MapOrderToDto(order);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<OrderDto>.Ok(dto));
    }

    /// <summary>
    /// Get a paginated list of orders for the current user, newest first.
    /// </summary>
    [HttpGet]
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
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity
            }).ToList()
        };
    }
}
