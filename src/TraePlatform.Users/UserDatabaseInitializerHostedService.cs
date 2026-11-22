using Microsoft.Extensions.Hosting;
using TraePlatform.Users.Persistence;

namespace TraePlatform.Users;

/// <summary>在宿主启动时完成用户模块数据库初始化。</summary>
internal sealed class UserDatabaseInitializerHostedService : IHostedService
{
    private readonly UserDatabaseInitializer _initializer;

    public UserDatabaseInitializerHostedService(UserDatabaseInitializer initializer) => _initializer = initializer;

    public Task StartAsync(CancellationToken cancellationToken) => _initializer.InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
