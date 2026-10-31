namespace Weather.Core;

internal static class SourceFailure
{
    public static string Describe(Exception exception) => exception switch
    {
        OperationCanceledException => "Timed out",
        HttpRequestException { StatusCode: { } status } => $"Source returned HTTP {(int)status}",
        HttpRequestException => "Source could not be reached",
        _ => "Source data could not be read",
    };
}
