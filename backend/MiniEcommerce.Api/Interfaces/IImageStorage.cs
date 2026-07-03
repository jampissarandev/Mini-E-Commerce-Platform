namespace MiniEcommerce.Api.Interfaces;

public interface IImageStorage
{
    Task<string> SaveAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string url, CancellationToken cancellationToken = default);
    string GetPublicUrl(string relativePath);
}
