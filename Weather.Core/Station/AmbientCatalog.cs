using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Weather.Core.Locations;

namespace Weather.Core.Station;

/// <summary>
/// Every device on every configured Ambient Weather account. The home device keeps the "home" ID, so
/// saved defaults and links still work; the others get an ID from <see cref="AmbientAccount.DeviceIdFor"/>.
/// Settings in <see cref="AmbientWeatherOptions.Stations"/> name, locate, order or hide them.
/// </summary>
public sealed class AmbientCatalog
{
    private const string FallbackName = "Ambient station";

    private readonly IOptions<WeatherOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _discoveryGate = new(1, 1);
    private readonly Dictionary<AmbientAccount, IReadOnlyList<AmbientDevice>> _lastGood = [];
    private IReadOnlyList<AmbientWeatherStation>? _stations;
    private DateTimeOffset _stationsExpire;

    public AmbientCatalog(
        IHttpClientFactory httpClientFactory,
        IOptions<WeatherOptions> options,
        LocationResolver locations,
        TimeProvider timeProvider,
        ILogger<AmbientCatalog>? logger = null)
    {
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<AmbientCatalog>.Instance;
        Locations = locations;

        var settings = options.Value.AmbientWeather;
        Accounts = new[] { new AmbientAccountOptions { ApiKey = settings.ApiKey, ApplicationKey = settings.ApplicationKey } }
            .Concat(settings.Accounts)
            .Where(account => !string.IsNullOrWhiteSpace(account.ApiKey) && !string.IsNullOrWhiteSpace(account.ApplicationKey))
            .DistinctBy(account => account.ApiKey!.Trim())
            .Select(account => new AmbientAccount(account.ApiKey!.Trim(), account.ApplicationKey!.Trim(), httpClientFactory, options, timeProvider))
            .ToList();

        var home = HomeSettings;
        var zip = string.IsNullOrWhiteSpace(home?.Zip) ? DefaultZip : home.Zip.Trim();
        Home = new AmbientWeatherStation(this, AmbientWeatherStation.HomeId, Named(home?.Name) ?? settings.Name, zip, null);
    }

    public IReadOnlyList<AmbientAccount> Accounts { get; }

    /// <summary>Always present, so "home" never falls through to a Weather Underground lookup.</summary>
    public AmbientWeatherStation Home { get; }

    public bool HasKeys => Accounts.Count > 0;

    internal LocationResolver Locations { get; }

    internal WeatherOptions Options => _options.Value;

    internal TimeProvider Time => _timeProvider;

    private AmbientWeatherOptions Settings => _options.Value.AmbientWeather;

    private string? DefaultZip =>
        string.IsNullOrWhiteSpace(_options.Value.DefaultLocation.Zip) ? null : _options.Value.DefaultLocation.Zip;

    private AmbientStationOptions? HomeSettings => Settings.Stations.FirstOrDefault(station => station.Home);

