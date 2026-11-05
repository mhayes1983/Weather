namespace Weather.Core.Models;

public sealed record DailyTemperatures(DateOnly Date, int? HighF, int? LowF);
