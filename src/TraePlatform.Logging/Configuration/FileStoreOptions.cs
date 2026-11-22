namespace TraePlatform.Logging.Configuration;

/// <summary>
/// 文件存储配置。针对大数据量场景，采用「按天 + 按体积」滚动、JSON Lines 追加写、
/// 按保留天数清理 的策略。
/// </summary>
public sealed class FileStoreOptions
{
    /// <summary>日志目录。</summary>
    public string Directory { get; set; } = "logs";

    /// <summary>文件名前缀。</summary>
    public string FilePrefix { get; set; } = "log";

    /// <summary>文件扩展名。</summary>
    public string Extension { get; set; } = ".jsonl";

    /// <summary>单文件体积上限（字节），超过则滚动到下一个序号文件。</summary>
    public long MaxFileSizeBytes { get; set; } = 64L * 1024 * 1024;

    /// <summary>文件保留天数。</summary>
    public int RetentionDays { get; set; } = 30;
}
