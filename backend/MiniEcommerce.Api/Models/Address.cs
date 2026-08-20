namespace MiniEcommerce.Api.Models;

public class Address
{
    public int Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public ApplicationUser Customer { get; set; } = null!;
    public string FullName { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
