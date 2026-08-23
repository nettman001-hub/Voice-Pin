using Microsoft.Data.Sqlite;
using VoicePin.Core.Models;

namespace VoicePin.Infrastructure.Data;

public sealed class Db
{
    private readonly string _connectionString;

    public Db(string? databasePath = null)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoicePin");
        Directory.CreateDirectory(dir);
        var path = databasePath ?? Path.Combine(dir, "voicepin.db");
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        Initialize();
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void Initialize()
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();

        const string schema = """
            CREATE TABLE IF NOT EXISTS broadcast_sessions(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_no TEXT NOT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT
            );
            CREATE TABLE IF NOT EXISTS sales_records(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id INTEGER NOT NULL REFERENCES broadcast_sessions(id),
                nickname TEXT NOT NULL DEFAULT '',
                amount INTEGER NOT NULL DEFAULT 0,
                recognized_at TEXT NOT NULL,
                transcript TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT 'AutoSaved',
                duplicate_suspect INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS captures(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                sale_id INTEGER,
                file_path TEXT NOT NULL,
                area_name TEXT NOT NULL DEFAULT '',
                captured_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS recognition_rules(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                keyword TEXT NOT NULL UNIQUE COLLATE NOCASE,
                actions INTEGER NOT NULL,
                priority INTEGER NOT NULL DEFAULT 0,
                enabled INTEGER NOT NULL DEFAULT 1,
                is_built_in INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS training_phrases(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                text TEXT NOT NULL UNIQUE,
                recording_count INTEGER NOT NULL DEFAULT 0,
                last_trained_at TEXT,
                last_score REAL
            );
            CREATE TABLE IF NOT EXISTS notification_settings(
                event_type TEXT PRIMARY KEY,
                push_enabled INTEGER NOT NULL DEFAULT 1,
                email_enabled INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS export_logs(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                exported_at TEXT NOT NULL,
                range_from TEXT,
                range_to TEXT,
                row_count INTEGER NOT NULL DEFAULT 0
            );
            """;

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = schema;
            cmd.ExecuteNonQuery();
        }

        SeedRules(conn, tx);
        Migrate(tx);
        tx.Commit();
    }

    private static void Migrate(SqliteTransaction tx)
    {
        var conn = tx.Connection!;
        try
        {
            using var alter = conn.CreateCommand();
            alter.Transaction = tx;
            alter.CommandText = "ALTER TABLE training_phrases ADD COLUMN last_score REAL";
            alter.ExecuteNonQuery();
        }
        catch
        {
            // 컬럼이 이미 있으면 무시
        }
    }

    private static void SeedRules(SqliteConnection conn, SqliteTransaction tx)
    {
        using var check = conn.CreateCommand();
        check.Transaction = tx;
        check.CommandText = "SELECT COUNT(*) FROM recognition_rules";
        if (Convert.ToInt64(check.ExecuteScalar()!) > 0)
        {
            return;
        }

        var defaults = new (string Keyword, RuleAction Actions, int Priority, bool BuiltIn)[]
        {
            ("구매확정", RuleAction.SaveSale, 10, false),
            ("구매자", RuleAction.SaveSale, 5, true),
            ("닉네임", RuleAction.SaveSale, 5, true),
            ("가격", RuleAction.SaveSale, 5, true),
            ("금액", RuleAction.SaveSale, 5, true),
            ("캡처", RuleAction.Capture, 8, false),
            ("결제 완료", RuleAction.SaveSale | RuleAction.Capture, 6, false)
        };

        foreach (var (keyword, actions, priority, builtIn) in defaults)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO recognition_rules(keyword, actions, priority, enabled, is_built_in)
                VALUES($kw, $act, $pri, 1, $builtin)
                """;
            cmd.Parameters.AddWithValue("$kw", keyword);
            cmd.Parameters.AddWithValue("$act", (int)actions);
            cmd.Parameters.AddWithValue("$pri", priority);
            cmd.Parameters.AddWithValue("$builtin", builtIn ? 1 : 0);
            cmd.ExecuteNonQuery();
        }
    }
}
