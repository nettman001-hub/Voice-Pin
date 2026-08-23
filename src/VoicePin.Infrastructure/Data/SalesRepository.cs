using Microsoft.Data.Sqlite;
using VoicePin.Core.Export;
using VoicePin.Core.Models;
using VoicePin.Core.Services;

namespace VoicePin.Infrastructure.Data;

public class SalesRepository : ISalesRepository
{
    private readonly Db _db;

    public SalesRepository(Db db) => _db = db;

    public async Task<long> AddAsync(SalesRecord record)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sales_records(session_id, nickname, amount, recognized_at, transcript, status, duplicate_suspect)
            VALUES($sid, $nick, $amt, $at, $tr, $st, $dup);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$sid", record.SessionId);
        cmd.Parameters.AddWithValue("$nick", record.Nickname);
        cmd.Parameters.AddWithValue("$amt", record.Amount);
        cmd.Parameters.AddWithValue("$at", record.RecognizedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$tr", record.Transcript);
        cmd.Parameters.AddWithValue("$st", record.Status.ToString());
        cmd.Parameters.AddWithValue("$dup", record.DuplicateSuspect ? 1 : 0);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<SalesRecord?> GetAsync(long id)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, session_id, nickname, amount, recognized_at, transcript, status, duplicate_suspect FROM sales_records WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task<List<SalesRecord>> GetAllAsync()
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, session_id, nickname, amount, recognized_at, transcript, status, duplicate_suspect FROM sales_records ORDER BY recognized_at DESC";
        using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<SalesRecord>();
        while (await reader.ReadAsync())
        {
            list.Add(Map(reader));
        }
        return list;
    }

    public async Task<List<SalesRecord>> GetBySessionAsync(long sessionId)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, session_id, nickname, amount, recognized_at, transcript, status, duplicate_suspect FROM sales_records WHERE session_id=$sid ORDER BY recognized_at ASC";
        cmd.Parameters.AddWithValue("$sid", sessionId);
        using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<SalesRecord>();
        while (await reader.ReadAsync())
        {
            list.Add(Map(reader));
        }
        return list;
    }

    public async Task<List<SalesRecord>> SearchAsync(SalesSearchFilter filter)
    {
        var all = await GetAllAsync();
        IEnumerable<SalesRecord> query = all;

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var q = filter.Query.Trim();
            query = query.Where(r => r.Nickname.Contains(q, StringComparison.OrdinalIgnoreCase)
                                     || r.Transcript.Contains(q, StringComparison.OrdinalIgnoreCase)
                                     || r.Amount.ToString().Contains(q));
        }
        if (filter.Status is not null)
        {
            query = query.Where(r => r.Status == filter.Status);
        }
        if (filter.From is not null)
        {
            query = query.Where(r => r.RecognizedAt >= filter.From);
        }
        if (filter.To is not null)
        {
            query = query.Where(r => r.RecognizedAt < filter.To);
        }

        return (filter.SortBy switch
        {
            "oldest" => query.OrderBy(r => r.RecognizedAt),
            "amount" => query.OrderByDescending(r => r.Amount),
            _ => query.OrderByDescending(r => r.RecognizedAt)
        }).ToList();
    }

    public async Task<bool> ExistsDuplicateAsync(string nickname, long amount, DateTime recognizedAt, int windowSeconds = 60)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM sales_records
            WHERE nickname=$nick AND amount=$amt
              AND ABS(julianday(recognized_at) - julianday($at)) * 86400 <= $win
            """;
        cmd.Parameters.AddWithValue("$nick", nickname);
        cmd.Parameters.AddWithValue("$amt", amount);
        cmd.Parameters.AddWithValue("$at", recognizedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$win", windowSeconds);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task UpdateAsync(SalesRecord record)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE sales_records
            SET nickname=$nick, amount=$amt, recognized_at=$at, status=$st, transcript=$tr
            WHERE id=$id
            """;
        cmd.Parameters.AddWithValue("$nick", record.Nickname);
        cmd.Parameters.AddWithValue("$amt", record.Amount);
        cmd.Parameters.AddWithValue("$at", record.RecognizedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$st", record.Status.ToString());
        cmd.Parameters.AddWithValue("$tr", record.Transcript);
        cmd.Parameters.AddWithValue("$id", record.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(long id)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM sales_records WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public SettlementSummary Summarize(DateTime from, DateTime to)
    {
        var records = GetAllAsync().GetAwaiter().GetResult();
        return SettlementCalculator.Compute(records, from, to);
    }

    internal static SalesRecord Map(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        SessionId = reader.GetInt64(1),
        Nickname = reader.GetString(2),
        Amount = reader.GetInt64(3),
        RecognizedAt = DateTime.Parse(reader.GetString(4)),
        Transcript = reader.GetString(5),
        Status = Enum.TryParse<SalesStatus>(reader.GetString(6), out var st) ? st : SalesStatus.AutoSaved,
        DuplicateSuspect = reader.GetInt64(7) == 1
    };
}
