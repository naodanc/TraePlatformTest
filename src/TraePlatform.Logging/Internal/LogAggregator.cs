using TraePlatform.Logging.Abstractions;

namespace TraePlatform.Logging.Internal;

/// <summary>
/// 日志聚合核心。文件存储与数据库存储分别从介质取出记录后，复用同一套聚合逻辑，
/// 保证两种存储下的分析口径一致。
/// </summary>
internal static class LogAggregator
{
    public const int TopErrorLimit = 10;

    public static LogStatistics Aggregate(IReadOnlyList<LogRecord> records, TimeSpan bucket)
    {
        if (bucket <= TimeSpan.Zero) bucket = TimeSpan.FromHours(1);

        var byLevel = records
            .GroupBy(r => r.Level)
            .ToDictionary(g => g.Key, g => (long)g.Count());

        var byModule = records
            .GroupBy(r => string.IsNullOrEmpty(r.Module) ? "(none)" : r.Module!)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => (long)g.Count());

        var topErrors = records
            .Where(r => r.Level >= LogLevel.Error)
            .GroupBy(NormalizeErrorKey)
            .Select(g => new LogGroup(g.Key, g.Count()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Take(TopErrorLimit)
            .ToList();

        return new LogStatistics(records.Count, byLevel, byModule, BuildTrend(records, bucket), topErrors);
    }

    public static List<LogTrendPoint> BuildTrend(IReadOnlyList<LogRecord> records, TimeSpan bucket)
    {
        var trend = new List<LogTrendPoint>();
        if (records.Count == 0) return trend;

        var min = FloorToBucket(records.Min(r => r.Timestamp), bucket);
        var max = records.Max(r => r.Timestamp);

        var lookup = records
            .GroupBy(r => FloorToBucket(r.Timestamp, bucket))
            .ToDictionary(g => g.Key, g => g.ToList());

        for (var t = min; t <= max; t += bucket)
        {
            lookup.TryGetValue(t, out var list);
            var total = list?.Count ?? 0;
            var errors = list?.Count(x => x.Level >= LogLevel.Error) ?? 0;
            trend.Add(new LogTrendPoint(t, total, errors));
        }

        return trend;
    }

    private static DateTimeOffset FloorToBucket(DateTimeOffset time, TimeSpan bucket)
        => new(time.Ticks - time.Ticks % bucket.Ticks, time.Offset);

    private static string NormalizeErrorKey(LogRecord record)
    {
        var key = !string.IsNullOrWhiteSpace(record.MessageTemplate) ? record.MessageTemplate! : record.Message;
        key = key.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return key.Length > 200 ? key[..200] : key;
    }
}
