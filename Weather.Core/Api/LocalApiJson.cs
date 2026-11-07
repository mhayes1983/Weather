using System.Text.Json;
using System.Text.Json.Serialization;
using Weather.Core.Models;

namespace Weather.Core.Api;

// Source generated, because trimming on Android removes what reflection needs.
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UseStringEnumConverter = true)]
[JsonSerializable(typeof(ForecastTable))]
[JsonSerializable(typeof(IReadOnlyList<StationSummary>))]
[JsonSerializable(typeof(StationMatch))]
[JsonSerializable(typeof(StationReport))]
[JsonSerializable(typeof(AlertReport))]
[JsonSerializable(typeof(DailyForecastReport))]
[JsonSerializable(typeof(ForecastDayDetail))]
[JsonSerializable(typeof(AlmanacReport))]
[JsonSerializable(typeof(SnowTable))]
[JsonSerializable(typeof(IReadOnlyList<SnowTownSummary>))]
[JsonSerializable(typeof(SnowAlertReport))]
[JsonSerializable(typeof(LocalApiProblem))]
internal sealed partial class LocalApiJson : JsonSerializerContext;
