namespace TraePlatform.Logging.Abstractions;

/// <summary>日志查询排序方式。</summary>
public enum LogSort
{
    /// <summary>按时间正序（排查链路时使用）。</summary>
    TimestampAscending = 0,

    /// <summary>按时间倒序（查看最新日志时使用）。</summary>
    TimestampDescending = 1
}

/// <summary>
/// 日志查询条件。所有条件为“与”关系，支持分页。
/// </summary>
public sealed class LogQuery
{
    /// <summary>起始时间（含）。</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>结束时间（含）。</summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>最低级别（含），用于“只看 Warning 以上”。</summary>
    public LogLevel? MinLevel { get; set; }

    /// <summary>最高级别（含）。</summary>
    public LogLevel? MaxLevel { get; set; }

    /// <summary>来源模块精确匹配（忽略大小写）。</summary>
    public string? Module { get; set; }

    /// <summary>链路标识精确匹配。</summary>
    public string? TraceId { get; set; }

    /// <summary>用户标识精确匹配。</summary>
    public string? UserId { get; set; }

    /// <summary>关键字（对消息、异常、扩展属性做包含匹配，忽略大小写）。</summary>
    public string? Keyword { get; set; }

    /// <summary>跳过条数（分页）。</summary>
    public int Skip { get; set; }

    /// <summary>获取条数（分页）。</summary>
    public int Take { get; set; } = 100;

    /// <summary>排序方式。</summary>
    public LogSort Sort { get; set; } = LogSort.TimestampDescending;
}
