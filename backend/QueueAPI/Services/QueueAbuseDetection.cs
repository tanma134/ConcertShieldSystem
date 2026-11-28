using System.Security.Cryptography;
using System.Text;

namespace QueueAPI.Services;

public static class QueueAbuseDetection
{
    public const int MultipleRegistrationAttemptsThreshold = 3; //gưỡng 3 lần đăng ký lặp lại.
    public const int AutomatedRequestThreshold = 30; //Cần ít nhất 30 yêu cầu để xét mẫu gửi tự động
    public const int TokenReplayThreshold = 25;//ngưỡng 25 lần thử dùng lại token đã bị thu hồi.
    public const int AutomatedRequestWindowSeconds = 60;//xét các yêu cầu tự động trong 60 giây.
    public const int MultipleRegistrationWindowSeconds = 300;//xét đăng ký lặp lại trong 300 giây (5 phút)
    public const int TokenReplayWindowSeconds = 120;//xét việc dùng lại token trong 120 giây (2 phút).
    public const double AutomatedRequestMaxCoefficientOfVariation = 0.10;//hệ số biến thiên tối đa 0,10 (10%); (CV)
    // yêu cầu phải có khoảng cách thời gian tương đối đều nhau mới bị đánh dấu là mẫu tự động.


    public static string TokenFingerprint(string token) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public static bool HasRegularAutomatedRequestPattern(
        IReadOnlyList<long> timestamps,
        out double coefficientOfVariation)
    {
        //(CV)
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
