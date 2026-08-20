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
public class AddressesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AddressesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>
    /// Get all addresses for the current customer.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List addresses", Description = "Returns all saved addresses for the current customer.")]
    [ProducesResponseType(typeof(ApiResponse<List<AddressDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAddresses(CancellationToken cancellationToken = default)
    {
        var customerId = _userManager.GetUserId(User)!;

        var addresses = await _context.Addresses
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => MapToDto(a))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<AddressDto>>.Ok(addresses));
    }

    /// <summary>
    /// Create a new address for the current customer.
    /// If this is the first address, it becomes the default automatically.
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "Create address", Description = "Creates a new saved address. The first address is automatically set as default.")]
    [ProducesResponseType(typeof(ApiResponse<AddressDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAddress(
        [FromBody] CreateAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = _userManager.GetUserId(User)!;

        var hasAddresses = await _context.Addresses
            .AnyAsync(a => a.CustomerId == customerId, cancellationToken);

        var address = new Address
        {
            CustomerId = customerId,
            FullName = request.FullName,
            Street = request.Street,
            City = request.City,
            PostalCode = request.PostalCode,
            Country = request.Country,
            Phone = request.Phone,
            IsDefault = !hasAddresses, // first address is default
            CreatedAt = DateTime.UtcNow
        };

        _context.Addresses.Add(address);
        await _context.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<AddressDto>.Ok(MapToDto(address)));
    }

    /// <summary>
    /// Update an existing address.
    /// </summary>
    [HttpPut("{id:int}")]
    [SwaggerOperation(Summary = "Update address", Description = "Updates an existing saved address. Only the owner can update.")]
    [ProducesResponseType(typeof(ApiResponse<AddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAddress(
        int id,
        [FromBody] UpdateAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = _userManager.GetUserId(User)!;

        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == id && a.CustomerId == customerId, cancellationToken);

        if (address is null)
        {
            return NotFound(ApiResponse.Fail(new ApiError
            {
                Code = "ADDRESS_NOT_FOUND",
                Message = $"Address with ID {id} was not found."
            }));
        }

        address.FullName = request.FullName;
        address.Street = request.Street;
        address.City = request.City;
        address.PostalCode = request.PostalCode;
        address.Country = request.Country;
        address.Phone = request.Phone;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<AddressDto>.Ok(MapToDto(address)));
    }

    /// <summary>
    /// Delete an existing address.
    /// </summary>
    [HttpDelete("{id:int}")]
    [SwaggerOperation(Summary = "Delete address", Description = "Deletes a saved address. Only the owner can delete.")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAddress(
        int id,
        CancellationToken cancellationToken = default)
    {
        var customerId = _userManager.GetUserId(User)!;

        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == id && a.CustomerId == customerId, cancellationToken);

        if (address is null)
        {
            return NotFound(ApiResponse.Fail(new ApiError
            {
                Code = "ADDRESS_NOT_FOUND",
                Message = $"Address with ID {id} was not found."
            }));
        }

        var wasDefault = address.IsDefault;

        _context.Addresses.Remove(address);
        await _context.SaveChangesAsync(cancellationToken);

        // If we deleted the default, promote the most recent remaining address
        if (wasDefault)
        {
            var nextDefault = await _context.Addresses
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextDefault is not null)
            {
                nextDefault.IsDefault = true;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        return Ok(ApiResponse.Ok());
    }

    /// <summary>
    /// Set an address as the default shipping address.
    /// Unsets the previous default in the same transaction.
    /// </summary>
    [HttpPut("{id:int}/default")]
    [SwaggerOperation(Summary = "Set default address", Description = "Marks the address as the default shipping address. Unsets any other default.")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefault(
        int id,
        CancellationToken cancellationToken = default)
    {
        var customerId = _userManager.GetUserId(User)!;

        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == id && a.CustomerId == customerId, cancellationToken);

        if (address is null)
        {
            return NotFound(ApiResponse.Fail(new ApiError
            {
                Code = "ADDRESS_NOT_FOUND",
                Message = $"Address with ID {id} was not found."
            }));
        }

        // Unset all other defaults for this customer
        var otherDefaults = await _context.Addresses
            .Where(a => a.CustomerId == customerId && a.Id != id && a.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var other in otherDefaults)
        {
            other.IsDefault = false;
        }

        address.IsDefault = true;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse.Ok());
    }

    private static AddressDto MapToDto(Address address) => new()
    {
        Id = address.Id,
        FullName = address.FullName,
        Street = address.Street,
        City = address.City,
        PostalCode = address.PostalCode,
        Country = address.Country,
        Phone = address.Phone,
        IsDefault = address.IsDefault,
        CreatedAt = address.CreatedAt
    };
}
