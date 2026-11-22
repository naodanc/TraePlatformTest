namespace TraePlatform.Users.Domain;

/// <summary>用户状态。</summary>
public enum UserStatus
{
    /// <summary>正常。</summary>
    Active = 0,

    /// <summary>已禁用（管理员停用）。</summary>
    Disabled = 1,

    /// <summary>已锁定（例如连续登录失败）。</summary>
    Locked = 2
}
