using TraePlatform.Users.Domain;

namespace TraePlatform.Users.Authorization;

/// <summary>权限定义：一个业务资源及其支持的动作集合。</summary>
/// <param name="Resource">业务资源标识，例如 Device.Monitor。</param>
/// <param name="Name">可读名称。</param>
/// <param name="Group">分组。</param>
/// <param name="Actions">支持的权限动作。</param>
public sealed record PermissionDefinition(
    string Resource,
    string Name,
    string Group,
    IReadOnlyList<PermissionAction> Actions);

/// <summary>
/// 权限目录。业务模块在此登记自身资源与可用动作，用户模块据此生成权限项。
/// 新增业务页面时，只需在此追加资源定义。
/// </summary>
public static class PermissionCatalog
{
    /// <summary>默认权限目录（示例，可按业务扩展）。</summary>
    public static IReadOnlyList<PermissionDefinition> Default { get; } = new List<PermissionDefinition>
    {
        new("System.User", "用户管理", "系统管理",
            new[] { PermissionAction.View, PermissionAction.Operate, PermissionAction.Edit, PermissionAction.Manage }),
        new("System.Role", "角色权限", "系统管理",
            new[] { PermissionAction.View, PermissionAction.Manage }),
        new("Log.Viewer", "日志查看", "系统管理",
            new[] { PermissionAction.View }),
        new("Device.Monitor", "设备监控", "设备管理",
            new[] { PermissionAction.View, PermissionAction.Operate }),
        new("Device.Parameter", "设备参数", "设备管理",
            new[] { PermissionAction.View, PermissionAction.Edit }),
        new("Alarm.Record", "报警记录", "报警管理",
            new[] { PermissionAction.View, PermissionAction.Operate })
    };
}
