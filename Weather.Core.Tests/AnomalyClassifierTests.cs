using Weather.Core.Models;

namespace Weather.Core.Tests;

public class AnomalyClassifierTests
{
    [Theory]
    [InlineData(59, Anomaly.Above)]
    [InlineData(58, Anomaly.None)]
    [InlineData(48, Anomaly.None)]
    [InlineData(47, Anomaly.Below)]
    public void AppliesThresholdsInclusively(int high, Anomaly expected)
    {
        Assert.Equal(expected, AnomalyClassifier.Classify(high, averageF: 53, aboveF: 6, belowF: 6));
    }

    [Fact]
    public void MissingAverageIsNeverHighlighted()
    {
        Assert.Equal(Anomaly.None, AnomalyClassifier.Classify(90, averageF: null, aboveF: 6, belowF: 6));
    }
}