    public static bool IsDeviceId(string? id) =>
        id is not null && id.StartsWith(AmbientAccount.DeviceIdPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>The devices other than home, in the configured order and then the accounts' order.</summary>
    public async Task<IReadOnlyList<AmbientWeatherStation>> GetStationsAsync(CancellationToken cancellationToken = default)
    {
        if (!HasKeys)
        {
            return [];
        }

        // Discovery is shared, so it has its own timeouts and the caller only stops waiting.
        return await DiscoverAsync().WaitAsync(cancellationToken);
    }

    public async Task<AmbientWeatherStation?> FindAsync(string id, CancellationToken cancellationToken = default) =>
        (await GetStationsAsync(cancellationToken))
            .FirstOrDefault(station => string.Equals(station.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The home station's device: the one set as home, else the first account's first device.</summary>
    internal async Task<AmbientDevice> FindHomeDeviceAsync(CancellationToken cancellationToken)
    {
        if (!HasKeys)
        {
            throw new InvalidOperationException("No Ambient Weather keys are set.");
        }

        var isHome = HomeMatch;

        // Asked one account at a time, so a single-account setup makes one call.
        foreach (var account in Accounts)
        {
            var devices = await account.GetDevicesAsync(cancellationToken);
            var device = isHome is null ? devices.FirstOrDefault() : devices.FirstOrDefault(isHome);
            if (device is not null)
            {
                return device;
            }

            if (isHome is null)
            {
                break;
            }
        }

        throw new InvalidOperationException("No matching station on the Ambient Weather account.");
    }

    private async Task<IReadOnlyList<AmbientWeatherStation>> DiscoverAsync()
    {
        await _discoveryGate.WaitAsync();
        try
        {
            if (_stations is not null && _timeProvider.GetUtcNow() < _stationsExpire)
            {
                return _stations;
            }

            var results = await Task.WhenAll(Accounts.Select(ListAsync));
            var failed = results.Any(result => result.Failed);
            _stations = Build(results.SelectMany(result => result.Devices).ToList());
            _stationsExpire = _timeProvider.GetUtcNow() + (failed
                ? TimeSpan.FromMinutes(_options.Value.ErrorRetryMinutes)
                : TimeSpan.FromMinutes(Settings.DiscoveryCacheMinutes));
            return _stations;
        }
        finally
        {
            _discoveryGate.Release();
        }
    }

    // One account failing keeps its last good list, or drops only its own stations.
    private async Task<(IReadOnlyList<AmbientDevice> Devices, bool Failed)> ListAsync(AmbientAccount account)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_options.Value.SourceTimeoutSeconds));
        try
        {
            var devices = await account.GetDevicesAsync(timeout.Token);
            lock (_lastGood)
            {
                _lastGood[account] = devices;
            }

            return (devices, false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Ambient Weather device list failed: {Error}", exception.GetType().Name);
            lock (_lastGood)
            {
                return (_lastGood.GetValueOrDefault(account) ?? [], true);
            }
        }
    }

    private List<AmbientWeatherStation> Build(List<AmbientDevice> devices)
    {
        var overrides = Settings.Stations;
        var isHome = HomeMatch;
        var homeDevice = isHome is null
            ? devices.FirstOrDefault(device => device.Account == Accounts[0])
            : devices.FirstOrDefault(isHome);

        var unnamed = 1;
        return devices
            .Where(device => device != homeDevice)
            .Select(device => (Device: device, Settings: overrides.FindIndex(station => Matches(station, device))))
            .Where(entry => entry.Settings < 0 || !overrides[entry.Settings].Hidden)
            .OrderBy(entry => entry.Settings < 0 ? int.MaxValue : entry.Settings)
            .Select(entry =>
            {
                var settings = entry.Settings < 0 ? null : overrides[entry.Settings];
                var name = Named(settings?.Name) ?? entry.Device.Name ?? $"{FallbackName} {++unnamed}";
                var zip = string.IsNullOrWhiteSpace(settings?.Zip) ? null : settings.Zip.Trim();
                return new AmbientWeatherStation(this, entry.Device.DeviceId, name, zip, entry.Device);
            })
            .ToList();
    }

    // Null means the first account's first device, as before there were several.
    private Func<AmbientDevice, bool>? HomeMatch
    {
        get
        {
            var home = HomeSettings;
            if (home is not null && (!string.IsNullOrWhiteSpace(home.MacAddress) || !string.IsNullOrWhiteSpace(home.DeviceId)))
            {
                return device => Matches(home, device);
            }

            var macAddress = Settings.MacAddress?.Trim();
            return string.IsNullOrEmpty(macAddress)
                ? null
                : device => string.Equals(device.MacAddress, macAddress, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool Matches(AmbientStationOptions station, AmbientDevice device) =>
        (!string.IsNullOrWhiteSpace(station.MacAddress)
            && string.Equals(station.MacAddress.Trim(), device.MacAddress, StringComparison.OrdinalIgnoreCase))
        || (!string.IsNullOrWhiteSpace(station.DeviceId)
            && string.Equals(station.DeviceId.Trim(), device.DeviceId, StringComparison.OrdinalIgnoreCase));

    private static string? Named(string? name) => string.IsNullOrWhiteSpace(name) ? null : name.Trim();
}
