using System;

namespace CursorHunter.Progression
{
    /// <summary>Shared level curve and first-run talent-card pacing.</summary>
    public static class ProgressionLevelCurve
    {
        public const int MaximumLevel = 25;

        public static long ExperienceForNextLevel(int currentLevel)
        {
            if (currentLevel >= MaximumLevel) return 0L;
            int level = Math.Max(1, currentLevel);
            return 250L + 70L * (level - 1);
        }

        public static long TotalExperienceForLevel(int level)
        {
            int target = Math.Max(1, Math.Min(MaximumLevel, level));
            long total = 0L;
            for (int current = 1; current < target; current++)
                total = SaturatingAdd(total, ExperienceForNextLevel(current));
            return total;
        }

        public static int GetLevel(long totalExperience)
        {
            long remaining = Math.Max(0L, totalExperience);
            int level = 1;
            while (level < MaximumLevel)
            {
                long required = ExperienceForNextLevel(level);
                if (remaining < required) break;
                remaining -= required;
                level++;
            }
            return level;
        }

        private static long SaturatingAdd(long left, long right) =>
            right > long.MaxValue - left ? long.MaxValue : left + right;
    }
}
