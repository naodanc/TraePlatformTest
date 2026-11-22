using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TraePlatform.Users.Authentication;
using TraePlatform.Users.Authorization;
using TraePlatform.Users.Configuration;
using TraePlatform.Users.Management;
using TraePlatform.Users.Persistence;

namespace TraePlatform.Users;

/// <summary>用户模块的依赖注入入口。</summary>
public static class UsersModule
{
    /// <summary>注册用户模块。</summary>
    public static IServiceCollection AddTraeUsers(
        this IServiceCollection services, Action<UserModuleOptions>? configure = null)
    {
        var options = new UserModuleOptions();
        configure?.Invoke(options);
        return services.AddTraeUsers(options);
    }

    /// <summary>注册用户模块（使用现成配置对象）。</summary>
    public static IServiceCollection AddTraeUsers(this IServiceCollection services, UserModuleOptions options)
    {
        services.AddSingleton<IOptions<UserModuleOptions>>(Options.Create(options));

        var fullPath = Path.GetFullPath(options.DatabasePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        services.AddDbContextFactory<UserDbContext>(builder =>
            builder.UseSqlite($"Data Source={fullPath}"));

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IAuthorizationService, AuthorizationService>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
        services.AddSingleton<IUserManagementService, UserManagementService>();
        services.AddSingleton<UserDatabaseInitializer>();
        services.AddHostedService<UserDatabaseInitializerHostedService>();

        return services;
    }
}
