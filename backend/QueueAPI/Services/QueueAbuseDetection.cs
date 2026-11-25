using System.Security.Cryptography;
using System.Text;

namespace QueueAPI.Services;

public static class QueueAbuseDetection
{
    public const int MultipleRegistrationAttemptsThreshold = 3;
    public const int AutomatedRequestThreshold = 30;
    public const int TokenReplayThreshold = 25;
    public const int AutomatedRequestWindowSeconds = 60;
    public const int MultipleRegistrationWindowSeconds = 300;
    public const int TokenReplayWindowSeconds = 120;
    public const double AutomatedRequestMaxCoefficientOfVariation = 0.10;

    public static string TokenFingerprint(string token) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public static bool HasRegularAutomatedRequestPattern(
        IReadOnlyList<long> timestamps,
        out double coefficientOfVariation)
    {
        coefficientOfVariation = double.PositiveInfinity;
        if (timestamps.Count < AutomatedRequestThreshold)
            return false;

        var recent = timestamps
            .OrderByDescending(timestamp => timestamp)
            .Take(AutomatedRequestThreshold)
            .OrderBy(timestamp => timestamp)
            .ToArray();
        if (recent[^1] - recent[0] > TimeSpan.FromSeconds(AutomatedRequestWindowSeconds).TotalMilliseconds)
            return false;

        var intervals = recent.Zip(recent.Skip(1), (first, second) => (double)(second - first)).ToArray();
        var average = intervals.Average();
        if (average <= 0)
            return false;

        var variance = intervals.Sum(interval => Math.Pow(interval - average, 2)) / intervals.Length;
        coefficientOfVariation = Math.Sqrt(variance) / average;
        return coefficientOfVariation <= AutomatedRequestMaxCoefficientOfVariation;
    }
}
