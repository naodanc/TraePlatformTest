using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Extensions.Logging;
using TraePlatform.Logging.Analysis;
using TraePlatform.Logging.Abstractions;
using TraePlatform.Logging.Configuration;
using TraePlatform.Logging.Runtime;
using TraePlatform.Logging.Stores;

namespace TraePlatform.Logging;

/// <summary>
/// 日志模块的依赖注入入口。
/// </summary>
public static class LoggingModule
{
    /// <summary>注册日志模块。</summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configure">日志配置回调。</param>
    public static IServiceCollection AddTraeLogging(
        this IServiceCollection services,
        Action<LoggingOptions>? configure = null)
    {
        var options = new LoggingOptions();
        configure?.Invoke(options);
        return services.AddTraeLogging(options);
    }

    /// <summary>注册日志模块（使用现成配置对象）。</summary>
    public static IServiceCollection AddTraeLogging(this IServiceCollection services, LoggingOptions options)
    {
        services.AddSingleton<IOptions<LoggingOptions>>(Options.Create(options));

        if (options.Storage.HasFlag(LogStorageMode.File))
        {
            services.AddSingleton<ILogStore, FileLogStore>();
        }

        if (options.Storage.HasFlag(LogStorageMode.Sqlite))
        {
            services.AddSingleton<ILogStore, SqliteLogStore>();
        }

        services.AddSingleton<LogPipeline>();
        services.AddSingleton<LogAnalyzer>();
        services.AddHostedService(sp => sp.GetRequiredService<LogPipeline>());

        if (options.EnableBackgroundCleanup)
        {
            services.AddHostedService<LogCleanupService>();
        }

        services.AddLogging();
        services.AddSingleton<ILoggerFactory>(sp =>
            new SerilogLoggerFactory(sp.GetRequiredService<LogPipeline>().CreateLogger(), dispose: true));

        return services;
    }
}
