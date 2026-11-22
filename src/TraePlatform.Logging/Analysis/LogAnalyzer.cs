using Microsoft.Extensions.Options;
using TraePlatform.Logging.Abstractions;
using TraePlatform.Logging.Configuration;

namespace TraePlatform.Logging.Analysis;

/// <summary>
/// 日志分析外观。面向“提高问题排查效率”的常见场景，提供统一入口：
/// 条件检索、聚合统计、按链路（TraceId）还原问题现场、查看高频错误。
/// </summary>
public sealed class LogAnalyzer
{
    private readonly IReadOnlyList<ILogStore> _stores;
    private readonly LoggingOptions _options;

    public LogAnalyzer(IEnumerable<ILogStore> stores, IOptions<LoggingOptions> options)
    {
        _stores = stores.ToList();
        _options = options.Value;
    }

    /// <summary>可用的存储名称。</summary>
    public IReadOnlyList<string> AvailableStores => _stores.Select(s => s.Name).ToList();

    /// <summary>条件分页检索。<paramref name="store"/> 为空时使用首个已注册存储。</summary>
    public Task<LogPage> QueryAsync(LogQuery query, string? store = null, CancellationToken cancellationToken = default)
        => Resolve(store).QueryAsync(query, cancellationToken);

    /// <summary>聚合统计（级别分布、模块分布、趋势、高频错误）。</summary>
    public Task<LogStatistics> AnalyzeAsync(
        LogQuery? query = null,
        TimeSpan? bucket = null,
        string? store = null,
        CancellationToken cancellationToken = default)
        => Resolve(store).AnalyzeAsync(
            query ?? new LogQuery(),
            bucket ?? _options.DefaultAnalysisBucket,
            cancellationToken);

    /// <summary>按链路标识还原一次业务操作的全部日志（时间正序）。</summary>
    public async Task<IReadOnlyList<LogRecord>> GetTraceAsync(
        string traceId, string? store = null, CancellationToken cancellationToken = default)
    {
        var page = await Resolve(store).QueryAsync(new LogQuery
        {
            TraceId = traceId,
            Take = int.MaxValue,
            Sort = LogSort.TimestampAscending
        }, cancellationToken).ConfigureAwait(false);

        return page.Items;
    }

    /// <summary>高频错误 Top N。</summary>
    public async Task<IReadOnlyList<LogGroup>> TopErrorsAsync(
        LogQuery? query = null, int top = 10, string? store = null, CancellationToken cancellationToken = default)
    {
        var statistics = await AnalyzeAsync(query, store: store, cancellationToken: cancellationToken).ConfigureAwait(false);
        return statistics.TopErrors.Take(top).ToList();
    }

    private ILogStore Resolve(string? name)
    {
        if (_stores.Count == 0)
        {
            throw new InvalidOperationException("未注册任何日志存储，请检查 LoggingOptions.Storage 配置。");
        }

        if (string.IsNullOrEmpty(name))
        {
            return _stores[0];
        }

        return _stores.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException($"未找到名为 {name} 的日志存储。");
    }
}
