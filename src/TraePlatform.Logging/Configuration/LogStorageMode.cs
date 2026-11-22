namespace TraePlatform.Logging.Configuration;

/// <summary>
/// 日志落地的存储方式。可组合，例如同时输出到控制台、文件与数据库。
/// </summary>
[Flags]
public enum LogStorageMode
{
    /// <summary>不落任何存储（仅构建管道）。</summary>
    None = 0,

    /// <summary>输出到控制台。</summary>
    Console = 1,

    /// <summary>输出到文件（JSON Lines，按天/体积滚动）。</summary>
    File = 2,

    /// <summary>输出到数据库（本地 SQLite，批量写入）。</summary>
    Sqlite = 4,

    /// <summary>文件 + 数据库双写。</summary>
    FileAndSqlite = File | Sqlite
}
