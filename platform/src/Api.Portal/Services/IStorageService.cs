namespace Api.Portal.Services;

public interface IStorageService
{
    Task<string> SaveAsync(Guid tenantId, Guid documentId, string fileName, Stream content);
    Task<Stream> OpenReadAsync(string storagePath);
    Task<byte[]> ReadAllBytesAsync(string storagePath);
}
