using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace MiniEcommerce.Api.Dtos;

[SwaggerSchema("A saved shipping address in the customer's address book.")]
public record AddressDto
{
    [SwaggerSchema("Address id.")]
    public int Id { get; init; }

    [SwaggerSchema("Recipient full name.")]
    public string FullName { get; init; } = string.Empty;

    [SwaggerSchema("Street address.")]
    public string Street { get; init; } = string.Empty;

    [SwaggerSchema("City.")]
    public string City { get; init; } = string.Empty;

    [SwaggerSchema("Postal code.")]
    public string PostalCode { get; init; } = string.Empty;

    [SwaggerSchema("Country.")]
    public string Country { get; init; } = string.Empty;

    [SwaggerSchema("Contact phone.")]
    public string Phone { get; init; } = string.Empty;

    [SwaggerSchema("True if this is the customer's default shipping address.")]
    public bool IsDefault { get; init; }

    [SwaggerSchema("UTC timestamp of address creation.")]
    public DateTime CreatedAt { get; init; }
}

[SwaggerSchema("Payload to create a new saved address.")]
public record CreateAddressRequest
{
    [Required(ErrorMessage = "Full name is required.")]
    [MinLength(2, ErrorMessage = "Full name must be at least 2 characters.")]
    [SwaggerSchema("Recipient full name.")]
    public string FullName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Street is required.")]
    [MinLength(3, ErrorMessage = "Street must be at least 3 characters.")]
    [SwaggerSchema("Street address.")]
    public string Street { get; init; } = string.Empty;

    [Required(ErrorMessage = "City is required.")]
    [MinLength(2, ErrorMessage = "City must be at least 2 characters.")]
    [SwaggerSchema("City.")]
    public string City { get; init; } = string.Empty;

    [Required(ErrorMessage = "Postal code is required.")]
    [MinLength(3, ErrorMessage = "Postal code must be at least 3 characters.")]
    [SwaggerSchema("Postal code.")]
    public string PostalCode { get; init; } = string.Empty;

    [Required(ErrorMessage = "Country is required.")]
    [MinLength(2, ErrorMessage = "Country must be at least 2 characters.")]
    [SwaggerSchema("Country.")]
    public string Country { get; init; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    [MinLength(5, ErrorMessage = "Phone must be at least 5 characters.")]
    [SwaggerSchema("Contact phone.")]
    public string Phone { get; init; } = string.Empty;
}

[SwaggerSchema("Payload to update an existing saved address.")]
public record UpdateAddressRequest
{
    [Required(ErrorMessage = "Full name is required.")]
    [MinLength(2, ErrorMessage = "Full name must be at least 2 characters.")]
    [SwaggerSchema("Recipient full name.")]
    public string FullName { get; init; } = string.Empty;

    [Required(ErrorMessage = "Street is required.")]
    [MinLength(3, ErrorMessage = "Street must be at least 3 characters.")]
    [SwaggerSchema("Street address.")]
    public string Street { get; init; } = string.Empty;

    [Required(ErrorMessage = "City is required.")]
    [MinLength(2, ErrorMessage = "City must be at least 2 characters.")]
    [SwaggerSchema("City.")]
    public string City { get; init; } = string.Empty;

    [Required(ErrorMessage = "Postal code is required.")]
    [MinLength(3, ErrorMessage = "Postal code must be at least 3 characters.")]
    [SwaggerSchema("Postal code.")]
    public string PostalCode { get; init; } = string.Empty;

    [Required(ErrorMessage = "Country is required.")]
    [MinLength(2, ErrorMessage = "Country must be at least 2 characters.")]
    [SwaggerSchema("Country.")]
    public string Country { get; init; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    [MinLength(5, ErrorMessage = "Phone must be at least 5 characters.")]
    [SwaggerSchema("Contact phone.")]
    public string Phone { get; init; } = string.Empty;
}
