using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TraePlatform.Logging;
using TraePlatform.Logging.Abstractions;
using TraePlatform.Logging.Analysis;
using TraePlatform.Logging.Configuration;
using TraePlatform.Logging.Runtime;
using TraePlatform.Users;
using TraePlatform.Users.Authentication;
using TraePlatform.Users.Authorization;
using TraePlatform.Users.Configuration;
using TraePlatform.Users.Domain;
using TraePlatform.Users.Management;
using LogLevel = TraePlatform.Logging.Abstractions.LogLevel;

const string AdminPassword = "Admin@123";
string[] sampleResources =
{
    "System.User", "System.Role", "Log.Viewer", "Device.Monitor", "Device.Parameter", "Alarm.Record"
};

// ---------------------------------------------------------------------------
// 1. 组装平台：日志模块 + 用户模块
// ---------------------------------------------------------------------------
// 每次运行前重置演示数据，保证可重复执行。
var demoRoot = Path.Combine(AppContext.BaseDirectory, "demo");
if (Directory.Exists(demoRoot))
{
    Directory.Delete(demoRoot, recursive: true);
}

var logDirectory = Path.Combine(demoRoot, "logs");
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTraeLogging(new LoggingOptions
{
    Storage = LogStorageMode.Console | LogStorageMode.File | LogStorageMode.Sqlite,
    MinimumLevel = LogLevel.Information,
    File = new FileStoreOptions { Directory = logDirectory },
    Sqlite = new SqliteStoreOptions { DatabasePath = Path.Combine(logDirectory, "trae-logs.db") }
});

builder.Services.AddTraeUsers(new UserModuleOptions
{
    DatabasePath = Path.Combine(demoRoot, "users", "trae-users.db"),
    DefaultAdminPassword = AdminPassword
});

using var host = builder.Build();
await host.StartAsync();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Demo");
var authentication = host.Services.GetRequiredService<IAuthenticationService>();
var authorization = host.Services.GetRequiredService<IAuthorizationService>();
var management = host.Services.GetRequiredService<IUserManagementService>();
var analyzer = host.Services.GetRequiredService<LogAnalyzer>();
var pipeline = host.Services.GetRequiredService<LogPipeline>();

// ---------------------------------------------------------------------------
// 2. 用户模块：登录、鉴权、权限管控、页面能力适配
// ---------------------------------------------------------------------------
Section("二、用户模块");

Section("1) 管理员登录");
var adminLogin = await authentication.LoginAsync("admin", AdminPassword);
Console.WriteLine(adminLogin.Success
    ? $"登录成功：{adminLogin.Session!.DisplayName}，令牌 {adminLogin.Session.Token[..8]}..."
    : $"登录失败：{adminLogin.Message}");
var adminCaps = adminLogin.Session!.Capabilities;
PrintCapabilities("管理员权限（可见/可操作/可编辑/可管理）", adminCaps, sampleResources);

Section("2) 口令错误");
var badLogin = await authentication.LoginAsync("admin", "wrong-password");
Console.WriteLine($"结果：Success={badLogin.Success}，原因={badLogin.Reason}，提示={badLogin.Message}");

Section("3) 创建操作员并对比权限");
var operatorId = await management.CreateUserAsync("operator1", "Op@123456", "操作员一号", new[] { "Operator" });
var operatorLogin = await authentication.LoginAsync("operator1", "Op@123456");
Console.WriteLine($"已创建用户 Id={operatorId}");
PrintCapabilities("操作员权限", operatorLogin.Session!.Capabilities, sampleResources);

Section("4) 权限管控：禁用账号后无法登录");
await management.SetUserStatusAsync(operatorId, UserStatus.Disabled);
var disabledLogin = await authentication.LoginAsync("operator1", "Op@123456");
Console.WriteLine($"结果：Success={disabledLogin.Success}，原因={disabledLogin.Reason}，提示={disabledLogin.Message}");
await management.SetUserStatusAsync(operatorId, UserStatus.Active);
Console.WriteLine("已恢复账号为正常状态。");

Section("5) 单点权限校验");
Console.WriteLine($"操作员能否「编辑设备参数」：{await authorization.HasPermissionAsync(operatorId, "Device.Parameter", PermissionAction.Edit)}");
Console.WriteLine($"操作员能否「查看设备参数」：{await authorization.HasPermissionAsync(operatorId, "Device.Parameter", PermissionAction.View)}");
Console.WriteLine($"管理员能否「管理用户」：{await authorization.HasPermissionAsync(adminCaps.UserId, "System.User", PermissionAction.Manage)}");

