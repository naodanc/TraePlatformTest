namespace TraePlatform.Logging.Abstractions;

/// <summary>分页查询结果。</summary>
/// <param name="Items">当前页记录。</param>
/// <param name="Total">满足条件的总条数。</param>
/// <param name="Skip">跳过的条数。</param>
/// <param name="Take">本页请求的条数。</param>
public sealed record LogPage(IReadOnlyList<LogRecord> Items, long Total, int Skip, int Take);
