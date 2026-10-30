using Weather.Core.Models;

namespace Weather.Core;

public static class AnomalyClassifier
{
    public static Anomaly Classify(int highF, int? averageF, int aboveF, int belowF)
    {
        if (averageF is not int average)
        {
            return Anomaly.None;
        }

        var difference = highF - average;
        if (difference >= aboveF)
        {
            return Anomaly.Above;
        }

        return difference <= -belowF ? Anomaly.Below : Anomaly.None;
    }
}
