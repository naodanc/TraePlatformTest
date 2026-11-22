using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TraePlatform.Logging.Abstractions;
using TraePlatform.Logging.Configuration;

namespace TraePlatform.Logging.Runtime;

/// <summary>
/// 后台清理服务。按各存储自身的保留策略，周期性删除过期日志，控制磁盘占用。
/// </summary>
internal sealed class LogCleanupService : BackgroundService
{
    private readonly IReadOnlyList<ILogStore> _stores;
    private readonly LoggingOptions _options;
    private readonly ILogger<LogCleanupService> _logger;

    public LogCleanupService(
        IEnumerable<ILogStore> stores,
        IOptions<LoggingOptions> options,
        ILogger<LogCleanupService> logger)
    {
        _stores = stores.ToList();
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var store in _stores)
            {
                try
                {
                    var removed = await store.CleanupAsync(DateTimeOffset.Now - store.Retention, stoppingToken)
                        .ConfigureAwait(false);
                    if (removed > 0)
                    {
                        _logger.LogInformation("日志清理：{Store} 过期数据 {Count} 条", store.Name, removed);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "日志清理：{Store} 执行失败", store.Name);
                }
            }

            try
            {
                await Task.Delay(_options.CleanupInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
