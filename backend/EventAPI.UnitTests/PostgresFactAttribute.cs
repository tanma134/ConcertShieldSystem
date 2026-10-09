using Xunit;
namespace EventAPI.UnitTests;
// Thiếu PostgreSQL được báo Skipped, không coi là kiểm thử integration đã pass.
public class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CONCERTSHIELD_TEST_POSTGRES"))) Skip = "Set CONCERTSHIELD_TEST_POSTGRES to a disposable PostgreSQL server connection with CREATE DATABASE permission."; }
}
