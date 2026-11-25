namespace AdminAPI.Repositories;

/// <summary>Một phiên bản chính sách trong bảng system_policies (admin_db).</summary>
public sealed record SystemPolicyRecord(
    int PolicyId,
    string Category,
    string VersionCode,
    int VersionNumber,
    string Title,
    string Scope,
    string ChangeSummary,
    string Content,
    string Status,
    DateTime EffectiveDate,
    DateTime CreatedAt,
    int CreatedByAdminId,
    string CreatedByAdminName,
    decimal PlatformFeePercent = 5.0m,
    int PayoutDays = 7);

/// <summary>Dữ liệu để publish một phiên bản chính sách mới.</summary>
public sealed record NewSystemPolicyVersion(
    string Category,
    string VersionCode,
    int VersionNumber,
    string Title,
    string Scope,
    string ChangeSummary,
    string Content,
    DateTime EffectiveDate,
    int AdminId,
    string AdminName,
    decimal PlatformFeePercent = 5.0m,
    int PayoutDays = 7);

public interface ISystemPolicyRepository
{
    Task<List<SystemPolicyRecord>> GetHistoryAsync(string category, CancellationToken ct = default);

    Task<List<SystemPolicyRecord>> GetActiveAsync(CancellationToken ct = default);

    Task<int> GetNextVersionNumberAsync(string category, CancellationToken ct = default);

    /// <summary>Archive các bản Active cũ và chèn bản mới trong cùng một transaction.</summary>
    Task<(int PolicyId, DateTime CreatedAt)> PublishAsync(NewSystemPolicyVersion version,
        CancellationToken ct = default);
}

/// <summary>
/// Truy vấn bảng system_policies bằng SQL trực tiếp trên admin_db.
/// Bảng này không nằm trong AdminDbContext (không có entity), nên được giữ riêng ở
/// repository thay vì rải NpgsqlConnection trong controller.
/// </summary>
public sealed class SystemPolicyRepository(IConfiguration configuration) : ISystemPolicyRepository
{
    private readonly string _connectionString =
        configuration.GetConnectionString("DefaultConnection")
        ?? "Host=localhost;Port=5432;Database=admin_db;Username=postgres;Password=123456";

    public async Task<List<SystemPolicyRecord>> GetHistoryAsync(string category, CancellationToken ct = default)
    {
        var items = new List<SystemPolicyRecord>();

        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT policy_id, category, version_code, version_number, title, scope, change_summary, content,
                   platform_fee_percent, payout_days, status, effective_date, created_at, created_by_admin_id, created_by_admin_name
            FROM system_policies
            WHERE category = @cat
            ORDER BY version_number DESC, created_at DESC;";

        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("cat", category);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(Map(reader));

        return items;
    }

    public async Task<List<SystemPolicyRecord>> GetActiveAsync(CancellationToken ct = default)
    {
        var items = new List<SystemPolicyRecord>();

        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT DISTINCT ON (category)
                   policy_id, category, version_code, version_number, title, scope, change_summary, content,
                   platform_fee_percent, payout_days, status, effective_date, created_at, created_by_admin_id, created_by_admin_name
            FROM system_policies
            WHERE LOWER(status) = 'active'
            ORDER BY category, version_number DESC, policy_id DESC;";

        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(Map(reader));

        return items;
    }

    public async Task<int> GetNextVersionNumberAsync(string category, CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = "SELECT COALESCE(MAX(version_number), 0) FROM system_policies WHERE category = @cat;";
        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("cat", category);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) + 1;
    }

    public async Task<(int PolicyId, DateTime CreatedAt)> PublishAsync(NewSystemPolicyVersion version,
        CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // 1. Archive previous active versions for this category
        const string archiveSql = "UPDATE system_policies SET status = 'Archived' WHERE category = @cat AND LOWER(status) = 'active';";
        await using (var cmd = new Npgsql.NpgsqlCommand(archiveSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("cat", version.Category);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // 2. Insert new policy version
        const string insertSql = @"
            INSERT INTO system_policies (category, version_code, version_number, title, scope, change_summary, content,
                                        platform_fee_percent, payout_days, status, effective_date, created_at, created_by_admin_id, created_by_admin_name)
            VALUES (@cat, @code, @num, @title, @scope, @sum, @content, @fee, @payout, 'Active', @eff, NOW(), @aid, @aname)
            RETURNING policy_id, created_at;";

        int policyId;
        DateTime createdAt;
        await using (var cmd = new Npgsql.NpgsqlCommand(insertSql, conn, tx))
        {
            cmd.Parameters.AddWithValue("cat", version.Category);
            cmd.Parameters.AddWithValue("code", version.VersionCode);
            cmd.Parameters.AddWithValue("num", version.VersionNumber);
            cmd.Parameters.AddWithValue("title", version.Title);
            cmd.Parameters.AddWithValue("scope", version.Scope);
            cmd.Parameters.AddWithValue("sum", version.ChangeSummary);
            cmd.Parameters.AddWithValue("content", version.Content);
            cmd.Parameters.AddWithValue("fee", version.PlatformFeePercent);
            cmd.Parameters.AddWithValue("payout", version.PayoutDays);
            cmd.Parameters.AddWithValue("eff", version.EffectiveDate);
            cmd.Parameters.AddWithValue("aid", version.AdminId);
            cmd.Parameters.AddWithValue("aname", version.AdminName);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                throw new InvalidOperationException("Failed to insert policy version into database.");

            policyId = Convert.ToInt32(reader["policy_id"]);
            createdAt = Convert.ToDateTime(reader["created_at"]);
        }

        await tx.CommitAsync(ct);
        return (policyId, createdAt);
    }

    private static SystemPolicyRecord Map(Npgsql.NpgsqlDataReader reader) => new(
        Convert.ToInt32(reader["policy_id"]),
        Convert.ToString(reader["category"]) ?? "",
        Convert.ToString(reader["version_code"]) ?? "",
        Convert.ToInt32(reader["version_number"]),
        Convert.ToString(reader["title"]) ?? "",
        Convert.ToString(reader["scope"]) ?? "",
        Convert.ToString(reader["change_summary"]) ?? "",
        Convert.ToString(reader["content"]) ?? "",
        Convert.ToString(reader["status"]) ?? "Active",
        Convert.ToDateTime(reader["effective_date"]),
        Convert.ToDateTime(reader["created_at"]),
        Convert.ToInt32(reader["created_by_admin_id"]),
        Convert.ToString(reader["created_by_admin_name"]) ?? "Platform Administrator",
        reader["platform_fee_percent"] != DBNull.Value ? Convert.ToDecimal(reader["platform_fee_percent"]) : 5.0m,
        reader["payout_days"] != DBNull.Value ? Convert.ToInt32(reader["payout_days"]) : 7);
}
