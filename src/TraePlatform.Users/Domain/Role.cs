namespace TraePlatform.Users.Domain;

/// <summary>角色。权限按角色授予，用户通过角色获得权限。</summary>
public class Role
{
    public int Id { get; set; }

    /// <summary>角色名（唯一）。</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>是否内置角色（内置角色不允许删除）。</summary>
    public bool IsBuiltIn { get; set; }

    public List<UserRole> UserRoles { get; set; } = new();

    public List<RolePermission> RolePermissions { get; set; } = new();
}
