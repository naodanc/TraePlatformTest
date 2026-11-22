using TraePlatform.Users.Domain;

namespace TraePlatform.Users.Management;

/// <summary>
/// 用户与权限管理服务（供“用户/角色/权限”配置界面调用）。
/// </summary>
public interface IUserManagementService
{
    Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Permission>> GetPermissionsAsync(CancellationToken cancellationToken = default);

    /// <summary>创建用户，返回用户 Id。</summary>
    Task<int> CreateUserAsync(
        string userName, string password, string displayName,
        IEnumerable<string> roleNames, CancellationToken cancellationToken = default);

    /// <summary>重设用户角色（覆盖式）。</summary>
    Task SetUserRolesAsync(int userId, IEnumerable<string> roleNames, CancellationToken cancellationToken = default);

    /// <summary>重设用户口令。</summary>
    Task SetUserPasswordAsync(int userId, string password, CancellationToken cancellationToken = default);

    /// <summary>启用/禁用/锁定用户。</summary>
    Task SetUserStatusAsync(int userId, UserStatus status, CancellationToken cancellationToken = default);

    /// <summary>创建角色并授权，返回角色 Id。</summary>
    Task<int> CreateRoleAsync(
        string name, string? description, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default);

    /// <summary>重设角色权限（覆盖式），权限码格式为“资源:动作”。</summary>
    Task SetRolePermissionsAsync(int roleId, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default);
}
