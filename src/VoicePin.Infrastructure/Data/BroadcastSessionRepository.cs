using Microsoft.Data.Sqlite;
using VoicePin.Core.Models;
using VoicePin.Core.Services;

namespace VoicePin.Infrastructure.Data;

public class BroadcastSessionRepository : IBroadcastSessionRepository
{
    private readonly Db _db;

    public BroadcastSessionRepository(Db db) => _db = db;

    public async Task<BroadcastSession> StartAsync(BroadcastSession session)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO broadcast_sessions(session_no, started_at) VALUES($no, $at); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$no", session.SessionNo);
        cmd.Parameters.AddWithValue("$at", session.StartedAt.ToString("O"));
        session.Id = (long)(await cmd.ExecuteScalarAsync())!;
        return session;
    }

    public async Task EndAsync(long sessionId, DateTime endedAt)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE broadcast_sessions SET ended_at=$at WHERE id=$id";
        cmd.Parameters.AddWithValue("$at", endedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$id", sessionId);
        await cmd.ExecuteNonQueryAsync();

        // 종료 시점에 아직 '자동저장' 상태인 건은 확정 가능하도록 그대로 둠 (방송 후 확인 탭에서 처리)
    }

    public async Task<BroadcastSession?> GetLatestAsync()
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, session_no, started_at, ended_at FROM broadcast_sessions ORDER BY started_at DESC LIMIT 1";
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task<BroadcastSession?> GetAsync(long id)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, session_no, started_at, ended_at FROM broadcast_sessions WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    internal static BroadcastSession Map(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        SessionNo = reader.GetString(1),
        StartedAt = DateTime.Parse(reader.GetString(2)),
        EndedAt = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3))
    };
}
