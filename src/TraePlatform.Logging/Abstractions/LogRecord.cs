namespace TraePlatform.Logging.Abstractions;

/// <summary>
/// 结构化日志记录，是“文件存储”与“数据库存储”共用的统一载体。
/// 字段设计面向后续的日志分析业务：可按级别、模块、链路、用户维度检索与聚合。
/// </summary>
public sealed class LogRecord
{
    /// <summary>自增主键（数据库存储回填，文件存储无意义）。</summary>
    public long Id { get; set; }

    /// <summary>发生时间（带时区）。</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

    /// <summary>日志级别。</summary>
    public LogLevel Level { get; set; } = LogLevel.Information;

    /// <summary>渲染后的消息文本。</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>原始消息模板，便于对同类日志聚类分析。</summary>
    public string? MessageTemplate { get; set; }

    /// <summary>异常堆栈（完整文本）。</summary>
    public string? Exception { get; set; }

    /// <summary>来源模块/业务域，例如 Auth、Device、Task，用于按模块过滤。</summary>
    public string? Module { get; set; }

    /// <summary>链路追踪标识。一次业务操作/请求的全链路关联，排查问题时最常用的入口。</summary>
    public string? TraceId { get; set; }

    /// <summary>关联用户标识。</summary>
    public string? UserId { get; set; }

    /// <summary>产生日志的机器名（上位机多机部署时用于区分来源）。</summary>
    public string? MachineName { get; set; }

    /// <summary>附加结构化属性（JSON 文本），承载业务自定义字段。</summary>
    public string? Properties { get; set; }
}
