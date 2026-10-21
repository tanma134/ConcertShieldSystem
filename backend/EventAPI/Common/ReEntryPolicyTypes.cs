using System;
using System.Linq;

namespace EventAPI.Common
{
    public static class ReEntryPolicyTypes
    {
        public const string NoReEntry = "NoReEntry";
        public const string Unlimited = "Unlimited";
        public const string TimeLimited = "TimeLimited";

        public static readonly string[] All = { NoReEntry, Unlimited, TimeLimited };

        public static bool IsValid(string? policy) =>
            !string.IsNullOrWhiteSpace(policy) && All.Contains(policy, StringComparer.OrdinalIgnoreCase);

        public static string Normalize(string? policy)
        {
            if (string.IsNullOrWhiteSpace(policy)) return NoReEntry;
            return All.FirstOrDefault(p => p.Equals(policy, StringComparison.OrdinalIgnoreCase)) ?? NoReEntry;
        }
    }
}
