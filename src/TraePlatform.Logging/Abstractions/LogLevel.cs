namespace TraePlatform.Logging.Abstractions;

/// <summary>
/// 日志级别。与具体日志框架解耦，供文件存储与数据库存储统一使用。
/// </summary>
public enum LogLevel
{
    Verbose = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Fatal = 5
}
