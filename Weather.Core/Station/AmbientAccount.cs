using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Weather.Core.Station;

/// <summary>A device on an Ambient Weather account, with its latest reading.</summary>
public sealed record AmbientDevice(AmbientAccount Account, string MacAddress, string DeviceId, string? Name, JsonElement LastData);

/// <summary>
/// One Ambient Weather key pair. Ambient allows one request a second per key, so every station on
/// the account shares this gate. One device list holds every device's latest reading, so it's
/// shared too, and kept as long as a current reading.
/// </summary>
public sealed class AmbientAccount(
    string apiKey,
    string applicationKey,
    IHttpClientFactory httpClientFactory,
    IOptions<WeatherOptions> options,
    TimeProvider timeProvider)
{
    public const string DeviceIdPrefix = "amb-";

    private const string DevicesUrl = "https://rt.ambientweather.net/v1/devices";

    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private DateTimeOffset _nextRequestAt = DateTimeOffset.MinValue;
    private IReadOnlyList<AmbientDevice>? _devices;
    private DateTimeOffset _devicesAt;

    internal string ApiKey => apiKey;

    internal string ApplicationKey => applicationKey;

    private AmbientWeatherOptions Settings => options.Value.AmbientWeather;

    /// <summary>
    /// A stable ID for a device that doesn't reveal its MAC address. The key is secret,
    /// so the short space of MAC addresses can't be searched for a match.
    /// </summary>
    public string DeviceIdFor(string macAddress)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(apiKey), Encoding.UTF8.GetBytes(macAddress.ToUpperInvariant()));
        return DeviceIdPrefix + Convert.ToHexStringLower(hash)[..12];
    }

    public async Task<IReadOnlyList<AmbientDevice>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        await _requestGate.WaitAsync(cancellationToken);
        try
        {
            // Callers queue here, so the ones behind the first share its list.
            if (_devices is not null && timeProvider.GetUtcNow() - _devicesAt < TimeSpan.FromSeconds(Settings.CurrentCacheSeconds))
            {
                return _devices;
            }

            var json = await SendAsync(DevicesUrl, null, cancellationToken);
            _devices = ParseDevices(json);
            _devicesAt = timeProvider.GetUtcNow();
            return _devices;
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async Task<string> GetHistoryAsync(string macAddress, string query, CancellationToken cancellationToken)
    {
        await _requestGate.WaitAsync(cancellationToken);
        try
        {
            return await SendAsync($"{DevicesUrl}/{Uri.EscapeDataString(macAddress)}", query, cancellationToken);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private List<AmbientDevice> ParseDevices(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Where(device => device.TryGetProperty("macAddress", out var mac) && mac.ValueKind == JsonValueKind.String)
            .Select(device =>
            {
                var mac = device.GetProperty("macAddress").GetString()!;
                var name = device.TryGetProperty("info", out var info) && info.TryGetProperty("name", out var named) && named.ValueKind == JsonValueKind.String
                    ? named.GetString()
                    : null;
                var lastData = device.TryGetProperty("lastData", out var data) ? data.Clone() : default;
                return new AmbientDevice(this, mac, DeviceIdFor(mac), string.IsNullOrWhiteSpace(name) ? null : name.Trim(), lastData);
            })
            .ToList();
    }

    // Callers hold the gate.
    private async Task<string> SendAsync(string url, string? query, CancellationToken cancellationToken)
    {
        var fullUrl = $"{url}?apiKey={Uri.EscapeDataString(apiKey)}&applicationKey={Uri.EscapeDataString(applicationKey)}{query}";

        var wait = _nextRequestAt - timeProvider.GetUtcNow();
        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, timeProvider, cancellationToken);
        }

        try
        {
            var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
            return await client.GetStringAsync(fullUrl, cancellationToken);
        }
        finally
        {
            _nextRequestAt = timeProvider.GetUtcNow().AddMilliseconds(Settings.RequestSpacingMilliseconds);
        }
    }
}
