namespace Weather.Core.Models;

/// <summary>A date's afternoon high and morning low, the low being the night that ends that morning.</summary>
public sealed record DailyTemperatures(DateOnly Date, int? HighF, int? LowF);
