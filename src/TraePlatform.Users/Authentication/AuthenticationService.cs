using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TraePlatform.Logging;
using TraePlatform.Users.Authorization;
using TraePlatform.Users.Configuration;
using TraePlatform.Users.Domain;
using TraePlatform.Users.Persistence;

namespace TraePlatform.Users.Authentication;

/// <summary>
/// 鉴权服务实现。会话保存在内存中（适用于上位机单进程场景），
/// 如需跨进程共享可替换会话存储实现。
/// </summary>
public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IDbContextFactory<UserDbContext> _contextFactory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthorizationService _authorizationService;
    private readonly UserModuleOptions _options;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly ConcurrentDictionary<string, UserSession> _sessions = new(StringComparer.Ordinal);

    public AuthenticationService(
        IDbContextFactory<UserDbContext> contextFactory,
        IPasswordHasher passwordHasher,
        IAuthorizationService authorizationService,
        IOptions<UserModuleOptions> options,
        ILogger<AuthenticationService> logger)
    {
        _contextFactory = contextFactory;
        _passwordHasher = passwordHasher;
        _authorizationService = authorizationService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LoginResult> LoginAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        using var scope = LogContextScope.Push(module: "Auth");

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            return LoginResult.Fail(LoginFailureReason.InvalidCredentials, "用户名或口令不能为空。");
        }

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken).ConfigureAwait(false);

        if (user is null || !_passwordHasher.Verify(password, user.PasswordHash))
        {
            _logger.LogWarning("登录失败：用户名或口令错误，用户名 {UserName}", userName);
            return LoginResult.Fail(LoginFailureReason.InvalidCredentials, "用户名或口令错误。");
        }

        if (user.Status == UserStatus.Disabled)
        {
            _logger.LogWarning("登录被拒：账号已禁用，用户名 {UserName}", userName);
            return LoginResult.Fail(LoginFailureReason.Disabled, "账号已被禁用，请联系管理员。");
        }

        if (user.Status == UserStatus.Locked)
        {
            _logger.LogWarning("登录被拒：账号已锁定，用户名 {UserName}", userName);
            return LoginResult.Fail(LoginFailureReason.Locked, "账号已被锁定，请联系管理员。");
        }

        user.LastLoginAt = DateTimeOffset.Now;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var capabilities = await _authorizationService.GetCapabilitiesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var session = new UserSession
        {
            Token = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Roles = capabilities.Roles,
            Capabilities = capabilities,
            CreatedAt = DateTimeOffset.Now,
            ExpiresAt = DateTimeOffset.Now.Add(_options.SessionLifetime)
        };
        _sessions[session.Token] = session;

        _logger.LogInformation("登录成功：{DisplayName}（{UserName}），角色 {Roles}",
            user.DisplayName, user.UserName, string.Join(",", capabilities.Roles));
        return LoginResult.Ok(session);
    }

    public Task LogoutAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        using var scope = LogContextScope.Push(module: "Auth");

        if (!string.IsNullOrEmpty(sessionToken) && _sessions.TryRemove(sessionToken, out var session))
        {
            _logger.LogInformation("用户登出：{UserName}", session.UserName);
        }
        return Task.CompletedTask;
    }

    public Task<UserSession?> GetSessionAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionToken) || !_sessions.TryGetValue(sessionToken, out var session))
        {
            return Task.FromResult<UserSession?>(null);
        }

        if (session.IsExpired)
        {
            _sessions.TryRemove(sessionToken, out _);
            return Task.FromResult<UserSession?>(null);
        }

        return Task.FromResult<UserSession?>(session);
    }
}
