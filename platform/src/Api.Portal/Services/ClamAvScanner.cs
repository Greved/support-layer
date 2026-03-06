using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace Api.Portal.Services;

public sealed class ClamAvScanner(IConfiguration configuration, ILogger<ClamAvScanner> logger) : IAntivirusScanner
{
    private readonly bool _enabled = configuration.GetValue("Antivirus:Enabled", false);
    private readonly bool _failOpen = configuration.GetValue("Antivirus:FailOpen", false);
    private readonly string _host = configuration["Antivirus:Host"] ?? "localhost";
    private readonly int _port = configuration.GetValue("Antivirus:Port", 3310);
    private readonly int _timeoutSeconds = configuration.GetValue("Antivirus:TimeoutSeconds", 10);

    public async Task<AntivirusScanResult> ScanAsync(Stream stream, CancellationToken ct = default)
    {
        if (!_enabled)
            return new AntivirusScanResult(AntivirusScanStatus.Clean);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

            using var client = new TcpClient();
            await client.ConnectAsync(_host, _port, timeoutCts.Token);

            await using var network = client.GetStream();
            await WriteInstreamCommandAsync(network, stream, timeoutCts.Token);
            var response = await ReadResponseAsync(network, timeoutCts.Token);

            if (response.Contains("FOUND", StringComparison.OrdinalIgnoreCase))
            {
                return new AntivirusScanResult(
                    AntivirusScanStatus.Infected,
                    Signature: ExtractSignature(response),
                    Details: response);
            }

            if (response.Contains("OK", StringComparison.OrdinalIgnoreCase))
                return new AntivirusScanResult(AntivirusScanStatus.Clean);

            logger.LogWarning("Unexpected ClamAV response: {Response}", response);
            return HandleUnavailable($"unexpected_response:{response}");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("ClamAV scan timed out host={Host} port={Port}", _host, _port);
            return HandleUnavailable("timeout");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ClamAV scan failed host={Host} port={Port}", _host, _port);
            return HandleUnavailable(ex.Message);
        }
    }

    private AntivirusScanResult HandleUnavailable(string details)
    {
        if (_failOpen)
            return new AntivirusScanResult(AntivirusScanStatus.Clean, Details: $"fail_open:{details}");

        return new AntivirusScanResult(AntivirusScanStatus.Unavailable, Details: details);
    }

    private static async Task WriteInstreamCommandAsync(
        NetworkStream network,
        Stream file,
        CancellationToken ct)
    {
        var command = Encoding.ASCII.GetBytes("INSTREAM\n");
        await network.WriteAsync(command, ct);

        var buffer = new byte[8192];
        var lenBuffer = new byte[4];
        while (true)
        {
            var read = await file.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read <= 0) break;

            BinaryPrimitives.WriteUInt32BigEndian(lenBuffer, (uint)read);

            await network.WriteAsync(lenBuffer.AsMemory(0, lenBuffer.Length), ct);
            await network.WriteAsync(buffer.AsMemory(0, read), ct);
        }

        // Zero-size chunk terminates stream.
        await network.WriteAsync(new byte[4], ct);
        await network.FlushAsync(ct);
    }

    private static async Task<string> ReadResponseAsync(NetworkStream network, CancellationToken ct)
    {
        using var reader = new StreamReader(network, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var line = await reader.ReadLineAsync(ct);
        return string.IsNullOrWhiteSpace(line) ? "empty_response" : line.Trim();
    }

    private static string? ExtractSignature(string response)
    {
        // Example: "stream: Win.Test.EICAR_HDB-1 FOUND"
        const string foundSuffix = " FOUND";
        var trimmed = response.Trim();

        if (trimmed.EndsWith(foundSuffix, StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^foundSuffix.Length];

        var colonIndex = trimmed.IndexOf(':');
        if (colonIndex >= 0 && colonIndex < trimmed.Length - 1)
            return trimmed[(colonIndex + 1)..].Trim();

        return trimmed;
    }
}
