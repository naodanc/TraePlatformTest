namespace TraePlatform.Users.Domain;

/// <summary>用户。</summary>
public class User
{
    public int Id { get; set; }

    /// <summary>登录名（唯一）。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>显示名。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>口令哈希（PBKDF2，自带盐与迭代次数）。</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>账号状态。</summary>
    public UserStatus Status { get; set; } = UserStatus.Active;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset? LastLoginAt { get; set; }

    public List<UserRole> UserRoles { get; set; } = new();
}
