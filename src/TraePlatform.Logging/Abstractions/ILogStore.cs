namespace TraePlatform.Logging.Abstractions;

/// <summary>
/// 日志存储抽象。文件存储与数据库存储均实现该接口，从而对上层（查询/分析/清理）
/// 屏蔽底层介质差异。
/// </summary>
public interface ILogStore
{
    /// <summary>存储名称，例如 File、Sqlite。</summary>
    string Name { get; }

    /// <summary>该存储的保留时长，供后台清理使用。</summary>
    TimeSpan Retention { get; }

    /// <summary>批量写入。实现需保证批量/异步写入以适配大数据量场景。</summary>
    Task WriteAsync(IReadOnlyList<LogRecord> records, CancellationToken cancellationToken = default);

    /// <summary>条件分页查询。</summary>
    Task<LogPage> QueryAsync(LogQuery query, CancellationToken cancellationToken = default);

    /// <summary>按条件聚合分析。</summary>
    /// <param name="query">过滤条件。</param>
    /// <param name="bucket">趋势统计的时间桶大小。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<LogStatistics> AnalyzeAsync(LogQuery query, TimeSpan bucket, CancellationToken cancellationToken = default);

    /// <summary>清理早于指定时间的记录（或文件），返回清理条数。</summary>
    Task<long> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}
