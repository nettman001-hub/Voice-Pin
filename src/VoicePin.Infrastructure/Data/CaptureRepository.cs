using Microsoft.Data.Sqlite;
using VoicePin.Core.Models;
using VoicePin.Core.Services;

namespace VoicePin.Infrastructure.Data;

public class CaptureRepository : ICaptureRepository
{
    private readonly Db _db;

    public CaptureRepository(Db db) => _db = db;

    public async Task<long> AddAsync(CaptureImage capture)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        if (capture.SaleId is null)
        {
            cmd.CommandText = "INSERT INTO captures(sale_id, file_path, area_name, captured_at) VALUES(NULL, $fp, $an, $at); SELECT last_insert_rowid();";
        }
        else
        {
            cmd.CommandText = "INSERT INTO captures(sale_id, file_path, area_name, captured_at) VALUES($sid, $fp, $an, $at); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$sid", capture.SaleId.Value);
        }
        cmd.Parameters.AddWithValue("$fp", capture.FilePath);
        cmd.Parameters.AddWithValue("$an", capture.AreaName);
        cmd.Parameters.AddWithValue("$at", capture.CapturedAt.ToString("O"));
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<List<CaptureImage>> GetBySaleIdAsync(long saleId)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, sale_id, file_path, area_name, captured_at FROM captures WHERE sale_id=$sid ORDER BY captured_at ASC";
        cmd.Parameters.AddWithValue("$sid", saleId);
        using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<CaptureImage>();
        while (await reader.ReadAsync())
        {
            list.Add(Map(reader));
        }
        return list;
    }

    public async Task<CaptureImage?> GetLatestAsync()
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, sale_id, file_path, area_name, captured_at FROM captures ORDER BY captured_at DESC LIMIT 1";
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task<int> CountSinceAsync(DateTime from)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM captures WHERE captured_at >= $at";
        cmd.Parameters.AddWithValue("$at", from.ToString("O"));
        return (int)Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    public async Task DeleteAsync(long id)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM captures WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    internal static CaptureImage Map(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        SaleId = reader.IsDBNull(1) ? null : reader.GetInt64(1),
        FilePath = reader.GetString(2),
        AreaName = reader.GetString(3),
        CapturedAt = DateTime.Parse(reader.GetString(4))
    };
}
