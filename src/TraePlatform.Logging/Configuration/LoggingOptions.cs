using TraePlatform.Logging.Abstractions;

namespace TraePlatform.Logging.Configuration;

/// <summary>
/// 日志模块总配置。
/// </summary>
public sealed class LoggingOptions
{
    /// <summary>配置节名称，便于绑定 appsettings.json。</summary>
    public const string SectionName = "TraeLogging";

    /// <summary>落地的存储方式（可组合）。默认控制台 + 文件 + 数据库。</summary>
    public LogStorageMode Storage { get; set; } =
        LogStorageMode.Console | LogStorageMode.File | LogStorageMode.Sqlite;

    /// <summary>最低记录级别。</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>控制台输出模板（Serilog 语法）。</summary>
    public string ConsoleTemplate { get; set; } =
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{Module}] {Message:lj}{NewLine}{Exception}";

    /// <summary>文件存储配置。</summary>
    public FileStoreOptions File { get; set; } = new();

    /// <summary>数据库存储配置。</summary>
    public SqliteStoreOptions Sqlite { get; set; } = new();

    /// <summary>是否启用后台定时清理（按保留策略删除过期文件/数据）。</summary>
    public bool EnableBackgroundCleanup { get; set; } = true;

    /// <summary>后台清理的执行间隔。</summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>分析接口默认的时间桶大小。</summary>
    public TimeSpan DefaultAnalysisBucket { get; set; } = TimeSpan.FromHours(1);
}