// ---------------------------------------------------------------------------
// 3. 日志模块：写入（结构化 + 模块 + 链路）与日志分析
// ---------------------------------------------------------------------------
Section("三、日志模块");

Section("1) 写入日志（模拟设备采集，含模块/链路/用户维度）");
var random = new Random(42);
const int total = 2000;
for (var i = 0; i < total; i++)
{
    using var scope = LogContextScope.Push(module: "Device", traceId: $"trk-{i / 50:D4}", userId: "admin");
    var deviceId = $"PLC-{i % 10:D2}";

    if (i % 97 == 0)
    {
        logger.LogError("设备 {DeviceId} 通信超时，已重试 {Retry} 次", deviceId, 3);
    }
    else if (i % 23 == 0)
    {
        logger.LogWarning("设备 {DeviceId} 响应延迟 {Elapsed}ms", deviceId, 120 + random.Next(80));
    }
    else
    {
        logger.LogInformation("采集设备 {DeviceId} 点位 {Point} 数据", deviceId, i % 64);
    }
}

await pipeline.FlushAsync();
Console.WriteLine($"已写入 {total} 条日志，丢弃 {pipeline.DroppedCount} 条。");
Console.WriteLine($"可用存储：{string.Join("、", analyzer.AvailableStores)}");

Section("2) 聚合分析（按级别/模块/趋势/高频错误）");
var statistics = await analyzer.AnalyzeAsync(store: "Sqlite", bucket: TimeSpan.FromHours(1));
Console.WriteLine($"总条数：{statistics.Total}");
Console.WriteLine($"级别分布：{string.Join("  ", statistics.ByLevel.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))}");
Console.WriteLine($"模块分布：{string.Join("  ", statistics.ByModule.Select(kv => $"{kv.Key}={kv.Value}"))}");
Console.WriteLine("高频错误 Top：");
foreach (var group in statistics.TopErrors)
{
    Console.WriteLine($"    x{group.Count,-5}{group.Key}");
}

Section("3) 条件检索：只看错误，最近 5 条");
var errorPage = await analyzer.QueryAsync(new LogQuery { MinLevel = LogLevel.Error, Take = 5 }, store: "Sqlite");
Console.WriteLine($"共 {errorPage.Total} 条错误，展示 {errorPage.Items.Count} 条：");
foreach (var record in errorPage.Items)
{
    Console.WriteLine($"    [{record.Timestamp:MM-dd HH:mm:ss}] [{record.Module}] {record.Message}");
}

Section("4) 链路还原：按 TraceId 取回一次业务操作的全部日志");
var trace = await analyzer.GetTraceAsync("trk-0001", store: "Sqlite");
Console.WriteLine($"链路 trk-0001 共 {trace.Count} 条日志：");
foreach (var record in trace.Take(5))
{
    Console.WriteLine($"    [{record.Level}] {record.Message}");
}

Section("5) 同一份数据在文件存储上分析（验证存储适配一致性）");
var fileStats = await analyzer.AnalyzeAsync(store: "File", bucket: TimeSpan.FromHours(1));
Console.WriteLine($"文件存储总条数：{fileStats.Total}，错误数：{fileStats.ByLevel.GetValueOrDefault(LogLevel.Error)}");

await host.StopAsync();

Section("完成");
Console.WriteLine($"日志目录：{logDirectory}");
Console.WriteLine($"用户数据库：{Path.Combine(demoRoot, "users", "trae-users.db")}");

// ---------------------------------------------------------------------------
// 辅助方法
// ---------------------------------------------------------------------------
static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 72));
    Console.WriteLine($"  {title}");
    Console.WriteLine(new string('=', 72));
}

static void PrintCapabilities(string title, UserCapabilities capabilities, IReadOnlyList<string> resources)
{
    Console.WriteLine($"{title}（角色：{string.Join("、", capabilities.Roles)}）");
    Console.WriteLine($"    {"资源",-18}{"可见",-8}{"可操作",-10}{"可编辑",-10}{"可管理",-8}");
    foreach (var resource in resources)
    {
        Console.WriteLine(
            $"    {resource,-18}{Flag(capabilities.IsVisible(resource)),-8}{Flag(capabilities.IsOperable(resource)),-10}" +
            $"{Flag(capabilities.IsEditable(resource)),-10}{Flag(capabilities.IsManageable(resource)),-8}");
    }
}

static string Flag(bool allowed) => allowed ? "是" : "否";
