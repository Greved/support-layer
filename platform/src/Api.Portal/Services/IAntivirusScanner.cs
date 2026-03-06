namespace Api.Portal.Services;

public enum AntivirusScanStatus
{
    Clean,
    Infected,
    Unavailable,
}

public sealed record AntivirusScanResult(
    AntivirusScanStatus Status,
    string? Signature = null,
    string? Details = null
);

public interface IAntivirusScanner
{
    Task<AntivirusScanResult> ScanAsync(Stream stream, CancellationToken ct = default);
}
