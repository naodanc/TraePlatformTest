using System.Text.Json;
using System.Text.Json.Serialization;
using TraePlatform.Logging.Abstractions;

namespace TraePlatform.Logging.Internal;

/// <summary>
/// 日志记录的 JSON 序列化工具（文件存储使用 JSON Lines 格式）。
/// </summary>
internal static class LogRecordSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static string Serialize(LogRecord record) => JsonSerializer.Serialize(record, Options);

    public static LogRecord? Deserialize(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        try
        {
            return JsonSerializer.Deserialize<LogRecord>(line, Options);
        }
        catch (JsonException)
        {
            // 容忍残缺/损坏行，避免单行问题影响整体读取。
            return null;
        }
    }
}
