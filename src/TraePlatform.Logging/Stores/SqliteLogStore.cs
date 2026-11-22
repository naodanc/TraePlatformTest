using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using TraePlatform.Logging.Abstractions;
using TraePlatform.Logging.Configuration;
using TraePlatform.Logging.Internal;

namespace TraePlatform.Logging.Stores;

/// <summary>
/// 数据库日志存储（本地 SQLite）。
/// <para>写入：开启 WAL，批量事务插入，配合上层缓冲实现面向大数据量的高吞吐落库。</para>
/// <para>读取/分析：对时间、级别、模块、链路建立索引，聚合统计尽量下推到 SQL 执行。</para>
/// <para>清理：按保留天数删除过期数据。</para>
/// </summary>
public sealed class SqliteLogStore : ILogStore
{
    private const string TimeFormat = "yyyy-MM-ddTHH:mm:ss.fff";

    private readonly SqliteStoreOptions _options;
    private readonly string _connectionString;

    public SqliteLogStore(IOptions<LoggingOptions> options)
    {
        _options = options.Value.Sqlite;

        var directory = Path.GetDirectoryName(Path.GetFullPath(_options.DatabasePath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _options.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        }.ToString();

        Initialize();
    }

    public string Name => "Sqlite";

    public TimeSpan Retention => TimeSpan.FromDays(Math.Max(1, _options.RetentionDays));

    public async Task WriteAsync(IReadOnlyList<LogRecord> records, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0) return;

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            INSERT INTO {_options.TableName}
                (Timestamp, Level, LevelName, Message, MessageTemplate, Exception, Module, TraceId, UserId, MachineName, Properties)
            VALUES
                ($ts, $level, $levelName, $msg, $tpl, $ex, $module, $trace, $user, $machine, $props);
            """;

        var pTs = AddParameter(command, "$ts");
        var pLevel = AddParameter(command, "$level");
        var pLevelName = AddParameter(command, "$levelName");
        var pMsg = AddParameter(command, "$msg");
        var pTpl = AddParameter(command, "$tpl");
        var pEx = AddParameter(command, "$ex");
        var pModule = AddParameter(command, "$module");
        var pTrace = AddParameter(command, "$trace");
        var pUser = AddParameter(command, "$user");
        var pMachine = AddParameter(command, "$machine");
        var pProps = AddParameter(command, "$props");

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pTs.Value = ToUtcText(record.Timestamp);
            pLevel.Value = (int)record.Level;
            pLevelName.Value = record.Level.ToString();
            pMsg.Value = record.Message;
            pTpl.Value = (object?)record.MessageTemplate ?? DBNull.Value;
            pEx.Value = (object?)record.Exception ?? DBNull.Value;
            pModule.Value = (object?)record.Module ?? DBNull.Value;
            pTrace.Value = (object?)record.TraceId ?? DBNull.Value;
            pUser.Value = (object?)record.UserId ?? DBNull.Value;
            pMachine.Value = (object?)record.MachineName ?? DBNull.Value;
            pProps.Value = (object?)record.Properties ?? DBNull.Value;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<LogPage> QueryAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        var (conditions, parameters) = BuildConditions(query);
        var where = conditions.Length > 0 ? " WHERE " + conditions : "";
        var order = query.Sort == LogSort.TimestampDescending ? "DESC" : "ASC";
        var take = query.Take <= 0 ? 100 : query.Take;
        var skip = Math.Max(0, query.Skip);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        long total;
        await using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = $"SELECT COUNT(*) FROM {_options.TableName}{where};";
            Bind(countCommand, parameters);
            total = Convert.ToInt64(await countCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }

        var items = new List<LogRecord>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT Id, Timestamp, Level, LevelName, Message, MessageTemplate, Exception,
                       Module, TraceId, UserId, MachineName, Properties
                FROM {_options.TableName}{where}
                ORDER BY Timestamp {order}, Id {order}
                LIMIT $take OFFSET $skip;
                """;
            Bind(command, parameters);
            command.Parameters.AddWithValue("$take", take);
            command.Parameters.AddWithValue("$skip", skip);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(ReadRecord(reader));
            }
        }

        return new LogPage(items, total, skip, take);
    }

    public async Task<LogStatistics> AnalyzeAsync(LogQuery query, TimeSpan bucket, CancellationToken cancellationToken = default)
    {
        if (bucket <= TimeSpan.Zero) bucket = TimeSpan.FromHours(1);
        var bucketSeconds = Math.Max(1, (long)bucket.TotalSeconds);

        var (conditions, parameters) = BuildConditions(query);
        var where = conditions.Length > 0 ? " WHERE " + conditions : "";

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var byLevel = new Dictionary<LogLevel, long>();
        long total = 0;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"SELECT Level, COUNT(*) FROM {_options.TableName}{where} GROUP BY Level;";
            Bind(command, parameters);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var count = reader.GetInt64(1);
                byLevel[(LogLevel)reader.GetInt32(0)] = count;
                total += count;
            }
        }

        var byModule = new Dictionary<string, long>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT IFNULL(Module, '(none)') AS M, COUNT(*) AS C
                FROM {_options.TableName}{where}
                GROUP BY M ORDER BY C DESC;
                """;
            Bind(command, parameters);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                byModule[reader.GetString(0)] = reader.GetInt64(1);
            }
        }

        var topErrors = new List<LogGroup>();
        await using (var command = connection.CreateCommand())
        {
            var errorWhere = CombineWhere(conditions, $"Level >= {(int)LogLevel.Error}");
            command.CommandText = $"""
                SELECT IFNULL(NULLIF(MessageTemplate, ''), Message) AS K, COUNT(*) AS C
                FROM {_options.TableName} WHERE {errorWhere}
                GROUP BY K ORDER BY C DESC LIMIT {LogAggregator.TopErrorLimit};
                """;
            Bind(command, parameters);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                topErrors.Add(new LogGroup(Truncate(reader.GetString(0)), reader.GetInt64(1)));
            }
        }

        var trend = new List<LogTrendPoint>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT (CAST(strftime('%s', Timestamp) AS INTEGER) / $bucket) * $bucket AS B,
                       COUNT(*) AS Total,
                       SUM(CASE WHEN Level >= $err THEN 1 ELSE 0 END) AS Errors
                FROM {_options.TableName}{where}
                GROUP BY B ORDER BY B;
                """;
            Bind(command, parameters);
            command.Parameters.AddWithValue("$bucket", bucketSeconds);
            command.Parameters.AddWithValue("$err", (int)LogLevel.Error);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var start = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)).ToLocalTime();
                trend.Add(new LogTrendPoint(start, reader.GetInt64(1), reader.GetInt64(2)));
            }
        }

        return new LogStatistics(total, byLevel, byModule, trend, topErrors);
    }

    public async Task<long> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {_options.TableName} WHERE Timestamp < $cutoff;";
        command.Parameters.AddWithValue("$cutoff", ToUtcText(olderThan));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS {_options.TableName} (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp       TEXT    NOT NULL,
                Level           INTEGER NOT NULL,
                LevelName       TEXT    NOT NULL,
                Message         TEXT    NOT NULL,
                MessageTemplate TEXT    NULL,
                Exception       TEXT    NULL,
                Module          TEXT    NULL,
                TraceId         TEXT    NULL,
                UserId          TEXT    NULL,
                MachineName     TEXT    NULL,
                Properties      TEXT    NULL
            );
            CREATE INDEX IF NOT EXISTS IX_{_options.TableName}_Timestamp ON {_options.TableName}(Timestamp);
            CREATE INDEX IF NOT EXISTS IX_{_options.TableName}_Level     ON {_options.TableName}(Level);
            CREATE INDEX IF NOT EXISTS IX_{_options.TableName}_Module    ON {_options.TableName}(Module);
            CREATE INDEX IF NOT EXISTS IX_{_options.TableName}_TraceId   ON {_options.TableName}(TraceId);
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA busy_timeout=5000; PRAGMA synchronous=NORMAL;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA busy_timeout=5000; PRAGMA synchronous=NORMAL;";
        await pragma.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static SqliteParameter AddParameter(SqliteCommand command, string name)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        command.Parameters.Add(parameter);
        return parameter;
    }

    private static void Bind(SqliteCommand command, IReadOnlyList<(string Name, object Value)> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
    }

    private static (string Conditions, List<(string Name, object Value)> Parameters) BuildConditions(LogQuery query)
    {
        var conditions = new List<string>();
        var parameters = new List<(string, object)>();

        if (query.From is { } from)
        {
            conditions.Add("Timestamp >= $from");
            parameters.Add(("$from", ToUtcText(from)));
        }
        if (query.To is { } to)
        {
            conditions.Add("Timestamp <= $to");
            parameters.Add(("$to", ToUtcText(to)));
        }
        if (query.MinLevel is { } min)
        {
            conditions.Add("Level >= $min");
            parameters.Add(("$min", (int)min));
        }
        if (query.MaxLevel is { } max)
        {
            conditions.Add("Level <= $max");
            parameters.Add(("$max", (int)max));
        }
        if (!string.IsNullOrEmpty(query.Module))
        {
            conditions.Add("Module = $module COLLATE NOCASE");
            parameters.Add(("$module", query.Module));
        }
        if (!string.IsNullOrEmpty(query.TraceId))
        {
            conditions.Add("TraceId = $trace");
            parameters.Add(("$trace", query.TraceId));
        }
        if (!string.IsNullOrEmpty(query.UserId))
        {
            conditions.Add("UserId = $user");
            parameters.Add(("$user", query.UserId));
        }
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            conditions.Add(
                "(Message LIKE $kw ESCAPE '\\' OR Exception LIKE $kw ESCAPE '\\' OR Properties LIKE $kw ESCAPE '\\')");
            parameters.Add(("$kw", "%" + EscapeLike(query.Keyword!) + "%"));
        }

        return (string.Join(" AND ", conditions), parameters);
    }

    private static string CombineWhere(string conditions, string extra)
        => string.IsNullOrEmpty(conditions) ? extra : $"{conditions} AND {extra}";

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static string Truncate(string value)
        => value.Length > 200 ? value[..200] : value;

    private static string ToUtcText(DateTimeOffset value)
        => value.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string text)
        => DateTimeOffset
            .Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
            .ToLocalTime();

    private static LogRecord ReadRecord(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Timestamp = ParseTimestamp(reader.GetString(1)),
        Level = (LogLevel)reader.GetInt32(2),
        Message = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
        MessageTemplate = reader.IsDBNull(5) ? null : reader.GetString(5),
        Exception = reader.IsDBNull(6) ? null : reader.GetString(6),
        Module = reader.IsDBNull(7) ? null : reader.GetString(7),
        TraceId = reader.IsDBNull(8) ? null : reader.GetString(8),
        UserId = reader.IsDBNull(9) ? null : reader.GetString(9),
        MachineName = reader.IsDBNull(10) ? null : reader.GetString(10),
        Properties = reader.IsDBNull(11) ? null : reader.GetString(11)
    };
}
