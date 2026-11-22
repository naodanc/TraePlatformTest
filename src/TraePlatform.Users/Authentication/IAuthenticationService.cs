namespace TraePlatform.Users.Authentication;

/// <summary>用户鉴权服务：登录、登出、会话校验。</summary>
public interface IAuthenticationService
{
    /// <summary>登录，成功后返回会话（含权限快照）。</summary>
    Task<LoginResult> LoginAsync(string userName, string password, CancellationToken cancellationToken = default);

    /// <summary>登出，销毁会话。</summary>
    Task LogoutAsync(string sessionToken, CancellationToken cancellationToken = default);

    /// <summary>校验会话令牌，返回有效会话；无效或过期返回 null。</summary>
    Task<UserSession?> GetSessionAsync(string sessionToken, CancellationToken cancellationToken = default);
}
