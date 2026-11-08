namespace Weather.Core.Models;

/// <summary>
/// The id and short header of every forecast source, shared by the Temperature and Snow Forecast tables so a
/// source has the same name on both. Ids never change: hover text and saved client state are keyed by them.
/// Headers stay at 5 characters or fewer so equal-width columns fit them; the full name is in the hover text.
/// </summary>
public static class SourceColumns
{
    public const int MaxLabelLength = 5;

    // Open-Meteo models
    public static readonly ColumnDescriptor Ecmwf = new("ecmwf", "ECMWF");
    public static readonly ColumnDescriptor EcmwfAifs = new("ecmwf-aifs", "AIFS");
    public static readonly ColumnDescriptor Gfs = new("gfs", "GFS");
    public static readonly ColumnDescriptor Nbm = new("nbm", "NBM");
    public static readonly ColumnDescriptor Hrrr = new("hrrr", "HRRR");
    public static readonly ColumnDescriptor Gem = new("gem", "GEM");
    public static readonly ColumnDescriptor Hrdps = new("hrdps", "HRDPS");
    public static readonly ColumnDescriptor Icon = new("icon", "ICON");
    public static readonly ColumnDescriptor Ukmo = new("ukmo", "UKMO");
    public static readonly ColumnDescriptor Jma = new("jma", "JMA");
    public static readonly ColumnDescriptor MeteoFrance = new("meteofrance", "MF");
    public static readonly ColumnDescriptor Cma = new("cma", "CMA");
    public static readonly ColumnDescriptor WeatherNext = new("weathernext", "WN");

    // Forecasters and services
    public static readonly ColumnDescriptor Nws = new("nws", "NWS");
    public static readonly ColumnDescriptor WeatherUnderground = new("wu", "WU");
    public static readonly ColumnDescriptor WeatherApi = new("weatherapi", "WAPI");
    public static readonly ColumnDescriptor NewsCenterMaineCoastal = new("ncm-coastal", "NCM-C");
    public static readonly ColumnDescriptor NewsCenterMaineInland = new("ncm-inland", "NCM-I");

    public static IReadOnlyList<ColumnDescriptor> All { get; } =
    [
        Ecmwf, EcmwfAifs, Gfs, Nbm, Hrrr, Gem, Hrdps, Icon, Ukmo, Jma, MeteoFrance, Cma, WeatherNext,
        Nws, WeatherUnderground, WeatherApi, NewsCenterMaineCoastal, NewsCenterMaineInland,
    ];
}
