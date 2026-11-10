using System.Security.Cryptography;
using System.Text;

namespace Intern.API.WeatherMail;

public static class EmailLoginCodes
{
    public static string Create() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    public static string Hash(Guid challengeId, string code, string secret) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes($"weather-email-login:{challengeId}:{code}")));
    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
