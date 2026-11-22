using TraePlatform.Logging.Abstractions;

namespace TraePlatform.Logging.Internal;

/// <summary>文件存储使用的内存过滤逻辑，语义与数据库存储的 SQL 过滤保持一致。</summary>
internal static class LogRecordFilter
{
    public static bool Matches(LogRecord r, LogQuery q)
    {
        if (q.From is { } from && r.Timestamp < from) return false;
        if (q.To is { } to && r.Timestamp > to) return false;
        if (q.MinLevel is { } min && r.Level < min) return false;
        if (q.MaxLevel is { } max && r.Level > max) return false;
        if (!string.IsNullOrEmpty(q.Module) &&
            !string.Equals(r.Module, q.Module, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrEmpty(q.TraceId) &&
            !string.Equals(r.TraceId, q.TraceId, StringComparison.Ordinal)) return false;
        if (!string.IsNullOrEmpty(q.UserId) &&
            !string.Equals(r.UserId, q.UserId, StringComparison.Ordinal)) return false;
        if (!string.IsNullOrEmpty(q.Keyword) && !ContainsKeyword(r, q.Keyword!)) return false;
        return true;
    }

    private static bool ContainsKeyword(LogRecord r, string keyword)
    {
        const StringComparison c = StringComparison.OrdinalIgnoreCase;
        return (r.Message?.Contains(keyword, c) ?? false)
            || (r.Exception?.Contains(keyword, c) ?? false)
            || (r.Properties?.Contains(keyword, c) ?? false)
            || (r.Module?.Contains(keyword, c) ?? false)
            || (r.MessageTemplate?.Contains(keyword, c) ?? false);
    }
}
