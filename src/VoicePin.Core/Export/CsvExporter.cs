using System.Text;
using VoicePin.Core.Models;

namespace VoicePin.Core.Export;

public static class CsvExporter
{
    public static readonly string[] Header = { "구매자 닉네임", "금액", "인식 시각", "원본 문장", "상태" };

    /// <summary>확정 내역만 CSV(UTF-8 BOM)로 생성. 대상이 없으면 null 반환(SP-012: 0건 시 파일 미생성).</summary>
    public static string? BuildCsv(IEnumerable<SalesRecord> records, DateTime from, DateTime to)
    {
        var confirmed = records
            .Where(r => r.Status == SalesStatus.Confirmed && r.RecognizedAt >= from && r.RecognizedAt < to)
            .OrderBy(r => r.RecognizedAt)
            .ToList();

        if (confirmed.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Header));
        foreach (var record in confirmed)
        {
            sb.AppendLine(string.Join(",",
                Escape(record.Nickname),
                record.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Escape(record.RecognizedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                Escape(record.Transcript),
                Escape(record.Status.ToKorean())));
        }
        return sb.ToString();
    }

    public static void WriteFile(string csv, string path)
    {
        File.WriteAllText(path, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}
