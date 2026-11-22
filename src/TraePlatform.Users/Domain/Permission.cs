namespace TraePlatform.Users.Domain;

/// <summary>权限。</summary>
public class Permission
{
    public int Id { get; set; }

    /// <summary>业务资源标识，例如 System.User、Device.Monitor。</summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>动作类型。</summary>
    public PermissionAction Action { get; set; }

    /// <summary>可读名称，用于权限配置界面。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>分组，用于权限配置界面归类。</summary>
    public string? Group { get; set; }

    /// <summary>权限编码：资源:动作，例如 System.User:Manage。</summary>
    public string Code => $"{Resource}:{Action}";

    public List<RolePermission> RolePermissions { get; set; } = new();
}
