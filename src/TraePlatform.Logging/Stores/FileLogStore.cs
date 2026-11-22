using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Options;
using TraePlatform.Logging.Abstractions;
using TraePlatform.Logging.Configuration;
using TraePlatform.Logging.Internal;

namespace TraePlatform.Logging.Stores;

/// <summary>
/// 文件日志存储。
/// <para>写入：JSON Lines 追加写，按「天 + 体积」滚动，单文件到达上限即切分新文件。</para>
/// <para>读取：按查询时间范围定位候选文件后流式解析，避免一次性载入全部文件。</para>
/// <para>清理：按保留天数删除过期文件。</para>
/// </summary>
public sealed class FileLogStore : ILogStore
{
    private readonly FileStoreOptions _options;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _rollSync = new();

    private string? _currentPath;
    private DateOnly _currentDate;
    private int _currentSequence;

    public FileLogStore(IOptions<LoggingOptions> options)
    {
        _options = options.Value.File;
        Directory.CreateDirectory(_options.Directory);
    }

    public string Name => "File";

    public TimeSpan Retention => TimeSpan.FromDays(Math.Max(1, _options.RetentionDays));

    public async Task WriteAsync(IReadOnlyList<LogRecord> records, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0) return;

        var lines = new List<string>(records.Count);
        long payloadBytes = 0;
        foreach (var record in records)
        {
            var line = LogRecordSerializer.Serialize(record) + Environment.NewLine;
            payloadBytes += Encoding.UTF8.GetByteCount(line);
            lines.Add(line);
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = ResolveWritableFile(payloadBytes);
            await using var stream = new FileStream(
                path, FileMode.Append, FileAccess.Write, FileShare.Read, 64 * 1024, useAsync: true);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            foreach (var line in lines)
            {
                await writer.WriteAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<LogPage> QueryAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        var matched = await LoadAsync(query, cancellationToken).ConfigureAwait(false);

        IEnumerable<LogRecord> ordered = query.Sort == LogSort.TimestampDescending
            ? matched.OrderByDescending(r => r.Timestamp)
            : matched.OrderBy(r => r.Timestamp);

        var skip = Math.Max(0, query.Skip);
        var take = query.Take <= 0 ? 100 : query.Take;
        var items = ordered.Skip(skip).Take(take).ToList();

        return new LogPage(items, matched.Count, skip, take);
    }

    public async Task<LogStatistics> AnalyzeAsync(LogQuery query, TimeSpan bucket, CancellationToken cancellationToken = default)
    {
        var matched = await LoadAsync(query, cancellationToken).ConfigureAwait(false);
        return LogAggregator.Aggregate(matched, bucket);
    }

    public Task<long> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        var cutoff = DateOnly.FromDateTime(olderThan.LocalDateTime);
        long removed = 0;

        foreach (var file in EnumerateFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(file, _currentPath, StringComparison.Ordinal)) continue;

            var date = TryParseDate(file);
            if (date is null || date >= cutoff) continue;

            try
            {
                File.Delete(file);
                removed++;
            }
            catch (IOException)
            {
                // 文件被占用时跳过，等待下次清理。
            }
        }

        return Task.FromResult(removed);
    }

    private async Task<List<LogRecord>> LoadAsync(LogQuery query, CancellationToken cancellationToken)
    {
        var result = new List<LogRecord>();
        foreach (var file in EnumerateCandidateFiles(query))
        {
            await foreach (var line in ReadLinesAsync(file, cancellationToken).ConfigureAwait(false))
            {
                var record = LogRecordSerializer.Deserialize(line);
                if (record is null || !LogRecordFilter.Matches(record, query)) continue;
                result.Add(record);
            }
        }
        return result;
    }

    private string ResolveWritableFile(long incomingBytes)
    {
        lock (_rollSync)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (_currentPath is null || _currentDate != today)
            {
                var (sequence, path) = FindLatestForDate(today);
                _currentDate = today;
                if (path is null)
                {
                    _currentSequence = 1;
                    _currentPath = BuildPath(today, 1);
                }
                else
                {
                    _currentSequence = sequence;
                    _currentPath = path;
                }
            }

            var info = new FileInfo(_currentPath!);
            if (info.Exists && info.Length > 0 && info.Length + incomingBytes > _options.MaxFileSizeBytes)
            {
                _currentSequence++;
                _currentPath = BuildPath(_currentDate, _currentSequence);
            }

            return _currentPath!;
        }
    }

    private (int Sequence, string? Path) FindLatestForDate(DateOnly date)
    {
        var pattern = $"{_options.FilePrefix}-{date:yyyyMMdd}-*{_options.Extension}";
        string? latest = null;
        var best = 0;
        foreach (var file in Directory.EnumerateFiles(_options.Directory, pattern))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var parts = name.Split('-');
            if (parts.Length >= 3 && int.TryParse(parts[^1], out var sequence) && sequence >= best)
            {
                best = sequence;
                latest = file;
            }
        }
        return (best, latest);
    }

    private string BuildPath(DateOnly date, int sequence)
        => Path.Combine(_options.Directory, $"{_options.FilePrefix}-{date:yyyyMMdd}-{sequence:D3}{_options.Extension}");

    private IEnumerable<string> EnumerateFiles()
        => Directory.EnumerateFiles(_options.Directory, $"{_options.FilePrefix}-*{_options.Extension}").OrderBy(f => f, StringComparer.Ordinal);

    private IEnumerable<string> EnumerateCandidateFiles(LogQuery query)
    {
        var from = query.From is { } f ? DateOnly.FromDateTime(f.LocalDateTime) : (DateOnly?)null;
        var to = query.To is { } t ? DateOnly.FromDateTime(t.LocalDateTime) : (DateOnly?)null;

        foreach (var file in EnumerateFiles())
        {
            var date = TryParseDate(file);
            if (date is not null)
            {
                if (from is not null && date < from) continue;
                if (to is not null && date > to) continue;
            }
            yield return file;
        }
    }

    private DateOnly? TryParseDate(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        var parts = name.Split('-');
        if (parts.Length >= 3 && DateTime.TryParseExact(parts[^2], "yyyyMMdd", null,
                System.Globalization.DateTimeStyles.None, out var date))
        {
            return DateOnly.FromDateTime(date);
        }
        return null;
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(
        string path, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, useAsync: true);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null) break;
            yield return line;
        }
    }
}
