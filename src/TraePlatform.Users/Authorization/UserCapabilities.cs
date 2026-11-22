using TraePlatform.Users.Domain;

namespace TraePlatform.Users.Authorization;

/// <summary>
/// 用户在某一时刻的权限快照，供界面做「可见 / 可操作 / 可编辑 / 可管理」适配。
/// 界面元素在渲染前查询对应资源的动作，从而决定控件是否显示、是否启用。
/// </summary>
public sealed class UserCapabilities
{
    /// <summary>空权限（未登录或未授权）。</summary>
    public static readonly UserCapabilities Empty = new()
    {
        UserId = 0,
        Roles = Array.Empty<string>(),
        Resources = new Dictionary<string, IReadOnlySet<PermissionAction>>()
    };

    public required int UserId { get; init; }

    /// <summary>用户拥有的角色名。</summary>
    public required IReadOnlyList<string> Roles { get; init; }

    /// <summary>资源 → 允许的动作集合。</summary>
    public required IReadOnlyDictionary<string, IReadOnlySet<PermissionAction>> Resources { get; init; }

    /// <summary>是否拥有某资源的指定动作权限。</summary>
    public bool Can(string resource, PermissionAction action)
        => Resources.TryGetValue(resource, out var actions) && actions.Contains(action);

    /// <summary>页面/控件是否可见。</summary>
    public bool IsVisible(string resource) => Can(resource, PermissionAction.View);

    /// <summary>页面/控件是否可操作。</summary>
    public bool IsOperable(string resource) => Can(resource, PermissionAction.Operate);

    /// <summary>页面/控件是否可编辑。</summary>
    public bool IsEditable(string resource) => Can(resource, PermissionAction.Edit);

    /// <summary>页面/控件是否可管理。</summary>
    public bool IsManageable(string resource) => Can(resource, PermissionAction.Manage);

    /// <summary>是否拥有指定角色。</summary>
    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}
