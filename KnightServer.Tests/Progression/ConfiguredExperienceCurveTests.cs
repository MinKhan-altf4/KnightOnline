using KnightOnline.Server.Configuration;
using KnightOnline.Server.Progression;

namespace KnightOnline.Server.Tests.Progression;

public sealed class ConfiguredExperienceCurveTests
{
    [Fact]
    public void ResolveLevel_SupportsMultipleLevelUpsAndConfiguredCap()
    {
        var curve = new ConfiguredExperienceCurve(new ProgressionOptions
        {
            MaximumLevel = 40,
            BaseExperienceToNextLevel = 100,
            LinearExperienceGrowth = 0,
            QuadraticExperienceGrowth = 0,
        });

        Assert.Equal(4, curve.ResolveLevel(350, 40));
        Assert.Equal(10, curve.ResolveLevel(long.MaxValue, 10));
        Assert.Equal(40, curve.ResolveLevel(long.MaxValue, 40));
    }

    [Fact]
    public void TotalThreshold_IsMonotonicThroughLevelForty()
    {
        var curve = new ConfiguredExperienceCurve(new ProgressionOptions());

        long previous = -1;
        for (int level = 1; level <= 40; level++)
        {
            long current = curve.GetTotalExperienceRequiredForLevel(level);
            Assert.True(current > previous);
            previous = current;
        }
    }
}
