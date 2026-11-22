# TraePlatformTest

上位机平台层。与 UI 解耦的 .NET 8 类库，可被 WPF / Avalonia / 服务等复用。

当前已搭建 **日志模块** 与 **用户模块**。

## 目录结构

```
TraePlatform.sln
src/
  TraePlatform.Logging/   日志模块（结构化日志、多介质存储、日志分析）
  TraePlatform.Users/     用户模块（鉴权、RBAC 权限管控、用户管理）
  TraePlatform.Demo/      控制台演示（集成两模块的端到端示例）
```

## 日志模块

架构：`Serilog Sink → 有界 Channel 缓冲 → 后台批量分发 → 多存储`，业务线程写入不阻塞；
高并发下缓冲满则计数丢弃（`LogPipeline.DroppedCount`）。

- **存储适配**：`ILogStore` 抽象，内置 `FileLogStore`（JSON Lines、按天+体积滚动）与
  `SqliteLogStore`（WAL、批量事务、索引、SQL 下推聚合）。两者共用同一套过滤/聚合口径，
  分析结果一致。
- **大数据量方案**：有界 Channel + 批量异步落库 + WAL + 索引，配合
  `LogCleanupService` 按保留天数清理。
- **结构化约定**：`Module`、`TraceId`、`UserId` 作为独立列，用
  `LogContextScope.Push(...)` 或 Serilog `LogContext` 注入。
- **日志分析**：`LogAnalyzer` 支持条件检索、级别/模块分布、时间桶趋势、高频错误聚类、
  按 `TraceId` 还原业务链路。

### 接入

```csharp
builder.Services.AddTraeLogging(new LoggingOptions
{
    Storage = LogStorageMode.Console | LogStorageMode.File | LogStorageMode.Sqlite,
    MinimumLevel = LogLevel.Information,
    File   = new FileStoreOptions   { Directory = "logs" },
    Sqlite = new SqliteStoreOptions { DatabasePath = Path.Combine("logs", "trae-logs.db") }
});
```

写入与查询：

```csharp
using var scope = LogContextScope.Push(module: "Device", traceId: "trk-0001", userId: "admin");
logger.LogError("设备 {DeviceId} 通信超时", deviceId);   // Microsoft.Extensions.Logging.ILogger

await pipeline.FlushAsync();                              // 查询前强制落盘
var stats = await analyzer.AnalyzeAsync(store: "Sqlite", bucket: TimeSpan.FromHours(1));
var page  = await analyzer.QueryAsync(new LogQuery { MinLevel = LogLevel.Error, Take = 20 });
var trace = await analyzer.GetTraceAsync("trk-0001");
```

## 用户模块

- **鉴权**：`AuthenticationService.LoginAsync` 校验口令与账号状态（`Disabled` / `Locked`），
  返回含 `Capabilities` 的会话；口令使用 `PBKDF2-SHA256` + 随机盐 + 恒定时间比较。
- **权限管控**：RBAC（`User ↔ Role ↔ Permission`）。权限 = `资源:动作`，
  动作 `View / Operate / Edit / Manage` 分别对应页面的
  **可见 / 可操作 / 可编辑 / 可管理**。
- **页面能力适配**：`UserCapabilities` 提供 `IsVisible / IsOperable / IsEditable / IsManageable`
  与 `Can(resource, action)`，可直接驱动按钮/字段的显隐与可用性。
- **管理**：`IUserManagementService` 提供用户/角色/权限的增改与绑定。
- 内置角色：`Administrator`（全部）、`Operator`（查看+操作）、`Viewer`（仅查看）；
  默认管理员 `admin`，口令见 `UserModuleOptions.DefaultAdminPassword`。
  首次启动由 `UserDatabaseInitializer` 建库并写入种子数据。

### 接入

```csharp
builder.Services.AddTraeUsers(new UserModuleOptions
{
    DatabasePath = Path.Combine("data", "trae-users.db"),
    DefaultAdminPassword = "Admin@123"
});
```

```csharp
var result = await authentication.LoginAsync("admin", password);
if (result.Success && result.Session!.Capabilities.IsEditable("Device.Parameter"))
{
    // 允许编辑设备参数
}
```

## 运行演示

```bash
dotnet run --project src/TraePlatform.Demo
```

演示内容：管理员登录与权限矩阵、错误口令、创建操作员并对比权限、禁用后拒登、单点权限校验；
写入 2000 条带模块/链路/用户维度的日志，做聚合分析、错误检索、链路还原，并校验文件与
SQLite 两种存储的分析结果一致。
