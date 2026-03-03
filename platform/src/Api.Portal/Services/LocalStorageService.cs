namespace Api.Portal.Services;

public class LocalStorageService(IConfiguration configuration) : IStorageService
{
    private string LocalPath => configuration["Storage:LocalPath"] ?? "uploads";

    public async Task<string> SaveAsync(Guid tenantId, Guid documentId, string fileName, Stream content)
    {
        var dir = Path.Combine(LocalPath, tenantId.ToString(), documentId.ToString());
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, fileName);
        await using var fs = File.Create(filePath);
        await content.CopyToAsync(fs);
        return filePath;
    }

    public Task<Stream> OpenReadAsync(string storagePath)
    {
        Stream stream = File.OpenRead(storagePath);
        return Task.FromResult(stream);
    }

    public Task<byte[]> ReadAllBytesAsync(string storagePath)
        => File.ReadAllBytesAsync(storagePath);
}
