using Microsoft.Data.Sqlite;
using VoicePin.Core.Models;
using VoicePin.Core.Services;

namespace VoicePin.Infrastructure.Data;

public class RecognitionRuleRepository : IRecognitionRuleRepository
{
    private readonly Db _db;

    public RecognitionRuleRepository(Db db) => _db = db;

    public async Task<List<RecognitionRule>> GetAllAsync()
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, keyword, actions, priority, enabled, is_built_in FROM recognition_rules ORDER BY priority DESC, id ASC";
        using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<RecognitionRule>();
        while (await reader.ReadAsync())
        {
            list.Add(new RecognitionRule
            {
                Id = reader.GetInt64(0),
                Keyword = reader.GetString(1),
                Actions = (RuleAction)reader.GetInt64(2),
                Priority = (int)reader.GetInt64(3),
                Enabled = reader.GetInt64(4) == 1,
                IsBuiltIn = reader.GetInt64(5) == 1
            });
        }
        return list;
    }

    public async Task<long> AddAsync(RecognitionRule rule)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO recognition_rules(keyword, actions, priority, enabled, is_built_in)
            VALUES($kw, $act, $pri, $en, 0);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$kw", rule.Keyword.Trim());
        cmd.Parameters.AddWithValue("$act", (int)rule.Actions);
        cmd.Parameters.AddWithValue("$pri", rule.Priority);
        cmd.Parameters.AddWithValue("$en", rule.Enabled ? 1 : 0);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task UpdateAsync(RecognitionRule rule)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE recognition_rules SET keyword=$kw, actions=$act, priority=$pri, enabled=$en WHERE id=$id";
        cmd.Parameters.AddWithValue("$kw", rule.Keyword.Trim());
        cmd.Parameters.AddWithValue("$act", (int)rule.Actions);
        cmd.Parameters.AddWithValue("$pri", rule.Priority);
        cmd.Parameters.AddWithValue("$en", rule.Enabled ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", rule.Id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(long id)
    {
        await using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM recognition_rules WHERE id=$id AND is_built_in=0";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync();
    }
}
