namespace TraePlatform.Users.Authentication;

/// <summary>登录失败原因。</summary>
public enum LoginFailureReason
{
    None = 0,

    /// <summary>用户名或口令错误。</summary>
    InvalidCredentials = 1,

    /// <summary>账号已禁用。</summary>
    Disabled = 2,

    /// <summary>账号已锁定。</summary>
    Locked = 3
}

/// <summary>登录结果。</summary>
public sealed class LoginResult
{
    public bool Success { get; private init; }

    public UserSession? Session { get; private init; }

    public LoginFailureReason Reason { get; private init; }

    public string? Message { get; private init; }

    public static LoginResult Ok(UserSession session) => new()
    {
        Success = true,
        Session = session,
        Reason = LoginFailureReason.None
    };

    public static LoginResult Fail(LoginFailureReason reason, string message) => new()
    {
        Success = false,
        Reason = reason,
        Message = message
    };
}
