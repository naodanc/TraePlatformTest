namespace TraePlatform.Logging.Abstractions;

/// <summary>
/// 平台日志中使用的约定属性名。业务代码写日志时使用这些键，即可被自动提取为独立列，
/// 便于检索与聚合。
/// </summary>
public static class LogProperties
{
    /// <summary>来源模块/业务域。</summary>
    public const string Module = "Module";

    /// <summary>链路追踪标识。</summary>
    public const string TraceId = "TraceId";

    /// <summary>关联用户标识。</summary>
    public const string UserId = "UserId";
}
