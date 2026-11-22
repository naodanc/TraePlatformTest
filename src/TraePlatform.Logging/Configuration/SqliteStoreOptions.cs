namespace TraePlatform.Logging.Configuration;

/// <summary>
/// 数据库（SQLite）存储配置。针对大数据量场景，采用 WAL 模式、批量事务写入、
/// 关键字段索引、按时间清理等策略。
/// </summary>
public sealed class SqliteStoreOptions
{
    /// <summary>数据库文件路径。</summary>
    public string DatabasePath { get; set; } = "logs/trae-logs.db";

    /// <summary>日志表名。</summary>
    public string TableName { get; set; } = "LogRecords";

    /// <summary>批量写入条数上限。</summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>批量写入的最大等待间隔（毫秒），避免低峰期日志迟迟不落库。</summary>
    public int FlushIntervalMs { get; set; } = 1000;

    /// <summary>写入缓冲区容量（条）。缓冲区满时丢弃新日志并计数，保证业务线程不被阻塞。</summary>
    public int ChannelCapacity { get; set; } = 100_000;

    /// <summary>数据保留天数。</summary>
    public int RetentionDays { get; set; } = 30;
}
