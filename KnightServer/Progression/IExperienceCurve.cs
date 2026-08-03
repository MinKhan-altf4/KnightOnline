namespace KnightOnline.Server.Progression;

public interface IExperienceCurve
{
    int MaximumLevel { get; }
    long GetTotalExperienceRequiredForLevel(int level);
    long GetExperienceRequiredToAdvance(int level);
    int ResolveLevel(long totalExperience, int maximumAllowedLevel);
}
