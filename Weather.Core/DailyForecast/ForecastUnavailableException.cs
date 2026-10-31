namespace Weather.Core.DailyForecast;

public sealed class ForecastUnavailableException(string message) : Exception(message);
