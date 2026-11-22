using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using TraePlatform.Logging.Abstractions;
using TraePlatform.Logging.Configuration;
using TraePlatform.Logging.Internal;

namespace TraePlatform.Logging.Runtime;

/// <summary>
/// 日志写入管道。既作为 Serilog 的 Sink 接收日志事件，又作为后台服务把日志
/// 按批量、异步的方式分发到各个存储，从而在高并发/大数据量下不阻塞业务线程。
/// </summary>
public sealed class LogPipeline : ILogEventSink, IHostedService, IAsyncDisposable
{
    private readonly LoggingOptions _options;
    private readonly IReadOnlyList<ILogStore> _stores;
    private readonly Channel<LogRecord> _channel;
    private readonly CancellationTokenSource _cts = new();

    private Task? _consumer;
    private long _dropped;
    private int _stopped;

    public LogPipeline(IOptions<LoggingOptions> options, IEnumerable<ILogStore> stores)
    {
        _options = options.Value;
        _stores = stores.ToList();
        _channel = Channel.CreateBounded<LogRecord>(new BoundedChannelOptions(Math.Max(1024, _options.Sqlite.ChannelCapacity))
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>缓冲区满而丢弃的日志条数（用于观测写入压力）。</summary>
    public long DroppedCount => Interlocked.Read(ref _dropped);

    /// <summary>等待缓冲区中的日志全部落地，通常用于查询前或关停前强制刷新。</summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        while (_channel.Reader.Count > 0)
        {
            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        }
        await Task.Delay(Math.Max(100, _options.Sqlite.FlushIntervalMs), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>构建已挂载本管道的 Serilog 日志器。</summary>
    public Serilog.ILogger CreateLogger()
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Is(LogRecordMapper.MapLevel(_options.MinimumLevel))
            .Enrich.FromLogContext();

        if (_options.Storage.HasFlag(LogStorageMode.Console))
        {
            configuration = configuration.WriteTo.Console(outputTemplate: _options.ConsoleTemplate);
        }

        configuration = configuration.WriteTo.Sink(this);
        return configuration.CreateLogger();
    }

    public void Emit(LogEvent logEvent)
    {
        if (!_channel.Writer.TryWrite(LogRecordMapper.Create(logEvent)))
        {
            Interlocked.Increment(ref _dropped);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _consumer = Task.Run(() => ConsumeAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // 宿主停止与最终释放可能先后调用本方法，这里保证只真正执行一次。
        if (Interlocked.Exchange(ref _stopped, 1) == 1)
        {
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);
        if (_consumer is not null)
        {
            try
            {
                await _consumer.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 正常停止。
            }
        }
        await DrainRemainingAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _cts.Dispose();
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        var maxBatch = Math.Max(1, _options.Sqlite.BatchSize);
        var interval = TimeSpan.FromMilliseconds(Math.Max(50, _options.Sqlite.FlushIntervalMs));

        while (!cancellationToken.IsCancellationRequested)
        {
            using var wakeup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            wakeup.CancelAfter(interval);
            try
            {
                if (!await _channel.Reader.WaitToReadAsync(wakeup.Token).ConfigureAwait(false))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // 到达刷新间隔但暂无数据，进入下一轮。
            }

            var batch = new List<LogRecord>(maxBatch);
            while (batch.Count < maxBatch && _channel.Reader.TryRead(out var record))
            {
                batch.Add(record);
            }

            if (batch.Count > 0)
            {
                await DispatchAsync(batch, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task DrainRemainingAsync()
    {
        var batch = new List<LogRecord>();
        while (_channel.Reader.TryRead(out var record))
        {
            batch.Add(record);
        }

        if (batch.Count > 0)
        {
            await DispatchAsync(batch, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task DispatchAsync(IReadOnlyList<LogRecord> batch, CancellationToken cancellationToken)
    {
        foreach (var store in _stores)
        {
            try
            {
                await store.WriteAsync(batch, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 任一存储写入失败不得影响其他存储与业务。
                Trace.TraceError($"日志存储 {store.Name} 写入失败：{ex}");
            }
        }
    }
}
