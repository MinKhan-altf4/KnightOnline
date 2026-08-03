using KnightOnline.Server.Configuration;

namespace KnightOnline.Server.Progression;

public sealed class ConfiguredExperienceCurve : IExperienceCurve
{
    private readonly ProgressionOptions _options;
    private readonly long[] _totalRequiredByLevel;

    public ConfiguredExperienceCurve(ProgressionOptions options)
    {
        _options = options;
        MaximumLevel = options.MaximumLevel;
        _totalRequiredByLevel = new long[MaximumLevel + 1];
        for (int level = 2; level <= MaximumLevel; level++)
        {
            _totalRequiredByLevel[level] = checked(
                _totalRequiredByLevel[level - 1] +
                GetExperienceRequiredToAdvance(level - 1));
        }
    }

    public int MaximumLevel { get; }

    public long GetTotalExperienceRequiredForLevel(int level)
    {
        if (level < 1 || level > MaximumLevel)
            throw new ArgumentOutOfRangeException(nameof(level));
        return _totalRequiredByLevel[level];
    }

    public long GetExperienceRequiredToAdvance(int level)
    {
        if (level < 1 || level >= MaximumLevel)
            return 0;

        long offset = level - 1L;
        return checked(
            _options.BaseExperienceToNextLevel +
            _options.LinearExperienceGrowth * offset +
            _options.QuadraticExperienceGrowth * offset * offset);
    }

    public int ResolveLevel(long totalExperience, int maximumAllowedLevel)
    {
        int cap = Math.Clamp(maximumAllowedLevel, 1, MaximumLevel);
        int level = 1;
        while (level < cap &&
               totalExperience >= _totalRequiredByLevel[level + 1])
        {
            level++;
        }
        return level;
    }
}
