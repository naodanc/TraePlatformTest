using System.Text.Json;
using Serilog.Events;
using TraePlatform.Logging.Abstractions;

namespace TraePlatform.Logging.Internal;

/// <summary>把 Serilog 的日志事件映射为平台统一的结构化日志记录。</summary>
internal static class LogRecordMapper
{
    public static LogRecord Create(LogEvent e)
    {
        var record = new LogRecord
        {
            Timestamp = e.Timestamp,
            Level = MapLevel(e.Level),
            Message = e.RenderMessage(),
            MessageTemplate = e.MessageTemplate.Text,
            Exception = e.Exception?.ToString(),
            MachineName = Environment.MachineName
        };

        Dictionary<string, string?>? extras = null;
        foreach (var (name, value) in e.Properties)
        {
            var text = ToText(value);
            switch (name)
            {
                case LogProperties.Module:
                    record.Module = text;
                    break;
                case LogProperties.TraceId:
                    record.TraceId = text;
                    break;
                case LogProperties.UserId:
                    record.UserId = text;
                    break;
                default:
                    (extras ??= new Dictionary<string, string?>())[name] = text;
                    break;
            }
        }

        if (extras is { Count: > 0 })
        {
            record.Properties = JsonSerializer.Serialize(extras);
        }

        return record;
    }

    private static string? ToText(LogEventPropertyValue value) => value switch
    {
        ScalarValue s => s.Value?.ToString(),
        _ => value.ToString()
    };

    public static LogLevel MapLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => LogLevel.Verbose,
        LogEventLevel.Debug => LogLevel.Debug,
        LogEventLevel.Information => LogLevel.Information,
        LogEventLevel.Warning => LogLevel.Warning,
        LogEventLevel.Error => LogLevel.Error,
        LogEventLevel.Fatal => LogLevel.Fatal,
        _ => LogLevel.Information
    };

    public static LogEventLevel MapLevel(LogLevel level) => level switch
    {
        LogLevel.Verbose => LogEventLevel.Verbose,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Information => LogEventLevel.Information,
        LogLevel.Warning => LogEventLevel.Warning,
        LogLevel.Error => LogEventLevel.Error,
        LogLevel.Fatal => LogEventLevel.Fatal,
        _ => LogEventLevel.Information
    };
}
