using TraePlatform.Users.Authorization;

namespace TraePlatform.Users.Authentication;

/// <summary>登录会话。</summary>
public sealed class UserSession
{
    public required string Token { get; init; }

    public required int UserId { get; init; }

    public required string UserName { get; init; }

    public required string DisplayName { get; init; }

    public required IReadOnlyList<string> Roles { get; init; }

    /// <summary>该会话对应的权限快照，供界面能力适配使用。</summary>
    public required UserCapabilities Capabilities { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset ExpiresAt { get; init; }

    public bool IsExpired => DateTimeOffset.Now >= ExpiresAt;
}
