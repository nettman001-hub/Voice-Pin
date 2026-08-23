using Microsoft.Data.Sqlite;
using VoicePin.Core.Models;
using VoicePin.Core.Services;

namespace VoicePin.Infrastructure.Data;

public class TrainingRepository : ITrainingRepository
{
    private static readonly string[] DefaultPhrases =
    {
        "구매 확정됐습니다",
        "구매하신 분은 [닉네임]님입니다",
        "가격은 [금액]원입니다",
        "결제 완료되셨습니다",
        "캡처 부탁드립니다"
    };

    private readonly Db _db;

    public TrainingRepository(Db db) => _db = db;

    public async Task<List<TrainingPhrase>> GetAllAsync()
    {
        await SeedIfEmpty();
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, text, recording_count, last_trained_at, last_score FROM training_phrases ORDER BY id ASC";
        using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<TrainingPhrase>();
        while (await reader.ReadAsync())
        {
            list.Add(new TrainingPhrase
            {
                Id = reader.GetInt64(0),
                Text = reader.GetString(1),
                RecordingCount = (int)reader.GetInt64(2),
                LastTrainedAt = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                LastScore = reader.IsDBNull(4) ? null : reader.GetDouble(4)
            });
        }
        return list;
    }

    public async Task<long> AddAsync(TrainingPhrase phrase)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO training_phrases(text) VALUES($t); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$t", phrase.Text.Trim());
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task IncrementRecordingAsync(long phraseId, DateTime trainedAt, double? score = null)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE training_phrases
            SET recording_count = recording_count + 1,
                last_trained_at = CASE WHEN last_trained_at IS NULL OR last_trained_at < $at THEN $at ELSE last_trained_at END,
                last_score = COALESCE($score, last_score)
            WHERE id=$id
            """;
        cmd.Parameters.AddWithValue("$at", trainedAt.ToString("O"));
        if (score is null)
        {
            cmd.Parameters.AddWithValue("$score", DBNull.Value);
        }
        else
        {
            cmd.Parameters.AddWithValue("$score", score.Value);
        }
        cmd.Parameters.AddWithValue("$id", phraseId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(long id)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM training_phrases WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SeedIfEmpty()
    {
        await using var conn = _db.Open();
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM training_phrases";
        if (Convert.ToInt64(await check.ExecuteScalarAsync()) > 0)
        {
            return;
        }
        foreach (var text in DefaultPhrases)
        {
            using var insert = conn.CreateCommand();
            insert.CommandText = "INSERT INTO training_phrases(text) VALUES($t)";
            insert.Parameters.AddWithValue("$t", text);
            await insert.ExecuteNonQueryAsync();
        }
    }
}
