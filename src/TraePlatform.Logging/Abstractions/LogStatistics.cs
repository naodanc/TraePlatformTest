namespace TraePlatform.Logging.Abstractions;

/// <summary>趋势采样点。</summary>
/// <param name="Bucket">时间桶起点。</param>
/// <param name="Total">该时间桶内的日志总数。</param>
/// <param name="Errors">该时间桶内的错误（Error 及以上）数量。</param>
public sealed record LogTrendPoint(DateTimeOffset Bucket, long Total, long Errors);

/// <summary>分组统计项。</summary>
/// <param name="Key">分组键。</param>
/// <param name="Count">数量。</param>
public sealed record LogGroup(string Key, long Count);

/// <summary>
/// 日志分析结果。用于“提高问题排查效率”：一眼看到总量、级别分布、模块分布、
/// 错误趋势与高频错误，快速定位问题发生的时间窗口与业务域。
/// </summary>
/// <param name="Total">总条数。</param>
/// <param name="ByLevel">按级别的数量分布。</param>
/// <param name="ByModule">按模块的数量分布。</param>
/// <param name="Trend">按时间桶的趋势分布。</param>
/// <param name="TopErrors">高频错误（按消息模板聚类）Top N。</param>
public sealed record LogStatistics(
    long Total,
    IReadOnlyDictionary<LogLevel, long> ByLevel,
    IReadOnlyDictionary<string, long> ByModule,
    IReadOnlyList<LogTrendPoint> Trend,
    IReadOnlyList<LogGroup> TopErrors);
