using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using MiniEcommerce.Api.Dtos;
using MiniEcommerce.Api.Tests.Infrastructure;

namespace MiniEcommerce.Api.Tests.Integration.Controllers;

/// <summary>
/// End-to-end HTTP tests for <c>AddressesController</c> (Task 26 — ADR 0004).
/// Uses the shared in-memory database via <see cref="ApiFactory"/>.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class AddressesControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AddressesControllerTests(ApiFactory factory) => _factory = factory;

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ─────────────── GET /api/addresses ───────────────

    [Fact]
    public async Task GetAddresses_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAddresses_Empty_Returns200WithEmptyList()
    {
        var client = await AuthenticatedClientAsync("addr-list-1@example.com");

        var response = await client.GetAsync("/api/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AddressDto>>>(Json);
        body!.Success.Should().BeTrue();
        body.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAddresses_ReturnsOnlyCurrentUserAddresses()
    {
        var clientA = await AuthenticatedClientAsync("addr-list-a@example.com");
        var clientB = await AuthenticatedClientAsync("addr-list-b@example.com");

        // User A creates an address
        await clientA.PostAsJsonAsync("/api/addresses", ValidAddressRequest("User A"));
        // User B creates an address
        await clientB.PostAsJsonAsync("/api/addresses", ValidAddressRequest("User B"));

        // User A should only see their own
        var response = await clientA.GetAsync("/api/addresses");
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AddressDto>>>(Json);
        body!.Data!.Should().HaveCount(1);
        body.Data![0].FullName.Should().Be("User A");
    }

    // ─────────────── POST /api/addresses ───────────────

    [Fact]
    public async Task CreateAddress_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/addresses", ValidAddressRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAddress_InvalidPayload_Returns400()
    {
        var client = await AuthenticatedClientAsync("addr-create-invalid@example.com");

        var response = await client.PostAsJsonAsync("/api/addresses", new CreateAddressRequest
        {
            FullName = "",       // too short
            Street = "",         // too short
            City = "C",
            PostalCode = "12",
            Country = "US",
            Phone = "1234"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAddress_ValidPayload_Returns201()
    {
        var client = await AuthenticatedClientAsync("addr-create-ok@example.com");

        var response = await client.PostAsJsonAsync("/api/addresses", ValidAddressRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AddressDto>>(Json);
        body!.Success.Should().BeTrue();
        body.Data!.Id.Should().BeGreaterThan(0);
        body.Data.FullName.Should().Be("Jane Doe");
        body.Data.Street.Should().Be("123 Main St");
        body.Data.City.Should().Be("Springfield");
        body.Data.PostalCode.Should().Be("62704");
        body.Data.Country.Should().Be("US");
        body.Data.Phone.Should().Be("+1-555-0100");
    }

    [Fact]
    public async Task CreateAddress_FirstAddressIsDefault()
    {
        var client = await AuthenticatedClientAsync("addr-create-default@example.com");

        var response = await client.PostAsJsonAsync("/api/addresses", ValidAddressRequest());

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AddressDto>>(Json);
        body!.Data!.IsDefault.Should().BeTrue("the first address should be auto-set as default");
    }

    [Fact]
    public async Task CreateAddress_SecondAddressIsNotDefault()
    {
        var client = await AuthenticatedClientAsync("addr-create-second@example.com");

        await client.PostAsJsonAsync("/api/addresses", ValidAddressRequest("First"));
        var response = await client.PostAsJsonAsync("/api/addresses", ValidAddressRequest("Second"));

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AddressDto>>(Json);
        body!.Data!.IsDefault.Should().BeFalse("only one address can be default");
    }

    // ─────────────── PUT /api/addresses/:id ───────────────

    [Fact]
    public async Task UpdateAddress_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/addresses/1", new UpdateAddressRequest
        {
            FullName = "Updated",
            Street = "456 New St",
            City = "New City",
            PostalCode = "00000",
            Country = "US",
            Phone = "+1-555-0200"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateAddress_NonExistent_Returns404()
    {
        var client = await AuthenticatedClientAsync("addr-update-404@example.com");

        var response = await client.PutAsJsonAsync("/api/addresses/99999", new UpdateAddressRequest
        {
            FullName = "Updated",
            Street = "456 New St",
            City = "New City",
            PostalCode = "00000",
            Country = "US",
            Phone = "+1-555-0200"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("ADDRESS_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateAddress_AnotherUsersAddress_Returns404()
    {
        var clientA = await AuthenticatedClientAsync("addr-update-a@example.com");
        var clientB = await AuthenticatedClientAsync("addr-update-b@example.com");

        // User A creates an address
        var createResponse = await clientA.PostAsJsonAsync("/api/addresses", ValidAddressRequest());
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<AddressDto>>(Json))!;

        // User B tries to update it
        var response = await clientB.PutAsJsonAsync($"/api/addresses/{created.Data!.Id}", new UpdateAddressRequest
        {
            FullName = "Hacked",
            Street = "456 Evil St",
            City = "Villainville",
            PostalCode = "00000",
            Country = "EV",
            Phone = "+1-555-0300"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateAddress_ValidPayload_Returns200()
    {
        var client = await AuthenticatedClientAsync("addr-update-ok@example.com");

        var createResponse = await client.PostAsJsonAsync("/api/addresses", ValidAddressRequest());
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<AddressDto>>(Json))!;

        var response = await client.PutAsJsonAsync($"/api/addresses/{created.Data!.Id}", new UpdateAddressRequest
        {
            FullName = "Updated Name",
            Street = "789 Updated Ave",
            City = "New City",
            PostalCode = "11111",
            Country = "CA",
            Phone = "+1-555-0400"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AddressDto>>(Json);
        body!.Success.Should().BeTrue();
        body.Data!.FullName.Should().Be("Updated Name");
        body.Data.Street.Should().Be("789 Updated Ave");
        body.Data.City.Should().Be("New City");
    }

    // ─────────────── DELETE /api/addresses/:id ───────────────

    [Fact]
    public async Task DeleteAddress_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/api/addresses/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAddress_NonExistent_Returns404()
    {
        var client = await AuthenticatedClientAsync("addr-delete-404@example.com");

        var response = await client.DeleteAsync("/api/addresses/99999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(Json);
        body!.Error!.Code.Should().Be("ADDRESS_NOT_FOUND");
    }

    [Fact]
    public async Task DeleteAddress_Valid_Returns200AndRemovesFromList()
    {
        var client = await AuthenticatedClientAsync("addr-delete-ok@example.com");

        var createResponse = await client.PostAsJsonAsync("/api/addresses", ValidAddressRequest());
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<AddressDto>>(Json))!;

        var deleteResponse = await client.DeleteAsync($"/api/addresses/{created.Data!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify it's gone
        var listResponse = await client.GetAsync("/api/addresses");
        var list = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<AddressDto>>>(Json);
        list!.Data!.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAddress_AnotherUsersAddress_Returns404()
    {
        var clientA = await AuthenticatedClientAsync("addr-delete-a@example.com");
        var clientB = await AuthenticatedClientAsync("addr-delete-b@example.com");

        var createResponse = await clientA.PostAsJsonAsync("/api/addresses", ValidAddressRequest());
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<AddressDto>>(Json))!;

        var response = await clientB.DeleteAsync($"/api/addresses/{created.Data!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAddress_WhenDefaultDeleted_PromotesMostRecentRemaining()
    {
        // ADR 0004 invariant: at-most-one default per customer. After deleting
        // the default, the most-recent remaining must be promoted.
        var client = await AuthenticatedClientAsync("addr-delete-promote@example.com");

        // Create 3 addresses in order; the first created is auto-default.
        var oldest = await CreateAddressAsync(client, "Oldest");
        await Task.Delay(5); // ensure CreatedAt differs
        var middle = await CreateAddressAsync(client, "Middle");
        await Task.Delay(5);
        var newest = await CreateAddressAsync(client, "Newest");

        oldest.IsDefault.Should().BeTrue();

        // Delete the default.
        var deleteResponse = await client.DeleteAsync($"/api/addresses/{oldest.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert: exactly one default, and it's the most recent.
        var listResponse = await client.GetAsync("/api/addresses");
        var list = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<AddressDto>>>(Json);
        var defaults = list!.Data!.Where(a => a.IsDefault).ToList();
        defaults.Should().HaveCount(1);
        defaults[0].FullName.Should().Be("Newest");
    }

    [Fact]
    public async Task DeleteAddress_WhenNonDefaultDeleted_DefaultRemainsIntact()
    {
        var client = await AuthenticatedClientAsync("addr-delete-nondef@example.com");

        var oldest = await CreateAddressAsync(client, "Oldest");
        await Task.Delay(5);
        var middle = await CreateAddressAsync(client, "Middle");
        await Task.Delay(5);
        var newest = await CreateAddressAsync(client, "Newest");

        // Delete a non-default.
        var deleteResponse = await client.DeleteAsync($"/api/addresses/{middle.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert: original default still default, no spurious promotion.
        var listResponse = await client.GetAsync("/api/addresses");
        var list = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<AddressDto>>>(Json);
        var defaults = list!.Data!.Where(a => a.IsDefault).ToList();
        defaults.Should().HaveCount(1);
        defaults[0].Id.Should().Be(oldest.Id);
    }

    [Fact]
    public async Task DeleteAddress_WhenOnlyAddressExists_NoPromotionRequired()
    {
        // Edge case: user has one address (auto-default), deletes it.
        // Result: 0 addresses, 0 defaults — invariant trivially holds.
        var client = await AuthenticatedClientAsync("addr-delete-only@example.com");

        var only = await CreateAddressAsync(client, "Lonely");
        only.IsDefault.Should().BeTrue();

        var deleteResponse = await client.DeleteAsync($"/api/addresses/{only.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await client.GetAsync("/api/addresses");
        var list = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<AddressDto>>>(Json);
        list!.Data!.Should().BeEmpty();
    }

    private async Task<AddressDto> CreateAddressAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/addresses", new CreateAddressRequest
        {
            FullName = name,
            Street = "1 Test St",
            City = "Testville",
            PostalCode = "00000",
            Country = "US",
            Phone = "+1-555-0000"
        });
        var body = (await response.Content.ReadFromJsonAsync<ApiResponse<AddressDto>>(Json))!;
        return body.Data!;
    }

    // ─────────────── PUT /api/addresses/:id/default ───────────────

    [Fact]
    public async Task SetDefault_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/addresses/1/default", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetDefault_NonExistent_Returns404()
    {
        var client = await AuthenticatedClientAsync("addr-default-404@example.com");

        var response = await client.PutAsJsonAsync("/api/addresses/99999/default", new { });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetDefault_UnsetsOtherDefaults()
    {
        var client = await AuthenticatedClientAsync("addr-default-unset@example.com");

        // Create two addresses
        var addr1Response = await client.PostAsJsonAsync("/api/addresses", ValidAddressRequest("First"));
        var addr1 = (await addr1Response.Content.ReadFromJsonAsync<ApiResponse<AddressDto>>(Json))!;
        var addr2Response = await client.PostAsJsonAsync("/api/addresses", ValidAddressRequest("Second"));
        var addr2 = (await addr2Response.Content.ReadFromJsonAsync<ApiResponse<AddressDto>>(Json))!;

        // addr1 should be default (first created), addr2 should not
        addr1.Data!.IsDefault.Should().BeTrue();
        addr2.Data!.IsDefault.Should().BeFalse();

        // Set addr2 as default
        var response = await client.PutAsJsonAsync($"/api/addresses/{addr2.Data!.Id}/default", new { });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify: addr2 is now default, addr1 is not
        var listResponse = await client.GetAsync("/api/addresses");
        var list = (await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<AddressDto>>>(Json))!;
        var listData = list.Data!;
        var updated1 = listData.Single(a => a.Id == addr1.Data!.Id);
        var updated2 = listData.Single(a => a.Id == addr2.Data!.Id);
        updated1.IsDefault.Should().BeFalse();
        updated2.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task SetDefault_AnotherUsersAddress_Returns404()
    {
        var clientA = await AuthenticatedClientAsync("addr-default-a@example.com");
        var clientB = await AuthenticatedClientAsync("addr-default-b@example.com");

        var createResponse = await clientA.PostAsJsonAsync("/api/addresses", ValidAddressRequest());
        var created = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<AddressDto>>(Json))!;

        var response = await clientB.PutAsJsonAsync($"/api/addresses/{created.Data!.Id}/default", new { });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─────────────── helpers ───────────────

    private static CreateAddressRequest ValidAddressRequest(string fullName = "Jane Doe") => new()
    {
        FullName = fullName,
        Street = "123 Main St",
        City = "Springfield",
        PostalCode = "62704",
        Country = "US",
        Phone = "+1-555-0100"
    };

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123",
            FullName = "Address Test User"
        });
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password123"
        });
        var body = await login.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.Token);
        return client;
    }
}
